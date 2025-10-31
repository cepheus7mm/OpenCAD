using System.Linq;

namespace UI.Commands
{
    /// <summary>
    /// Command to display help information
    /// </summary>
    [InputCommand("help", "Show this help message", "?")]
    public class HelpCommand : CommandBase
    {
        private readonly Dictionary<string, (string description, string[] aliases)> _commandRegistry;

        public HelpCommand()
        {
            _commandRegistry = new Dictionary<string, (string, string[])>();
        }

        /// <summary>
        /// Set the command registry for help display
        /// </summary>
        public void SetCommandRegistry(Dictionary<string, (string description, string[] aliases)> registry)
        {
            _commandRegistry.Clear();
            foreach (var kvp in registry)
            {
                _commandRegistry[kvp.Key] = kvp.Value;
            }
        }

        public override void Execute()
        {
            Context?.OutputMessage("Available commands:");

            foreach (var cmd in _commandRegistry.OrderBy(c => c.Key))
            {
                var aliasText = cmd.Value.aliases.Length > 0
                    ? $" (or {string.Join(", ", cmd.Value.aliases)})"
                    : "";
                Context?.OutputMessage($"  {cmd.Key,-20}{aliasText,-15} - {cmd.Value.description}");
            }

            Context?.OutputMessage("");
            Context?.OutputMessage("Point format: x y z (e.g., 0 0 0 or 5.5 10 0)");
            Context?.OutputMessage("Press Enter at start point prompt to use last entered point");
            Context?.OutputMessage("Press ESC to cancel current command");
        }
    }
}