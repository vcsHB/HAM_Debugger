using System.Collections.Generic;

namespace HAM_DeBugger.Core.Debugging.DebugConsole
{
    [ConsoleCommand("help")]
    public class HelpCommand : GeneralCommand
    {
        public HelpCommand() : base("help", "Display DebugMaster Helper") { }

        protected override bool Execute(string[] args, out string errorMessage)
        {
            Dictionary<string, Command> commands = _commandRegistry.GetAllCommands();

            AddLogLine($"<b>=================================</b>", 20, ColorConstant.SubThemeColor);
            //AddLogLine($"<size=30><color={ColorConstant.MainThemeColor}><b>[Debug Master]</b> Helper</color></size>\n\n<color=#40c2ac>Debug master command list</color>\n");
            AddLogLine($"<b>[Debug Master]</b> Helper</color></size>\n\n<color=#40c2ac>Debug master command list</color>", 30, ColorConstant.MainThemeColor);
            foreach (var kv in commands)
            {
                PrintCommandRecursive(kv.Key, kv.Value, 0);
            }
            AddLogLine($"<b>=================================</b>", 20, ColorConstant.SubThemeColor);
            errorMessage = null;
            return true;
        }

        private void PrintCommandRecursive(string path, Command command, int indent)
        {
            string indentStr = new string('\t', indent);

            AddLogLine($"{indentStr}<size=17>▶ <b>{command.Name}</b> : <color={ColorConstant.MainThemeColor}>{path.Replace("/", " ")} {command.Argument}</color></size>");
            AddLogLine($"{indentStr}<color={ColorConstant.SubThemeColor}>{command.Description}</color>");

            if (command is GroupCommand group)
            {
                foreach (var kv in group.GetAllSubCommands())
                {
                    string subPath = $"{path}/{kv.Key}";
                    PrintCommandRecursive(subPath, kv.Value, indent + 1);
                }
            }
        }
    }
}
