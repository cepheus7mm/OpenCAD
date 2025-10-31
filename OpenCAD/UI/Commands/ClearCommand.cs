namespace UI.Commands
{
    /// <summary>
    /// Command to clear command history
    /// </summary>
    [InputCommand("clear", "Clear command history", "cls")]
    public class ClearCommand : CommandBase
    {
        private readonly Action _clearHistory;

        public ClearCommand(Action clearHistory)
        {
            _clearHistory = clearHistory;
        }

        public override void Execute()
        {
            _clearHistory();
            Context?.OutputMessage("Command history cleared.");
        }
    }
}