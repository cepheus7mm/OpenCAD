using OpenCAD;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("erase", "Erase selected objects (or prompts for selection)", "e")]
    public class EraseCommand : EditCommandBase
    {
        protected override string SelectObjectsPrompt => OpenCADStrings.SelectObjectsToErasePrompt;
        protected override string SelectObjectsMessage => OpenCADStrings.SelectObjectsToEraseMessage + "\nClick objects to select them, then press ENTER to erase (or ESC to cancel).";

        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
            System.Diagnostics.Debug.WriteLine(OpenCADStrings.EraseCommandInitialized);
        }

        protected override void OnObjectsSelected()
        {
            if (SelectedObjects == null || SelectedObjects.Count == 0)
            {
                Context?.OutputMessage(OpenCADStrings.NoObjectsToErase);
                return;
            }

            var document = Context?.GetDocument();
            var viewport = Context?.GetActiveViewport();
            var undoManager = Context?.GetUndoRedoManager();

            if (document == null || viewport == null)
            {
                Context?.OutputMessage(OpenCADStrings.UnableToEraseObjectsMissingContext);
                Cancel();
                return;
            }

            if (undoManager != null)
            {
                var action = new RemoveGeometryAction(
                    SelectedObjects,
                    document,
                    viewport,
                    string.Format(OpenCADStrings.UndoEraseObjectsFormat, SelectedObjects.Count)
                );
                undoManager.ExecuteAction(action);
                Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsErasedFormat, SelectedObjects.Count));
            }
            else
            {
                foreach (var obj in SelectedObjects)
                {
                    document.Remove(obj);
                    viewport.RemoveObject(obj);
                }
                Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsErasedNoUndoFormat, SelectedObjects.Count));
            }

            var viewModel = viewport.DataContext as ViewportViewModel;
            viewModel?.ClearSelection();
            viewport.Refresh();
        }
    }
}