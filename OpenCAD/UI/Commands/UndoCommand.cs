using UI.Commands.Undo;

namespace UI.Commands
{
    /// <summary>
    /// Command to undo the last action
    /// </summary>
    [InputCommand("undo", "Undo the last action", "u")]
    public class UndoCommand : CommandBase
    {
        public override Task Execute()
        {
            var undoManager = Context?.GetUndoRedoManager();
            
            if (undoManager == null)
            {
                Context?.OutputMessage("Undo manager not available.");
                return Task.CompletedTask;
            }

            if (!undoManager.CanUndo)
            {
                Context?.OutputMessage("Nothing to undo.");
                return Task.CompletedTask;
            }

            var description = undoManager.UndoDescription;
            undoManager.Undo();
            Context?.OutputMessage($"Undo: {description}");
            return Task.CompletedTask;
        }
    }
}