using System.Linq;
using UnityEngine;
namespace HAM_DeBugger.Core.Debugging.DebugConsole
{

    public abstract class GeneralCommand : Command
    {
        protected GeneralCommand(string name, string description, string argument = "") : base(name, description, argument) { }

        public override bool TryExecute(string[] tokens, int startIndex, out string errorMessage)
        {
            var args = tokens.Skip(startIndex + 1).ToArray();

            return Execute(args, out errorMessage);
        }

        protected abstract bool Execute(string[] args, out string errorMessage);

    }
}