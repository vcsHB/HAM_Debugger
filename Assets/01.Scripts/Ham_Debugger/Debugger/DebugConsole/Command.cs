using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HAM_DeBugger.Core.Debugging.DebugConsole
{
    [System.Serializable]
    public class LogEntry
    {
        public string text;
        public bool isError;
        public string time;
    }

    // Command system
    public abstract class Command
    {
        public string Name { get; }
        public string Description { get; }
        public string Argument {get;}
        protected LogRegistry _logRegistry;
        protected CommandRegistry _commandRegistry;

        protected Command(string name, string description, string argument = "")
        {
            Name = name.ToLower();
            Description = description;
            Argument = argument;
        }

        public abstract bool TryExecute(string[] tokens, int startIndex, out string errorMessage);
        public virtual IEnumerable<string> GetAutoCompleteCandidates(string[] tokens, int index) => Enumerable.Empty<string>();
        public virtual IEnumerable<string> GetAllNames() => new[] { Name };

        protected void AddLogLine(string content, int size, string colorValue)
        {
            AddLogLine($"<size={size}><color={colorValue}>{content}</color></size>");
        }

        protected void AddLogLine(string content)
        {
            _logRegistry.AddLog(content, false);
        }

        protected void AddErrorLine(string content)
        {
            _logRegistry.AddLog(content, true);
        }

        public bool InitializeCommand(LogRegistry registry, CommandRegistry commandRegistry)
        {
            _logRegistry = registry;
            _commandRegistry = commandRegistry;
            Initialize();
            return true;
        }

        protected virtual void Initialize()
        {

        }
    }
}