using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HAM_DeBugger.KetchupHierachy
{
    [Serializable]
    public class TagColorEntry
    {
        public string tag = "Untagged";
        public Color color = Color.cyan;
    }

    [Serializable]
    public class KetchupHierachySettings
    {
        // Feature toggles
        public bool enableTagHighlight = true;
        public bool enableTreeLines = true;
        public bool enableComponentIcons = true;
        public bool enableMissingScript = true;
        public bool enableRaycastCheckbox = true;
        public bool enableReferenceFinder = true;
        public bool enableActiveToggle = true;
        public bool enableRowAlternation = false;
        public bool enableChildCountBadge = false;
        public bool enablePrefabIndicator = true;
        public bool enableSeparatorHeaders = true;

        // Tag highlight
        public float colorBandWidth = 3f;
        public float tagRowAlpha = 0.12f;
        public List<TagColorEntry> tagColors = new List<TagColorEntry>
        {
            new TagColorEntry { tag = "Player",     color = new Color(0.20f, 0.85f, 0.30f, 1f) },
            new TagColorEntry { tag = "Enemy",      color = new Color(0.90f, 0.20f, 0.20f, 1f) },
            new TagColorEntry { tag = "MainCamera", color = new Color(0.20f, 0.55f, 0.95f, 1f) },
            new TagColorEntry { tag = "UI",         color = new Color(0.95f, 0.75f, 0.10f, 1f) },
            new TagColorEntry { tag = "Respawn",    color = new Color(0.70f, 0.30f, 0.95f, 1f) },
        };

        // Tree lines
        public Color treeLineColor = new Color(0.65f, 0.65f, 0.65f, 0.75f);
        public float treeLineThickness = 1.5f;
        public bool treeLineFollowTagColor = true;

        // Separator headers
        public Color separatorBgColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        public Color separatorTextColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        public int separatorFontSize = 10;
        public bool separatorBold = true;
        public bool separatorUppercase = false;

        // Component icons
        public bool iconCamera = true;
        public bool iconLight = true;
        public bool iconRigidbody = true;
        public bool iconCollider = true;
        public bool iconAudio = true;
        public bool iconParticle = true;
        public bool iconCanvas = true;
        public bool iconAnimator = true;
        public bool iconNavAgent = true;

        // Missing script
        public Color missingScriptColor = new Color(0.95f, 0.15f, 0.15f, 0.50f);

        // Reference finder
        public Color refHighlightColor = new Color(0.20f, 0.70f, 1.00f, 0.30f);

        // Row alternation
        public Color rowAltColor = new Color(0f, 0f, 0f, 0.06f);

        // Prefab indicator
        public Color prefabColor = new Color(0.45f, 0.70f, 1.00f, 1f);

        private const string KEY = "KetchupHierachy_v3";

        public void Save() => EditorPrefs.SetString(KEY, JsonUtility.ToJson(this));

        public static KetchupHierachySettings Load()
        {
            var s = new KetchupHierachySettings();
            if (EditorPrefs.HasKey(KEY))
                try { JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(KEY), s); } catch { }
            return s;
        }
    }


    [InitializeOnLoad]
    public static class KetchupHierachy
    {
        public static KetchupHierachySettings Settings { get; private set; }

        private static readonly HashSet<int> _refIDs = new HashSet<int>();
        private static GameObject _refTarget = null;
        private static readonly Dictionary<int, Color> _tagColorCache = new Dictionary<int, Color>();

        // Matches: === TEXT ===  |  --- TEXT ---  |  [ TEXT ]  |  // TEXT  |  ## TEXT
        private static readonly Regex _separatorRx = new Regex(
            @"^(?:={2,}|-{2,}|\[|\/{2}|#{1,2})\s*(?<label>[^=\-\[\]#\/]*?)\s*(?:={2,}|-{2,}|\])?$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private const float INDENT = 14f;
        private const float ICON_SIZE = 14f;
        private const float RIGHT_PADDING = 2f;

        static KetchupHierachy()
        {
            Settings = KetchupHierachySettings.Load();
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
            EditorApplication.hierarchyChanged += () => _tagColorCache.Clear();
        }

        public static void ReloadSettings()
        {
            Settings = KetchupHierachySettings.Load();
            _tagColorCache.Clear();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void OnGUI(int instanceID, Rect row)
        {
            var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (go == null) return;

            if (Settings.enableSeparatorHeaders && TryDrawSeparator(row, go))
                return;

            if (Settings.enableRowAlternation) DrawRowAlt(row, go);
            if (Settings.enableMissingScript) DrawMissingHighlight(row, go);
            if (Settings.enableReferenceFinder && _refIDs.Contains(instanceID)) DrawRefHighlight(row);

            Color tagColor = Color.clear;
            if (Settings.enableTagHighlight) tagColor = DrawTagBand(row, go);
            if (Settings.enableTreeLines) DrawTreeLines(row, go, tagColor);

            float cursor = row.xMax - RIGHT_PADDING;
            if (Settings.enableActiveToggle) cursor = DrawActiveToggle(row, go, cursor);
            if (Settings.enableRaycastCheckbox) cursor = DrawRaycastCheckbox(row, go, cursor);
            if (Settings.enableComponentIcons) cursor = DrawComponentIcons(row, go, cursor);
            if (Settings.enableChildCountBadge) DrawChildCount(row, go, cursor);
            if (Settings.enablePrefabIndicator) DrawPrefabDot(row, go);
        }

        // Separator / Header

        private static bool TryDrawSeparator(Rect row, GameObject go)
        {
            var m = _separatorRx.Match(go.name.Trim());
            if (!m.Success) return false;

            string rawLabel = m.Groups["label"].Value.Trim();
            if (string.IsNullOrEmpty(rawLabel)) rawLabel = go.name;
            string label = Settings.separatorUppercase ? rawLabel.ToUpper() : rawLabel;

            // Full-width background
            EditorGUI.DrawRect(new Rect(0, row.y, row.xMax + 20f, row.height), Settings.separatorBgColor);

            // Left accent stripe (tag color if set, otherwise neutral)
            Color accent = GetTagColor(go);
            if (accent == Color.clear) accent = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            EditorGUI.DrawRect(new Rect(0, row.y, 3f, row.height), accent);

            // Top rule
            EditorGUI.DrawRect(new Rect(0, row.y, row.xMax + 20f, 1f),
                new Color(accent.r, accent.g, accent.b, 0.5f));

            // Centered label
            var style = new GUIStyle(EditorStyles.label)
            {
                fontSize = Settings.separatorFontSize,
                fontStyle = Settings.separatorBold ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Settings.separatorTextColor }
            };
            GUI.Label(new Rect(row.x, row.y, row.width, row.height), label, style);

            // Child count hint on the far right
            int childCount = go.transform.childCount;
            if (childCount > 0)
            {
                GUI.Label(
                    new Rect(row.xMax - 40f, row.y, 36f, row.height),
                    $"[{childCount}]",
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleRight,
                        normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
                    });
            }

            return true;
        }

        // Tag Band + Row Tint 
        private static Color DrawTagBand(Rect row, GameObject go)
        {
            Color c = GetTagColor(go);
            if (c == Color.clear) return Color.clear;

            EditorGUI.DrawRect(new Rect(0, row.y, Settings.colorBandWidth, row.height), c);

            var tint = new Color(c.r, c.g, c.b, Settings.tagRowAlpha);
            EditorGUI.DrawRect(new Rect(Settings.colorBandWidth, row.y, row.xMax - Settings.colorBandWidth + 20f, row.height), tint);

            return c;
        }

        public static Color GetTagColor(GameObject go)
        {
            int id = go.GetInstanceID();
            if (_tagColorCache.TryGetValue(id, out Color cached)) return cached;

            foreach (var e in Settings.tagColors)
                if (e.tag == go.tag) { _tagColorCache[id] = e.color; return e.color; }

            _tagColorCache[id] = Color.clear;
            return Color.clear;
        }

        // Tree Lines Drawer
        private static void DrawTreeLines(Rect row, GameObject go, Color rootTagColor)
        {
            Transform t = go.transform;
            if (t.parent == null) return;

            int depth = GetDepth(t);
            Transform current = t;
            float w = Settings.treeLineThickness;

            for (int lvl = depth; lvl >= 1; lvl--)
            {
                bool isImmediate = (lvl == depth);
                bool isLastSibling = IsLastSibling(current);
                float x = row.x - (depth - lvl + 1) * INDENT - 7f;

                Color lineColor = Settings.treeLineColor;
                if (Settings.treeLineFollowTagColor && current.parent != null)
                {
                    Color pc = GetTagColor(current.parent.gameObject);
                    if (pc != Color.clear)
                        lineColor = new Color(pc.r, pc.g, pc.b, Settings.treeLineColor.a);
                }

                float mid = row.y + row.height * 0.5f;

                if (isImmediate)
                {
                    EditorGUI.DrawRect(new Rect(x, row.y, w, row.height * 0.5f), lineColor);
                    EditorGUI.DrawRect(new Rect(x, mid - w * 0.5f, INDENT * 0.5f + 2f, w), lineColor);
                    if (!isLastSibling)
                        EditorGUI.DrawRect(new Rect(x, mid, w, row.height * 0.5f), lineColor);
                }
                else
                {
                    if (!isLastSibling)
                        EditorGUI.DrawRect(new Rect(x, row.y, w, row.height), lineColor);
                }

                if (current.parent == null) break;
                current = current.parent;
            }
        }

        private static int GetDepth(Transform t) { int d = 0; while (t.parent != null) { d++; t = t.parent; } return d; }
        private static bool IsLastSibling(Transform t) => t.parent == null || t.GetSiblingIndex() == t.parent.childCount - 1;

        // Missing Script tracker

        private static void DrawMissingHighlight(Rect row, GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null) continue;
                EditorGUI.DrawRect(row, Settings.missingScriptColor);
                GUI.Label(new Rect(row.xMax - 18f, row.y + 1f, 14f, row.height - 2f),
                    new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image, "Missing Script"));
                return;
            }
        }

        // Reference Finder
        public static void FindReferences(GameObject target)
        {
            _refIDs.Clear();
            _refTarget = target;
            if (target == null) { EditorApplication.RepaintHierarchyWindow(); return; }

            foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go == target) continue;
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var so = new SerializedObject(comp);
                    var sp = so.GetIterator();
                    bool found = false;
                    while (!found && sp.NextVisible(true))
                    {
                        if (sp.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var val = sp.objectReferenceValue;
                        if (val == null) continue;
                        if (val == target) found = true;
                        else if (val is Component c2 && c2.gameObject == target) found = true;
                    }
                    if (found) { _refIDs.Add(go.GetInstanceID()); break; }
                }
            }
            EditorApplication.RepaintHierarchyWindow();
        }

        public static void ClearReferences()
        {
            _refIDs.Clear(); _refTarget = null;
            EditorApplication.RepaintHierarchyWindow();
        }

        public static GameObject GetReferenceTarget() => _refTarget;
        public static int GetReferenceCount() => _refIDs.Count;

        private static void DrawRefHighlight(Rect row)
        {
            EditorGUI.DrawRect(row, Settings.refHighlightColor);
            EditorGUI.DrawRect(new Rect(Settings.colorBandWidth, row.y, 2f, row.height),
                new Color(0.3f, 0.85f, 1f, 1f));
        }


        private static float DrawRaycastCheckbox(Rect row, GameObject go, float cursor)
        {
            var graphic = go.GetComponent<Graphic>();
            if (graphic == null) return cursor;

            float sz = row.height;
            var checkRect = new Rect(cursor - sz, row.y, sz, sz);
            var labelRect = new Rect(checkRect.x - 14f, row.y, 14f, row.height);

            GUI.Label(labelRect, new GUIContent("RT", "Raycast Target"),
                new GUIStyle(EditorStyles.miniLabel) { fontSize = 7, normal = { textColor = new Color(0.75f, 0.75f, 0.75f) } });

            EditorGUI.BeginChangeCheck();
            bool next = EditorGUI.Toggle(checkRect, graphic.raycastTarget);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(graphic, "Toggle Raycast Target");
                graphic.raycastTarget = next;
                EditorUtility.SetDirty(graphic);
            }

            return labelRect.x - 2f;
        }


        private static float DrawActiveToggle(Rect row, GameObject go, float cursor)
        {
            var r = new Rect(cursor - row.height, row.y, row.height, row.height);
            EditorGUI.BeginChangeCheck();
            bool active = EditorGUI.Toggle(r, go.activeSelf);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(go, "Toggle Active");
                go.SetActive(active);
            }
            return r.x - 2f;
        }

        private static float DrawComponentIcons(Rect row, GameObject go, float cursor)
        {
            float iconY = row.y + (row.height - ICON_SIZE) * 0.5f;
            foreach (var (tex, tip) in BuildIconList(go))
            {
                if (tex == null) continue;
                float x = cursor - ICON_SIZE;
                if (x < row.x + 80f) break;
                var r = new Rect(x, iconY, ICON_SIZE, ICON_SIZE);
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true, 0, Color.white, 0, 0);
                EditorGUI.LabelField(r, new GUIContent("", tip));
                cursor = x - 1f;
            }
            return cursor;
        }

        private static List<(Texture, string)> BuildIconList(GameObject go)
        {
            var list = new List<(Texture, string)>();
            Texture Ico(string n) => EditorGUIUtility.IconContent(n).image;

            if (Settings.iconCamera && go.TryGetComponent<Camera>(out _)) list.Add((Ico("Camera Icon"), "Camera"));
            if (Settings.iconLight && go.TryGetComponent<Light>(out _)) list.Add((Ico("Light Icon"), "Light"));
            if (Settings.iconRigidbody && go.TryGetComponent<Rigidbody>(out _)) list.Add((Ico("Rigidbody Icon"), "Rigidbody"));
            if (Settings.iconRigidbody && go.TryGetComponent<Rigidbody2D>(out _)) list.Add((Ico("Rigidbody2D Icon"), "Rigidbody2D"));
            if (Settings.iconCollider && (go.GetComponent<Collider>() != null ||
                                            go.GetComponent<Collider2D>() != null)) list.Add((Ico("BoxCollider Icon"), "Collider"));
            if (Settings.iconAudio && go.TryGetComponent<AudioSource>(out _)) list.Add((Ico("AudioSource Icon"), "AudioSource"));
            if (Settings.iconParticle && go.TryGetComponent<ParticleSystem>(out _)) list.Add((Ico("ParticleSystem Icon"), "ParticleSystem"));
            if (Settings.iconCanvas && go.TryGetComponent<Canvas>(out _)) list.Add((Ico("Canvas Icon"), "Canvas"));
            if (Settings.iconAnimator && go.TryGetComponent<Animator>(out _)) list.Add((Ico("Animator Icon"), "Animator"));
            if (Settings.iconNavAgent && go.GetComponent("NavMeshAgent") != null) list.Add((Ico("NavMeshAgent Icon"), "NavMeshAgent"));

            return list;
        }


        private static void DrawRowAlt(Rect row, GameObject go)
        {
            if (go.transform.GetSiblingIndex() % 2 == 0)
                EditorGUI.DrawRect(row, Settings.rowAltColor);
        }


        private static void DrawChildCount(Rect row, GameObject go, float cursor)
        {
            int count = go.transform.childCount;
            if (count == 0) return;

            string label = count.ToString();
            float w = Mathf.Max(16f, label.Length * 7f);
            var r = new Rect(cursor - w - 2f, row.y + 2f, w, row.height - 4f);

            EditorGUI.DrawRect(r, new Color(0.25f, 0.25f, 0.25f, 0.7f));
            GUI.Label(r, label, new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 8,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            });
        }


        private static void DrawPrefabDot(Rect row, GameObject go)
        {
            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if (status == PrefabInstanceStatus.NotAPrefab) return;

            Color c = status == PrefabInstanceStatus.Connected
                ? Settings.prefabColor
                : new Color(1f, 0.6f, 0.2f, 1f);

            float dotSize = 4f;
            EditorGUI.DrawRect(
                new Rect(row.x - 1f, row.y + (row.height - dotSize) * 0.5f, dotSize, dotSize), c);
        }
    }

    public class KetchupHierachyWindow : EditorWindow
    {
        private KetchupHierachySettings _s;
        private Vector2 _scroll;
        private Vector2 _tagScroll;
        private int _tab = 0;

        private static readonly string[] TABS = { "Features", "Tags", "Icons", "References", "Separators", "About" };

        [MenuItem("HAM_DeBugger/Ketchup Hierachy %#h")]
        public static void Open()
        {
            var w = GetWindow<KetchupHierachyWindow>("Ketchup Hierachy");
            w.minSize = new Vector2(380f, 540f);
            w.Show();
        }

        private void OnEnable() => _s = KetchupHierachySettings.Load();

        private void OnGUI()
        {
            DrawHeader();
            _tab = GUILayout.Toolbar(_tab, TABS, GUILayout.Height(24f));
            Separator();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: TabFeatures(); break;
                case 1: TabTags(); break;
                case 2: TabIcons(); break;
                case 3: TabReferences(); break;
                case 4: TabSeparators(); break;
                case 5: TabAbout(); break;
            }
            EditorGUILayout.EndScrollView();
            DrawFooter();
        }

        private void DrawHeader()
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Ketchup Hierachy",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter },
                GUILayout.Height(24f));
            GUILayout.Space(4f);
            Separator();
        }

        private void DrawFooter()
        {
            Separator();
            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.5f);
            if (GUILayout.Button("Save & Apply", GUILayout.Height(28f)))
            {
                _s.Save();
                KetchupHierachy.ReloadSettings();
            }
            GUI.backgroundColor = prev;
            if (GUILayout.Button("Reset Defaults", GUILayout.Height(28f), GUILayout.Width(110f)))
            {
                if (EditorUtility.DisplayDialog("Reset", "Reset all settings to defaults?", "Reset", "Cancel"))
                {
                    _s = new KetchupHierachySettings();
                    _s.Save();
                    KetchupHierachy.ReloadSettings();
                }
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);

        }

        //  Tab : Features
        private void TabFeatures()
        {
            Section("Core Features");

            BoolField("Tag Highlight", "Tag-based color band + faint row tint", ref _s.enableTagHighlight);
            if (_s.enableTagHighlight)
            {
                Indent(() =>
                {
                    _s.colorBandWidth = EditorGUILayout.Slider("Band Width", _s.colorBandWidth, 1f, 12f);
                    _s.tagRowAlpha = EditorGUILayout.Slider("Row Tint Alpha", _s.tagRowAlpha, 0f, 0.5f);
                });
            }

            BoolField("Tree Lines", "Parent-child connector lines", ref _s.enableTreeLines);
            if (_s.enableTreeLines)
            {
                Indent(() =>
                {
                    _s.treeLineColor = EditorGUILayout.ColorField("Line Color", _s.treeLineColor);
                    _s.treeLineThickness = EditorGUILayout.Slider("Thickness", _s.treeLineThickness, 0.5f, 4f);
                    _s.treeLineFollowTagColor = EditorGUILayout.Toggle("Follow Parent Tag Color", _s.treeLineFollowTagColor);
                });
            }

            BoolField("Component Icons", "Key component icons on the right", ref _s.enableComponentIcons);

            BoolField("Missing Script Highlight", "Red highlight on missing scripts", ref _s.enableMissingScript);
            if (_s.enableMissingScript)
                Indent(() => _s.missingScriptColor = EditorGUILayout.ColorField("Color", _s.missingScriptColor));

            BoolField("Raycast Target Checkbox", "Inline RT toggle for UI Graphics", ref _s.enableRaycastCheckbox);

            BoolField("Reference Finder", "Highlight objects referencing target", ref _s.enableReferenceFinder);
            if (_s.enableReferenceFinder)
                Indent(() => _s.refHighlightColor = EditorGUILayout.ColorField("Highlight Color", _s.refHighlightColor));

            BoolField("Separator Headers", "Render === / --- / [ ] objects as dividers", ref _s.enableSeparatorHeaders);

            GUILayout.Space(8f);
            Section("Additional Features");

            BoolField("Active Toggle", "Inline active toggle on the right", ref _s.enableActiveToggle);

            BoolField("Prefab Indicator", "Dot marking prefab instance status", ref _s.enablePrefabIndicator);
            if (_s.enablePrefabIndicator)
                Indent(() => _s.prefabColor = EditorGUILayout.ColorField("Prefab Color", _s.prefabColor));

            BoolField("Row Alternation", "Alternating row background tint", ref _s.enableRowAlternation);
            if (_s.enableRowAlternation)
                Indent(() => _s.rowAltColor = EditorGUILayout.ColorField("Row Alt Color", _s.rowAltColor));

            BoolField("Child Count Badge", "Show child count badge", ref _s.enableChildCountBadge);
        }


        //  Tab : Tags
        private void TabTags()
        {
            Section("Tag to Color Mapping");
            EditorGUILayout.HelpBox("Each tag gets a color band and a faint row tint. Hit 'Save & Apply' after editing.", MessageType.Info);
            GUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tag", EditorStyles.boldLabel, GUILayout.Width(130f));
            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel, GUILayout.Width(80f));
            EditorGUILayout.LabelField("Band", GUILayout.Width(20f));
            EditorGUILayout.EndHorizontal();

            _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll, GUILayout.MaxHeight(280f));
            string[] allTags = UnityEditorInternal.InternalEditorUtility.tags;
            int removeAt = -1;

            for (int i = 0; i < _s.tagColors.Count; i++)
            {
                var e = _s.tagColors[i];
                EditorGUILayout.BeginHorizontal();

                int idx = Array.IndexOf(allTags, e.tag);
                int newIdx = EditorGUILayout.Popup(idx < 0 ? 0 : idx, allTags, GUILayout.Width(130f));
                if (newIdx >= 0 && newIdx < allTags.Length) e.tag = allTags[newIdx];

                e.color = EditorGUILayout.ColorField(GUIContent.none, e.color, GUILayout.Width(80f));

                var br = EditorGUILayout.GetControlRect(false, 16f, GUILayout.Width(20f));
                EditorGUI.DrawRect(new Rect(br.x, br.y + 1f, 6f, br.height - 2f), e.color);

                if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20f))) removeAt = i;

                EditorGUILayout.EndHorizontal();
                _s.tagColors[i] = e;
            }
            EditorGUILayout.EndScrollView();

            if (removeAt >= 0) _s.tagColors.RemoveAt(removeAt);

            GUILayout.Space(4f);
            if (GUILayout.Button("+ Add Tag Entry", GUILayout.Height(22f)))
                _s.tagColors.Add(new TagColorEntry
                {
                    tag = allTags.Length > 0 ? allTags[0] : "Untagged",
                    color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1f)
                });
        }


        //  Tab : Icons
        private void TabIcons()
        {
            Section("Component Icon Visibility");
            EditorGUILayout.HelpBox("Checked components show a small icon to the right of the object name.", MessageType.Info);
            GUILayout.Space(6f);

            BoolField("Camera", "", ref _s.iconCamera);
            BoolField("Light", "", ref _s.iconLight);
            BoolField("Rigidbody", "", ref _s.iconRigidbody);
            BoolField("Collider", "", ref _s.iconCollider);
            BoolField("Audio Source", "", ref _s.iconAudio);
            BoolField("Particle System", "", ref _s.iconParticle);
            BoolField("Canvas", "", ref _s.iconCanvas);
            BoolField("Animator", "", ref _s.iconAnimator);
            BoolField("NavMesh Agent", "", ref _s.iconNavAgent);
        }


        //  Tab : References
        private static GameObject _refTarget;

        private void TabReferences()
        {
            Section("Reference Finder");
            EditorGUILayout.HelpBox(
                "Highlights all scene objects that hold a serialized reference to the target GameObject or any of its components.",
                MessageType.Info);
            GUILayout.Space(8f);

            EditorGUILayout.LabelField("Target Object", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _refTarget = (GameObject)EditorGUILayout.ObjectField(_refTarget, typeof(GameObject), true, GUILayout.Height(22f));
            if (EditorGUI.EndChangeCheck() && _refTarget == null) KetchupHierachy.ClearReferences();

            GUILayout.Space(4f);
            if (GUILayout.Button("Use Selection", EditorStyles.miniButton, GUILayout.Width(100f)))
                if (Selection.activeGameObject != null) _refTarget = Selection.activeGameObject;

            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = (_refTarget != null);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.75f, 1f);
            if (GUILayout.Button("Find References", GUILayout.Height(28f)))
            {
                KetchupHierachy.FindReferences(_refTarget);
                EditorUtility.DisplayDialog("Reference Finder",
                    $"Found {KetchupHierachy.GetReferenceCount()} object(s) referencing '{_refTarget.name}'.", "OK");
            }
            GUI.backgroundColor = prev;
            GUI.enabled = true;

            if (GUILayout.Button("Clear", GUILayout.Height(28f), GUILayout.Width(60f)))
            {
                KetchupHierachy.ClearReferences();
                _refTarget = null;
            }
            EditorGUILayout.EndHorizontal();

            var ct = KetchupHierachy.GetReferenceTarget();
            if (ct != null)
            {
                GUILayout.Space(8f);
                EditorGUILayout.LabelField(
                    $"Active: '{ct.name}'  ->  {KetchupHierachy.GetReferenceCount()} reference(s)",
                    EditorStyles.helpBox);
            }
        }


        private void TabSeparators()
        {
            Section("Separator / Header Objects");
            EditorGUILayout.HelpBox(
                "GameObjects whose names match the patterns below are rendered as full-width section headers.\n\n" +
                "Supported naming patterns:\n" +
                "  === Label ===\n" +
                "  --- Label ---\n" +
                "  [ Label ]\n" +
                "  // Label\n" +
                "  ## Label\n\n" +
                "Tag them 'EditorOnly' so they are stripped from builds automatically.",
                MessageType.Info);
            GUILayout.Space(8f);

            BoolField("Enable Separator Headers", "", ref _s.enableSeparatorHeaders);
            GUILayout.Space(6f);

            Section("Appearance");
            _s.separatorBgColor = EditorGUILayout.ColorField("Background Color", _s.separatorBgColor);
            _s.separatorTextColor = EditorGUILayout.ColorField("Text Color", _s.separatorTextColor);
            _s.separatorFontSize = EditorGUILayout.IntSlider("Font Size", _s.separatorFontSize, 8, 14);
            _s.separatorBold = EditorGUILayout.Toggle("Bold", _s.separatorBold);
            _s.separatorUppercase = EditorGUILayout.Toggle("Uppercase", _s.separatorUppercase);

            GUILayout.Space(10f);
            Section("Quick Create");
            EditorGUILayout.HelpBox("Creates a separator object at the scene root. Drag it to the desired position.", MessageType.None);
            GUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("=== New Separator ===")) CreateSeparator("=== New Separator ===");
            if (GUILayout.Button("--- New Separator ---")) CreateSeparator("--- New Separator ---");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("[ New Separator ]")) CreateSeparator("[ New Separator ]");
            if (GUILayout.Button("## New Separator")) CreateSeparator("## New Separator");
            EditorGUILayout.EndHorizontal();
        }

        private static void CreateSeparator(string name)
        {
            var go = new GameObject(name) { tag = "EditorOnly" };
            Undo.RegisterCreatedObjectUndo(go, "Create Separator");
            Selection.activeGameObject = go;
        }



        private void TabAbout()
        {
            Section("Ketchup Hierachy");
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Unity 6 compatible  |  Editor-only, zero runtime overhead",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 10 });
            GUILayout.Space(10f);

            EditorGUILayout.HelpBox(
                "Core\n" +
                "  Tag Highlight          color band + faint row tint\n" +
                "  Tree Lines             parent-child connectors with thickness control\n" +
                "  Component Icons        icons for key components\n" +
                "  Missing Script         red highlight for broken references\n" +
                "  Raycast Checkbox       inline RT toggle for UI Graphics\n" +
                "  Reference Finder       scene-wide reference search\n" +
                "  Separator Headers      visual section dividers (=== / --- / [])\n\n" +
                "Additional\n" +
                "  Active Toggle          inline active state toggle\n" +
                "  Prefab Indicator       dot showing prefab status\n" +
                "  Row Alternation        alternating row tint\n" +
                "  Child Count Badge      child count badge",
                MessageType.None);

            GUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Tips\n" +
                "  Always click 'Save & Apply' after changing settings.\n" +
                "  Shortcut: Ctrl+Shift+H\n" +
                "  Right-click any GameObject > Ketchup Hierachy for quick actions.\n" +
                "  Separator objects tagged 'EditorOnly' are stripped from builds.",
                MessageType.Info);
        }


        private static void Separator()
            => EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1f), new Color(0.45f, 0.45f, 0.45f, 0.5f));

        private static void Section(string title)
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1f), new Color(0.45f, 0.45f, 0.45f, 0.3f));
            GUILayout.Space(4f);
        }

        private static void BoolField(string label, string tooltip, ref bool value)
        {
            EditorGUILayout.BeginHorizontal();
            value = EditorGUILayout.Toggle(value, GUILayout.Width(18f));
            var style = new GUIStyle(EditorStyles.label);
            if (value) style.fontStyle = FontStyle.Bold;
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), style);
            EditorGUILayout.EndHorizontal();
        }

        private static void Indent(Action draw) { EditorGUI.indentLevel++; draw(); EditorGUI.indentLevel--; }
    }

    public static class HierarchyContextMenu
    {
        [MenuItem("GameObject/Ketchup Hierachy/Find References to This", false, 48)]
        private static void FindRefs() => KetchupHierachy.FindReferences(Selection.activeGameObject);

        [MenuItem("GameObject/Ketchup Hierachy/Clear Reference Highlights", false, 49)]
        private static void ClearRefs() => KetchupHierachy.ClearReferences();

        [MenuItem("GameObject/Ketchup Hierachy/Open Settings", false, 50)]
        private static void OpenSettings() => KetchupHierachyWindow.Open();

        [MenuItem("GameObject/Ketchup Hierachy/Find References to This", true)]
        private static bool ValidateFindRefs() => Selection.activeGameObject != null;
    }
}