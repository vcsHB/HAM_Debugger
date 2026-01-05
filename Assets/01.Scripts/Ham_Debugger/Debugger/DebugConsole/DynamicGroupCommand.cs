namespace HAM_DeBugger.Core.Debugging.DebugConsole
{
    public class DynamicGroupCommand : GroupCommand
    {
        public DynamicGroupCommand(string name, string description) : base(name, description) { }

        protected override void Initialize() { /* no-op */ }

    }

}