using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HAM_DeBugger.Core.Debugging.DebugConsole
{

    public class HamDebuggerCommandWindow : EditorWindow
    {
        // --- UI state Type
        private enum Tab { Console, Settings }
        private Tab _currentTab = Tab.Console;

        private Vector2 _scrollPosition;
        private string _currentInput = "";
        private LogRegistry _logRegistry = new LogRegistry();
        private List<string> _inputHistory = new List<string>();
        private int _historyIndex = -1;
        private bool _focusAfterAutocomplete = false;
        private bool _enterPressedOnce = false;
        private double _lastEnterTime = 0.0;
        private const double DOUBLE_ENTER_THRESHOLD = 0.4;

        // Control name for text control
        private const string INPUT_CONTROL_NAME = "ConsoleInput";

        private Color _normalColor = Color.white;
        private Color _errorColor = Color.red;
        private Color _suggestionColor = new Color(0.6f, 0.9f, 1f, 1f);

        // Command registry
        private CommandRegistry _commandRegistry;

        // Styles
        private GUIStyle _logTextStyle;
        private GUIStyle _timeStyle;
        private GUIStyle _suggestionStyle;
        private GUIStyle _errorStyle;

        [MenuItem("Tools/DebugMaster")]
        public static void OpenWindow()
        {
            var w = GetWindow<HamDebuggerCommandWindow>("HAM_DeBugger console");
            w.minSize = new Vector2(450, 300);
        }

        private void OnEnable()
        {
            InitializeStyles();
            SetupCommands();
            AddLog($"<color={ColorConstant.MainThemeColor}>[HAM_DeBugger] Console initialized.</color>", false);
        }

        private void InitializeStyles()
        {
            _logTextStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true,
                normal = { textColor = _normalColor }
            };

            _timeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight
            };

            _suggestionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true
            };

            _errorStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                normal = { textColor = _errorColor }
            };
        }


        private void SetupCommands()
        {
            _commandRegistry = new CommandRegistry();

            var types = TypeFinder.GetTypesDerivedFrom<Command>(); // prev: SimpleCommand

            foreach (var t in types)
            {
                var attr = t.GetCustomAttribute<ConsoleCommandAttribute>();
                if (attr == null) continue;
                if (!attr.Enabled) continue;

                var instance = (Command)Activator.CreateInstance(t); // NO PARAM
                instance.InitializeCommand(_logRegistry, _commandRegistry); // TODOMAN.. NO....

                _commandRegistry.RegisterByPath(attr.Path, instance);
            }
        }

        private void OnGUI()
        {
            _logTextStyle.normal.textColor = _normalColor;
            _errorStyle.normal.textColor = _errorColor;
            _suggestionStyle.normal.textColor = _suggestionColor;

            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, new[] { "Console", "Settings" });

            switch (_currentTab)
            {
                case Tab.Console:
                    DrawConsoleTab();
                    break;
                case Tab.Settings:
                    DrawSettingsTab();
                    break;
            }
        }

        // ---------- Console Tab ----------
        private void DrawConsoleTab()
        {
            EditorGUILayout.BeginVertical();

            // Top toolbar: Clear button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Logs", GUILayout.Width(100)))
            {
                _logRegistry.ClearRegistry();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Focus Input", GUILayout.Width(100)))
            {
                EditorGUI.FocusTextInControl(INPUT_CONTROL_NAME);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            // Display logs
            foreach (LogEntry entry in _logRegistry)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

                // Choose style depending on error flag
                GUIStyle styleToUse = entry.isError ? _errorStyle : _logTextStyle;

                // Label for message; limit width so time can sit on the right
                GUILayout.Label(entry.text, styleToUse, GUILayout.ExpandWidth(true));

                GUILayout.FlexibleSpace();

                GUILayout.Label($"[{entry.time}]", _timeStyle, GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();

                // Divider
                var dividerRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1), GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(dividerRect, new Color(0, 0, 0, 0.15f));
                GUILayout.Space(2);
                // Horizontal row: left = message (wrap), flexible space, right = time
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);

            // Suggestion preview (when appropriate)
            var suggestions = _commandRegistry.GetSuggestionsForInput(_currentInput);
            if (suggestions != null && suggestions.Any())
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Suggestions:", GUILayout.Width(80));
                GUILayout.Label(string.Join(", ", suggestions), _suggestionStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            // Input field (multiline to allow newline on single Enter)
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName(INPUT_CONTROL_NAME);
            _currentInput = EditorGUILayout.TextField(_currentInput, GUILayout.Height(40), GUILayout.ExpandWidth(true));
            if (_focusAfterAutocomplete)
            {
                EditorGUI.FocusTextInControl(INPUT_CONTROL_NAME);

                var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                te.cursorIndex = _currentInput.Length;
                te.selectIndex = _currentInput.Length;
            }


            if (GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(60)))
            {
                ExecuteInputCommand();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            // Handle keyboard input & autocomplete (Tab)
            HandleKeyboardEvents();

            EditorGUILayout.EndVertical();
        }

        // ---------- Settings Tab ----------
        private void DrawSettingsTab()
        {
            GUILayout.Label("Colors", EditorStyles.boldLabel);
            _normalColor = EditorGUILayout.ColorField("Normal Text", _normalColor);
            _errorColor = EditorGUILayout.ColorField("Error Text", _errorColor);
            _suggestionColor = EditorGUILayout.ColorField("Suggestion Text", _suggestionColor);

            GUILayout.Space(8);
            GUILayout.Label("Behavior", EditorStyles.boldLabel);
            GUILayout.Label("Enter behavior: single Enter inserts newline, double Enter sends (within 0.4s).", EditorStyles.wordWrappedLabel);

            GUILayout.Space(8);
            if (GUILayout.Button("Reinitialize Commands"))
            {
                SetupCommands();
                AddLog("Commands reinitialized.", false);
            }
        }

        // ---------- Keyboard & Autocomplete ----------
        private void HandleKeyboardEvents()
        {
            Event e = Event.current;

            if (e == null) return;

            // TAB: autocomplete
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab)
            {
                PerformAutocomplete();
                GUI.FocusControl(INPUT_CONTROL_NAME);

                _focusAfterAutocomplete = true;
                Repaint();
            }
            // ENTER handling: if KeyDown Return -> check double press
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
            {
                // If user holds shift or ctrl, allow newline by default - we still handle double-enter
                if (!_enterPressedOnce)
                {
                    _enterPressedOnce = true;
                    _lastEnterTime = EditorApplication.timeSinceStartup;
                }
                else
                {
                    var dt = EditorApplication.timeSinceStartup - _lastEnterTime;
                    if (dt <= DOUBLE_ENTER_THRESHOLD)
                    {
                        // Double enter detected, execute
                        ExecuteInputCommand();
                        _enterPressedOnce = false;
                        e.Use();
                    }
                    else
                    {
                        // Too slow; reset timer to start new double-enter detection
                        _lastEnterTime = EditorApplication.timeSinceStartup;
                    }
                }

                // After a small time, if not double-entered, reset the flag (handled in Update)
            }

            // ↑
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow)
            {
                if (_inputHistory.Count > 0)
                {
                    _historyIndex = Mathf.Clamp(_historyIndex - 1, 0, _inputHistory.Count - 1);
                    _currentInput = _inputHistory[_historyIndex];
                    Repaint();
                }
                e.Use();
            }

            // ↓
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.DownArrow)
            {
                _historyIndex = Mathf.Clamp(_historyIndex + 1, 0, _inputHistory.Count);
                _currentInput = _historyIndex < _inputHistory.Count ? _inputHistory[_historyIndex] : "";
                Repaint();
                e.Use();
            }


            // Reset enter flag if time exceeded
            if (_enterPressedOnce && (EditorApplication.timeSinceStartup - _lastEnterTime) > DOUBLE_ENTER_THRESHOLD)
            {
                _enterPressedOnce = false;
            }

            // Arrow history, etc. can be added here (not implemented now)
        }

        private void PerformAutocomplete()
        {
            var tokens = GetTokens(_currentInput);
            string before = _currentInput;

            if (tokens.Length == 0)
            {
                // suggest first command if available
                var all = _commandRegistry.GetAllNames();
                if (all.Any())
                {
                    _currentInput = all.First();
                }
            }
            else if (tokens.Length == 1)
            {
                // match top-level commands
                var match = _commandRegistry.GetTopLevelMatch(tokens[0]);
                if (!string.IsNullOrEmpty(match))
                {
                    _currentInput = match + (_currentInput.EndsWith(" ") ? "" : " ");
                }
            }
            else if (tokens.Length == 2)
            {
                var top = tokens[0];
                var subMatch = _commandRegistry.GetSubMatch(top, tokens[1]);
                if (!string.IsNullOrEmpty(subMatch))
                {
                    // preserve original spacing and append a space
                    _currentInput = top + " " + subMatch + " ";
                }
            }
            else
            {
                // For deeper tokens, no special handling for now
            }

            // Keep focus on input
            EditorGUI.FocusTextInControl(INPUT_CONTROL_NAME);
            Repaint();
        }

        // ---------- Command Execution ----------
        private void ExecuteInputCommand()
        {
            _currentInput = _currentInput.Replace("\n", "").Trim();
            if (string.IsNullOrWhiteSpace(_currentInput)) return;

            // Log the raw input
            AddLog("▶ " + _currentInput, false);

            // Tokenize preserving quoted strings could be added later; for now simple split
            var tokens = GetTokens(_currentInput);
            if (tokens.Length == 0)
            {
                _currentInput = "";
                return;
            }

            // Try to execute using registry
            try
            {
                bool executed = _commandRegistry.TryExecute(tokens, out string errorMessage);
                if (!executed)
                {
                    AddLog(errorMessage ?? "Unknown command or wrong usage", true);
                }
            }
            catch (Exception ex)
            {
                AddLog($"Exception: {ex.Message}", true);
            }
            _inputHistory.Add(_currentInput);
            _historyIndex = _inputHistory.Count;

            // Clear input and refocus
            _currentInput = "";
            EditorGUI.FocusTextInControl(INPUT_CONTROL_NAME);
            Repaint();

            // Scroll to bottom
            _scrollPosition.y = Mathf.Infinity;
        }

        // ---------- Utilities ----------
        private string[] GetTokens(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new string[0];
            // Naive split: split by spaces; could be improved to support quoted strings
            return text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void AddLog(string message, bool isError, bool hideTimestamp = false)
        {
            _logRegistry.AddLog(message, isError, hideTimestamp);
        }
    }
}