using UI.Commands.Undo;

namespace UI.Commands
{
    /// <summary>
    /// Command to undo the last action
    /// </summary>
    [InputCommand("undo", "Undo the last action", "u")]
    public class UndoCommand : CommandBase
    {
        public override void Execute()
        {
            var undoManager = Context?.GetUndoRedoManager();
            
            if (undoManager == null)
            {
                Context?.OutputMessage("Undo manager not available.");
                return;
            }

            if (!undoManager.CanUndo)
            {
                Context?.OutputMessage("Nothing to undo.");
                return;
            }

            var description = undoManager.UndoDescription;
            undoManager.Undo();
            Context?.OutputMessage($"Undo: {description}");
        }
    }
}