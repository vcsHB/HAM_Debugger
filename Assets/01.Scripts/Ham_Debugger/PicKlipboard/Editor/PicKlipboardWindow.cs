using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;

namespace HAM_DeBugger.PicKlipboardUtility
{
    public enum AssetFilteringType
    {
        All,
        GameObject,
        Folders,
        Prefab,
        Material,
        Texture,
        Script,
        Asset,
    }
    /// <summary>
    /// Utility to temporarily store objects from the Project or Scene.
    /// </summary>
    public class PicKlipboardWindow : EditorWindow
    {
        // Clipboard Data Structure
        [System.Serializable]
        public class ClipboardObject
        {
            public Object targetObject;
            public bool isLocked;
            public string description;

            public ClipboardObject(Object targetObject, bool isLocked, string description)
            {
                this.targetObject = targetObject;
                this.isLocked = isLocked;
                this.description = description;
            }
        }
        // ====================================================================

        public List<ClipboardObject> objectList = new List<ClipboardObject>();
        private List<ClipboardObject> _filteredObjectList = new List<ClipboardObject>();

        // ====================================================================

        private string _projectName = "";

        // EditorPrefs Keys
        #region EditorPrefs Const Keys 

        private const string EDITORPREFS_TOOL_NAME = "PicKlipboard_";
        private const string EDITORPREFS_INSID = "_InstanceID_";
        private const string EDITORPREFS_GUID = "_GUID_";
        private const string EDITORPREFS_LOCK = "_Lock_";
        private const string EDITORPREFS_DESCRIPTION = "_Desc_";
        private const string EDITORPREFS_OPT_R_COUNT = "_OptRCount_";
        private const string EDITORPREFS_OPT_S_DESCRIPTION = "_OptSDesc_";

        // Maximum Number to record in the registry.
        private const int MAX_REGISTRY_LIMIT = 1000;
        #endregion

        private bool _isMenuOpen = false;
        private string _searchString = "";

        private AssetFilteringType _filterType = AssetFilteringType.All;


        // Tool Options (Save by EditorPrefs)
        // ====================================================================
        private int _optionRegistryCount = 50;
        private bool _optionShowDescription = false;
        private float _descriptionBarLength = 60f;

        // ====================================================================

        private bool _isDataDirty = false;

        private GUIContent _iconToolSelect;
        private GUIContent _iconToolMove;
        private GUIContent _iconToolTrash;
        private GUIContent _iconToolLock;

        // Toggle Button Styles (Initialized in OnGUI)
        private static GUIStyle _toggleButtonStyleNormal = null;
        private static GUIStyle _toggleButtonStyleToggled = null;

        // GUI Style for Search Cancel Button (Cached in OnGUI)
        private static GUIStyle _searchCancelButtonStyle = null;
        private static GUIStyle _deleteButtonStyle = null;
        public static Vector2 scrollPosition;
        private string _lastActivePathCache = "";

        // ====================================================================


        // Main
        [MenuItem("HAM_DeBugger/PicKlipboard")]
        public static void OpenPicKlipboard()
        {

            PicKlipboardWindow window = GetWindow<PicKlipboardWindow>("PicKlipboard");

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/01.Scripts/Ham_Debugger/PicKlipboard/Editor/Custom_Icons/Logo_PicKlipboard.png");
            window.titleContent = new GUIContent("PicKlipboard", icon);
            window.minSize = new Vector2(200, 150);
        }

        //

        private void OnSelectionChange()
        {
            _lastActivePathCache = string.Empty;
            // Refresh
            Repaint();
        }

        private void OnEnable()
        {
            _projectName = GetProjectName();

            Texture2D customSelectTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/01.Scripts/Ham_Debugger/PicKlipboard/Editor/Custom_Icons/CustomIcon_PicKlipboard_icon_2.png"
            );

            Texture2D customDeleteTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/01.Scripts/Ham_Debugger/PicKlipboard/Editor/Custom_Icons/CustomIcon_PicKlipboard_icon_4.png"
            );
            Texture2D customLockTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/01.Scripts/Ham_Debugger/PicKlipboard/Editor/Custom_Icons/CustomIcon_PicKlipboard_icon_5.png"
            );
            _iconToolSelect = new GUIContent(customSelectTexture);
            _iconToolSelect.tooltip = "Clipboard Object Select";
            _iconToolTrash = new GUIContent(customDeleteTexture);
            _iconToolTrash.tooltip = "Erase Clipboard Object";
            _iconToolLock = new GUIContent(customLockTexture);
            _iconToolLock.tooltip = "Lock Clipboard Object. Excluded from deletion";

            CacheReflectionData();

            LoadEditorPrefs();
            ApplyAutoExpand(false);
            _isDataDirty = false;

            FilterList();
        }

        private void OnDisable()
        {
            if (_isDataDirty)
            {
                SetDisableEditorPrefs();
            }
        }

        // --- EditorPrefs Management ---

        private void SetOptionKey(out string keyRegCount, out string keySDescription)
        {
            keyRegCount = EDITORPREFS_TOOL_NAME + _projectName + EDITORPREFS_OPT_R_COUNT;
            keySDescription = EDITORPREFS_TOOL_NAME + _projectName + EDITORPREFS_OPT_S_DESCRIPTION;
        }

        private void SetIndexedKey(int index, out string keyInsID, out string keyGUID, out string keyLock, out string keyDescription)
        {
            string indexString = index.ToString();

            keyInsID = EDITORPREFS_TOOL_NAME + _projectName + EDITORPREFS_INSID + indexString;
            keyGUID = EDITORPREFS_TOOL_NAME + _projectName + EDITORPREFS_GUID + indexString;
            keyLock = EDITORPREFS_TOOL_NAME + _projectName + EDITORPREFS_LOCK + indexString;
            keyDescription = EDITORPREFS_TOOL_NAME + _projectName + EDITORPREFS_DESCRIPTION + indexString;
        }

        // --- Utility Functions ---


        // Initialize all custom styles ONCE inside OnGUI for safety and efficiency.
        private void InitializeStyles()
        {
            if (_toggleButtonStyleNormal == null)
            {
                _toggleButtonStyleNormal = "Button";
                _toggleButtonStyleToggled = new GUIStyle(_toggleButtonStyleNormal);

                Texture2D buttonTex = new Texture2D(1, 1);
                Color[] buttonColors = new Color[] { new Color(0.5f, 0f, 0f) };

                if (buttonColors.Length > 0)
                {
                    for (int i = 0; i < buttonColors.Length; i++)
                    {
                        buttonColors[i].r *= 0.5f;
                        buttonColors[i].g = 0f;
                        buttonColors[i].b = 0f;
                    }
                }

                buttonTex.SetPixels(buttonColors);
                buttonTex.Apply();
                _toggleButtonStyleToggled.normal.background = buttonTex;
            }

            // Cache Search Cancel Button Style here to avoid continuous lookups and potential null issues.
            if (_searchCancelButtonStyle == null)
            {
                _searchCancelButtonStyle = GUI.skin.FindStyle("ToolbarSeachCancelButton");
            }

            if (_deleteButtonStyle == null)
            {
                _deleteButtonStyle = new GUIStyle(GUI.skin.button);

                Texture2D tex = new Texture2D(1, 1);
                Color redColor = new Color(0.3f, 0.2f, 0.2f);

                tex.SetPixel(0, 0, redColor);
                tex.Apply();

                _deleteButtonStyle.normal.background = tex;
                _deleteButtonStyle.hover.background = tex;
                _deleteButtonStyle.active.background = tex;

                _deleteButtonStyle.normal.textColor = Color.white;
                _deleteButtonStyle.hover.textColor = Color.white;
                _deleteButtonStyle.active.textColor = Color.white;
            }
        }


        private static System.Type s_ProjectBrowserType;
        private static FieldInfo s_LastProjectBrowserField;
        private static FieldInfo s_ProjectBrowserViewModeField;
        private static MethodInfo s_GetActiveFolderPathMethod;

        private static void CacheReflectionData()
        {
            if (s_ProjectBrowserType != null) return;

            s_ProjectBrowserType = System.Type.GetType("UnityEditor.ProjectBrowser,UnityEditor");
            if (s_ProjectBrowserType == null) return;

            s_LastProjectBrowserField = s_ProjectBrowserType.GetField("s_LastInteractedProjectBrowser", BindingFlags.Static | BindingFlags.Public);
            s_ProjectBrowserViewModeField = s_ProjectBrowserType.GetField("m_ViewMode", BindingFlags.Instance | BindingFlags.NonPublic);
            s_GetActiveFolderPathMethod = s_ProjectBrowserType.GetMethod("GetActiveFolderPath", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private string GetLastActivePath()
        {
            if (!string.IsNullOrEmpty(_lastActivePathCache))
            {
                return _lastActivePathCache;
            }

            // Verification Reflection CacheDatas
            CacheReflectionData();

            if (s_ProjectBrowserType == null || s_LastProjectBrowserField == null || s_ProjectBrowserViewModeField == null) return "";

            object lastProjectBrowserInstance = s_LastProjectBrowserField.GetValue(null);
            if (lastProjectBrowserInstance == null) return "";

            int viewMode = (int)s_ProjectBrowserViewModeField.GetValue(lastProjectBrowserInstance);

            string path;
            if (viewMode == 1) // Two column
            {
                if (s_GetActiveFolderPathMethod == null) return "";
                path = (string)s_GetActiveFolderPathMethod.Invoke(lastProjectBrowserInstance, new object[] { });
            }
            else // One column
            {
                if (Selection.activeObject != null && AssetDatabase.Contains(Selection.activeObject))
                {
                    path = AssetDatabase.GetAssetPath(Selection.activeObject);
                    if (!Directory.Exists(path))
                    {
                        path = Path.GetDirectoryName(path);
                    }
                }
                else
                {
                    return "";
                }
            }

            _lastActivePathCache = path;
            return path;
        }

        private string GetProjectName()
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Application.dataPath);
            DirectoryInfo parentDirectory = directoryInfo.Parent;
            if (parentDirectory != null)
            {
                return parentDirectory.Name;
            }
            return "UnknownProject";
        }


        private bool ApplyAutoExpand(bool useForceSave = false)
        {
            bool isChanged = false;
            if (objectList.Count == 0)
            {
                objectList.Add(new ClipboardObject(null, false, ""));
                isChanged = true;
            }

            int endIndex = objectList.Count - 1;

            if (objectList[endIndex].targetObject != null && objectList.Count < MAX_REGISTRY_LIMIT)
            {
                objectList.Add(new ClipboardObject(null, false, ""));
                isChanged = true;
            }

            if (objectList.Count >= 2)
            {
                if (objectList[endIndex].targetObject == null && objectList[endIndex - 1].targetObject == null)
                {
                    objectList.RemoveAt(endIndex);
                    isChanged = true;
                }
            }

            if (isChanged && useForceSave)
                _isDataDirty = true;

            return isChanged;
        }

        // --- Filtering 
        private void FilterList()
        {
            if (string.IsNullOrEmpty(_searchString) && _filterType == AssetFilteringType.All)
            {
                _filteredObjectList = objectList.ToList();
                return;
            }

            _filteredObjectList = objectList.Where(obj =>
            {
                if (obj.targetObject == null)
                {
                    return string.IsNullOrEmpty(_searchString) && _filterType == AssetFilteringType.All;
                }

                bool typeMatch = false;

                if (_filterType == AssetFilteringType.All)
                {
                    typeMatch = true;
                }
                else
                {
                    System.Type objType = obj.targetObject.GetType();

                    switch (_filterType)
                    {
                        case AssetFilteringType.GameObject:
                            typeMatch = obj.targetObject is GameObject;
                            break;

                        case AssetFilteringType.Folders:
                            string assetPath = AssetDatabase.GetAssetPath(obj.targetObject);
                            typeMatch = !string.IsNullOrEmpty(assetPath) && Directory.Exists(assetPath);
                            break;

                        case AssetFilteringType.Prefab:
                            typeMatch = PrefabUtility.IsPartOfAnyPrefab(obj.targetObject);
                            break;

                        case AssetFilteringType.Script:
                            typeMatch = obj.targetObject is UnityEditor.MonoScript;
                            break;
                        case AssetFilteringType.Texture:
                            typeMatch = obj.targetObject is Texture;
                            break;

                        case AssetFilteringType.Asset:
                            typeMatch = AssetDatabase.Contains(obj.targetObject) && !(obj.targetObject is GameObject);
                            break;

                        default:
                            typeMatch = _filterType.ToString() == objType.Name;
                            break;
                    }
                }

                if (!typeMatch) return false;

                if (!string.IsNullOrEmpty(_searchString))
                {
                    string searchLower = _searchString.ToLower();
                    return obj.targetObject.name.ToLower().Contains(searchLower) || obj.description.ToLower().Contains(searchLower);
                }

                return true;

            }).ToList();
        }

        // --- GUI Drawing ---

        private void OnGUI()
        {
            // #1. Initialize Styles 
            InitializeStyles();

            bool listChanged = false;

            float buttonWidth = 27f;
            float buttonHeight = 22f;
            float menuHeight = 20f;

            // --- Handle Keyboard Events (Ctrl+C, Ctrl+V) ---
            Event currentEvent = Event.current;
            bool controlKeyDown = currentEvent.control || currentEvent.command;

            if (currentEvent.type == EventType.KeyDown && controlKeyDown)
            {
                if (currentEvent.keyCode == KeyCode.C)
                {
                    if (Selection.activeObject != null)
                    {
                        int firstEmptyIndex = objectList.FindIndex(o => o.targetObject == null);
                        if (firstEmptyIndex != -1)
                        {
                            objectList[firstEmptyIndex].targetObject = Selection.activeObject;
                            objectList[firstEmptyIndex].description = Selection.activeObject.name;
                            _isDataDirty = true;
                            listChanged = true;
                            currentEvent.Use();
                        }
                    }
                }
                else if (currentEvent.keyCode == KeyCode.V)
                {
                    if (objectList.Count > 0 && objectList[0].targetObject != null)
                    {
                        Selection.activeObject = objectList[0].targetObject;
                        currentEvent.Use();
                    }
                }
            }

            // --- Top Menu Bar ---
            GUILayout.BeginHorizontal();
            {
                GUIContent menuButton = _isMenuOpen ? new GUIContent("◀", "Settings") : new GUIContent("...", "Settings");

                if (GUILayout.Button(menuButton, GUILayout.Width(30f), GUILayout.Height(menuHeight)))
                {
                    _isMenuOpen = !_isMenuOpen;
                }

                if (GUILayout.Button("Clear All", GUILayout.Height(menuHeight)))
                {
                    for (int i = 0; i < objectList.Count; i++)
                    {
                        if (!objectList[i].isLocked)
                            objectList[i] = new ClipboardObject(null, false, "");
                    }
                    _isDataDirty = true;
                    listChanged = true;
                }

                if (GUILayout.Button(new GUIContent("▼", "Clipboard Shift Down"), GUILayout.Height(menuHeight)))
                {
                    objectList.Insert(0, new ClipboardObject(null, false, ""));
                    _isDataDirty = true;
                    listChanged = true;
                }

                if (GUILayout.Button(new GUIContent("▲", "Clipboard Shift Up"), GUILayout.Height(menuHeight)))
                {
                    if (objectList.Count > 1 && !objectList[0].isLocked)
                    {
                        objectList.RemoveAt(0);
                        objectList.Add(new ClipboardObject(null, false, ""));
                        listChanged = true;
                        _isDataDirty = true;
                    }
                }
            }
            GUILayout.EndHorizontal();

            // --- Settings / Options ---
            if (_isMenuOpen)
            {
                EditorGUI.BeginChangeCheck();
                {
                    EditorGUI.indentLevel++;
                    _optionRegistryCount = EditorGUILayout.IntField(new GUIContent("Registry Size", "The Maximum number of items in EditorPrefs"), _optionRegistryCount);
                    _optionRegistryCount = Mathf.Clamp(_optionRegistryCount, 1, MAX_REGISTRY_LIMIT);
                    _optionShowDescription = EditorGUILayout.Toggle(new GUIContent("Description", "Show Description field in Clipboard Item"), _optionShowDescription);
                    if (_optionShowDescription)
                    {

                        EditorGUILayout.LabelField(new GUIContent("Description Bar Length"));
                        _descriptionBarLength = EditorGUILayout.Slider(_descriptionBarLength, 50f, 250f);
                    }
                    //_descriptionBarLength = EditorGUILayout.IntField(new GUIContent("Description Bar Length", "test"), _descriptionBarLength);
                    EditorGUI.indentLevel--;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    _isDataDirty = true;
                    listChanged = true;
                }
            }

            // --- Filtering and Search Bar ---
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                EditorGUI.BeginChangeCheck();
                _filterType = (AssetFilteringType)EditorGUILayout.EnumPopup(_filterType, EditorStyles.toolbarDropDown, GUILayout.Width(80));

                if (EditorGUI.EndChangeCheck())
                {
                    FilterList();
                }

                EditorGUI.BeginChangeCheck();
                _searchString = GUILayout.TextField(_searchString, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                {
                    FilterList();
                }

                // The cancel Search button is always Called Unconditionally. so that it takes up the same space, but only functions when there is a search term.
                if (GUILayout.Button("Cancel Search", _searchCancelButtonStyle ?? GUI.skin.button, GUILayout.Width(130f))) // Fallback to GUI.skin.button if style is null
                {
                    if (!string.IsNullOrEmpty(_searchString))
                    {
                        _searchString = "";
                        FilterList();
                    }
                }
            }
            GUILayout.EndHorizontal(); // End of Search Bar

            if (ApplyAutoExpand(false))
            {
                _isDataDirty = true;
                listChanged = true;
            }

            if (listChanged)
            {
                FilterList();
                ApplyAutoExpand(true);
            }


            #region ScrollDraw

            string newPath = GetLastActivePath();
            // --- Draw List ---
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < _filteredObjectList.Count; i++)
            {
                ClipboardObject currentItem = _filteredObjectList[i];
                int originalListIndex = objectList.IndexOf(currentItem);

                GUILayout.BeginHorizontal(); // Start Row Layout
                {


                    EditorGUI.BeginChangeCheck();
                    currentItem.targetObject = EditorGUILayout.ObjectField(currentItem.targetObject, typeof(Object), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (originalListIndex != -1)
                        {
                            objectList[originalListIndex] = currentItem;
                        }
                        _isDataDirty = true;
                        listChanged = true;
                        FilterList();
                    }

                    bool isSelectedItem = currentItem.targetObject != null;

                    if (isSelectedItem)
                    {
                        if (_optionShowDescription)
                        {
                            EditorGUI.BeginChangeCheck();
                            currentItem.description = EditorGUILayout.TextField(currentItem.description, GUILayout.Width(_descriptionBarLength));
                            if (EditorGUI.EndChangeCheck())
                            {
                                _isDataDirty = true;
                            }
                        }

                        // Select
                        if (GUILayout.Button(_iconToolSelect, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                        {
                            Selection.activeObject = currentItem.targetObject;
                        }

                        if (!AssetDatabase.Contains(currentItem.targetObject) || currentItem.isLocked || newPath == "")
                            GUI.enabled = false;

                        GUI.enabled = true;
                    }

                    if (originalListIndex != -1)
                    {
                        // TRASH BUTTON
                        if (currentItem.isLocked)
                            GUI.enabled = false;

                        if (GUILayout.Button(_iconToolTrash, _deleteButtonStyle, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                        {
                            _isDataDirty = true;

                            if (controlKeyDown)
                            {
                                objectList.RemoveAt(originalListIndex);
                                listChanged = true;
                            }
                            else
                            {
                                objectList[originalListIndex].targetObject = null;
                                objectList[originalListIndex].description = string.Empty;
                            }

                            FilterList();
                        }

                        GUI.enabled = true;
                        // LOCK BUTTON
                        if (GUILayout.Button(_iconToolLock, currentItem.isLocked ? _toggleButtonStyleToggled : _toggleButtonStyleNormal, GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight)))
                        {
                            currentItem.isLocked = !currentItem.isLocked;
                            if (!isSelectedItem)
                                currentItem.isLocked = false;

                            _isDataDirty = true;

                            objectList[originalListIndex].isLocked = currentItem.isLocked;
                        }



                    }
                }
                GUILayout.EndHorizontal(); // End Row Layout
            }
            EditorGUILayout.EndScrollView();
            #endregion


            if (_isDataDirty)
            {
                Repaint();
            }
        }

        #region EditorPrefs Manage

        private void LoadEditorPrefs()
        {
            string keyRegCount;
            string keySDescription;
            SetOptionKey(out keyRegCount, out keySDescription);

            if (EditorPrefs.HasKey(keyRegCount))
            {
                _optionRegistryCount = EditorPrefs.GetInt(keyRegCount);
            }
            if (EditorPrefs.HasKey(keySDescription))
            {
                _optionShowDescription = EditorPrefs.GetBool(keySDescription);
            }

            objectList.Clear();
            string description;

            for (int i = 0; i < _optionRegistryCount; i++)
            {
                string keyInstanceID;
                string keyGUID;
                string keyLock;
                string keyDescription;
                SetIndexedKey(i, out keyInstanceID, out keyGUID, out keyLock, out keyDescription);

                if (!EditorPrefs.HasKey(keyInstanceID))
                    break;

                int instanceId = EditorPrefs.GetInt(keyInstanceID);
                string guid = EditorPrefs.GetString(keyGUID);

                if (instanceId == 0 && guid == string.Empty)
                {
                    objectList.Add(new ClipboardObject(null, false, ""));
                }
                else
                {
                    bool locked = EditorPrefs.GetBool(keyLock);
                    description = EditorPrefs.GetString(keyDescription);
                    Object idObject;
                    if (guid == string.Empty) // Scene object
                    {
                        idObject = EditorUtility.InstanceIDToObject(instanceId);
                    }
                    else // Project asset
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        idObject = AssetDatabase.LoadAssetAtPath<Object>(path);
                    }

                    objectList.Add(new ClipboardObject(idObject, locked, description));
                }
            }
        }

        private void SaveEditorPrefs()
        {
            if (objectList.Count == 0)
                return;

            string keyRegCount;
            string keySDescription;

            SetOptionKey(out keyRegCount, out keySDescription);

            EditorPrefs.SetInt(keyRegCount, _optionRegistryCount);
            EditorPrefs.SetBool(keySDescription, _optionShowDescription);
            int objectAmount = Mathf.Min(_optionRegistryCount, objectList.Count);

            for (int i = 0; i < objectAmount; i++)
            {

                string keyInstanceID;
                string keyGUID;
                string keyLock;
                string keyDescription;

                SetIndexedKey(i, out keyInstanceID, out keyGUID, out keyLock, out keyDescription);

                if (objectList[i].targetObject != null)
                {
                    if (AssetDatabase.Contains(objectList[i].targetObject)) // Project asset
                    {
                        EditorPrefs.SetInt(keyInstanceID, 0);
                        string guid;
                        long file;
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(objectList[i].targetObject, out guid, out file))
                        {
                            EditorPrefs.SetString(keyGUID, guid);
                        }
                        else
                        {
                            EditorPrefs.SetString(keyGUID, "");
                        }
                    }
                    else // Scene asset
                    {
                        EditorPrefs.SetInt(keyInstanceID, objectList[i].targetObject.GetInstanceID());
                        EditorPrefs.SetString(keyGUID, "");
                    }
                    EditorPrefs.SetBool(keyLock, objectList[i].isLocked);
                    EditorPrefs.SetString(keyDescription, objectList[i].description);
                }
                else
                {
                    // Save Empty slot
                    EditorPrefs.SetInt(keyInstanceID, 0);
                    EditorPrefs.SetString(keyGUID, "");
                    EditorPrefs.SetBool(keyLock, false);
                    EditorPrefs.SetString(keyDescription, "");
                }
            }
        }

        private void SetDisableEditorPrefs()
        {
            ClearEditorPrefs();
            SaveEditorPrefs();
            _isDataDirty = false; // Save complete
        }

        private void ClearEditorPrefs()
        {
            string keyRegCount;
            string keySDescription;
            SetOptionKey(out keyRegCount, out keySDescription);

            EditorPrefs.DeleteKey(keyRegCount);
            EditorPrefs.DeleteKey(keySDescription);
            for (int i = 0; i < MAX_REGISTRY_LIMIT; i++)
            {
                string keyInstanceID;
                string keyGUID;
                string keyLock;
                string keyDescription;
                SetIndexedKey(i, out keyInstanceID, out keyGUID, out keyLock, out keyDescription);
                if (!EditorPrefs.HasKey(keyInstanceID))
                    break;

                EditorPrefs.DeleteKey(keyInstanceID);
                EditorPrefs.DeleteKey(keyGUID);
                EditorPrefs.DeleteKey(keyLock);
                EditorPrefs.DeleteKey(keyDescription);
            }
        }

        #endregion

    }

}