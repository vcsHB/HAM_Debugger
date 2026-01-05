using System.Collections.Generic;
using System.Linq;

namespace HAM_DeBugger.Core.Debugging.DebugConsole
{

    // Group command that contains subcommands (ex. set timescale)
    public abstract class GroupCommand : Command
    {
        private Dictionary<string, Command> subCommands = new Dictionary<string, Command>();

        public GroupCommand(string name, string description) : base(name, description)
        {

        }

        public void RegisterSub(Command sub)
        {
            subCommands[sub.Name] = sub;
        }

        public Dictionary<string, Command> GetAllSubCommands()
        {
            return subCommands;
        }

        protected Command GetSubByName(string name)
        {
            subCommands.TryGetValue(name, out var cmd);
            return cmd;
        }

        public Command GetSubCommand(string name) => GetSubByName(name);

        public override IEnumerable<string> GetAllNames()
        {
            return new[] { Name }.Concat(subCommands.Keys.Select(k => $"{Name} {k}"));
        }
        
        public override bool TryExecute(string[] tokens, int startIndex, out string errorMessage)
        {
            errorMessage = null;

            // If only group name provided, list available subcommands
            if (tokens.Length <= startIndex + 1)
            {
                errorMessage = $"Available subcommands for '{Name}': {string.Join(", ", subCommands.Keys)}";
                return false;
            }

            var subKey = tokens[startIndex + 1].ToLower();
            if (!subCommands.TryGetValue(subKey, out var subCmd))
            {
                errorMessage = $"Unknown subcommand: {subKey} (for {Name}). Available: {string.Join(", ", subCommands.Keys)}";
                return false;
            }

            // Execute subcommand starting at startIndex+1
            return subCmd.TryExecute(tokens, startIndex + 1, out errorMessage);
        }

        public override IEnumerable<string> GetAutoCompleteCandidates(string[] tokens, int index)
        {
            // If index points to subcommand token, propose subs
            if (index == 1)
            {
                return subCommands.Keys;
            }
            return Enumerable.Empty<string>();
        }
    }
}