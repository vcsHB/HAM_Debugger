using UnityEngine;

namespace HAM_DeBugger.Core.Debugging.DebugConsole
{
    [ConsoleCommand("set/timescale")]
    public class SetTimeScaleCommand : GeneralCommand
    {
        public SetTimeScaleCommand() : base("timescale", "Setting TimeScale", "<int>") { }


        protected override bool Execute(string[] args, out string errorMessage)
        {
            if (int.TryParse(args[0], out int newTimeScale))
            {
                Time.timeScale = newTimeScale;
                AddLogLine($"TimeScale Changed to <b>{newTimeScale}</b> Successfully.");
                errorMessage = null;
                return true;
            }
            else
            {
                errorMessage = $"Unexpected value Type. >\"{args[0]}\"<";
                return false;
            }
        }
    }


}