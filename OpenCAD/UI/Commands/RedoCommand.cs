using UI.Commands.Undo;

namespace UI.Commands
{
    /// <summary>
    /// Command to redo the last undone action
    /// </summary>
    [InputCommand("redo", "Redo the last undone action", "r")]
    public class RedoCommand : CommandBase
    {
        public override void Execute()
        {
            var undoManager = Context?.GetUndoRedoManager();
            
            if (undoManager == null)
            {
                Context?.OutputMessage("Undo manager not available.");
                return;
            }

            if (!undoManager.CanRedo)
            {
                Context?.OutputMessage("Nothing to redo.");
                return;
            }

            var description = undoManager.RedoDescription;
            undoManager.Redo();
            Context?.OutputMessage($"Redo: {description}");
        }
    }
}