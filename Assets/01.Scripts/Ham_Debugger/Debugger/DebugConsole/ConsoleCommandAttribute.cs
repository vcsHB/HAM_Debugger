using System;

namespace HAM_DeBugger.Core.Debugging.DebugConsole
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ConsoleCommandAttribute : Attribute
    {
        // EX: "set/timescale" / "help"
        public string Path { get; }
        public bool Enabled { get; } // TODO : Disable Option

        public ConsoleCommandAttribute(string path, bool enabled = true)
        {
            Path = path?.ToLower();
            Enabled = enabled;
        }
    }

}