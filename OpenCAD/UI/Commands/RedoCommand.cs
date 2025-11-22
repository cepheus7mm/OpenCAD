using UI.Commands.Undo;

namespace UI.Commands
{
    /// <summary>
    /// Command to redo the last undone action
    /// </summary>
    [InputCommand("redo", "Redo the last undone action", "r")]
    public class RedoCommand : CommandBase
    {
        public override Task Execute()
        {
            var undoManager = Context?.GetUndoRedoManager();
            
            if (undoManager == null)
            {
                Context?.OutputMessage("Undo manager not available.");
                return Task.CompletedTask;
            }

            if (!undoManager.CanRedo)
            {
                Context?.OutputMessage("Nothing to redo.");
                return Task.CompletedTask;
            }

            var description = undoManager.RedoDescription;
            undoManager.Redo();
            Context?.OutputMessage($"Redo: {description}");
            return Task.CompletedTask;
        }
    }
}