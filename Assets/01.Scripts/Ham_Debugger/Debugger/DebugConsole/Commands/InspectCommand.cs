#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace HAM_DeBugger.Core.Debugging.DebugConsole
{
    [ConsoleCommand("inspect")]
    public class InspectCommand : GeneralCommand
    {
        public InspectCommand() : base(
            "inspect",
            "Open a NEW Inspector window for object",
            "<objectName> [index]"
        )
        { }

        protected override bool Execute(string[] args, out string errorMessage)
        {
            if (args == null || args.Length == 0)
            {
                errorMessage = "Usage : inspect <objectName> [index]";
                return false;
            }

            string targetName = args[0];

            // Collect All Object have same Name
            List<GameObject> found = new List<GameObject>();
            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>(true); // inactive \

            foreach (var obj in allObjects)
            {
                if (obj.name == targetName)
                    found.Add(obj);
            }

            if (found.Count == 0)
            {
                errorMessage = $"Object \"{targetName}\" not found in Hierarchy.";
                return false;
            }

            GameObject target = null;

            if (found.Count > 1)
            {
                if (args.Length < 2)
                {
                    errorMessage =
                        $"Found {found.Count} objects named \"{targetName}\".\n" +
                        $"Specify index: inspect {targetName} <1~{found.Count}>";
                    return false;
                }

                // Paring Index
                if (!int.TryParse(args[1], out int index))
                {
                    errorMessage = $"Index must be a number. Given: \"{args[1]}\"";
                    return false;
                }

                if (index < 1 || index > found.Count)
                {
                    errorMessage =
                        $"Index out of range. Total {found.Count} objects named \"{targetName}\".";
                    return false;
                }

                target = found[index - 1];
            }
            else
            {
                target = found[0];
            }

            Object prevSelection = Selection.activeObject;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);

            var inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            if (inspectorType == null)
            {
                Selection.activeObject = prevSelection;
                errorMessage = "Cannot find InspectorWindow type.";
                return false;
            }

            EditorWindow newInspector =
                ScriptableObject.CreateInstance(inspectorType) as EditorWindow;
            if (newInspector == null)
            {
                Selection.activeObject = prevSelection;
                errorMessage = "Failed to create Inspector window instance.";
                return false;
            }

            newInspector.titleContent = new GUIContent($"Inspector ({target.name})");
            newInspector.Show();
            newInspector.Focus();

            // Reset to origin state
            //Selection.activeObject = prevSelection;

            AddLogLine(
                $"Opened NEW Inspector for <b>{target.name}</b>.\n" +
                $"(Found {found.Count} objects with that name)"
            );

            errorMessage = null;
            return true;
        }
    }
}
#endif
