using OpenCAD;
using OpenCAD.Geometry;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("erase", "Erase selected objects (or prompts for selection)", "e")]
    public class EraseCommand : EditCommandBase
    {
        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
            System.Diagnostics.Debug.WriteLine(OpenCADStrings.EraseCommandInitialized);
            _commandName = OpenCADStrings.EraseCommandName;
        }

        protected override async Task OnObjectsSelected()
        {
            if (SelectedObjects == null || SelectedObjects.Count == 0)
            {
                Context?.OutputMessage(NoObjectsMessage);
                return;
            }

            var document = Context?.GetDocument();
            var viewport = Context?.GetActiveViewport();
            var undoManager = Context?.GetUndoRedoManager();

            if (document == null || viewport == null)
            {
                Context?.OutputMessage(UnableToActOnObjectsMissingContext);
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

        protected override Matrix4D GetTransformation()
        {
            throw new NotImplementedException();
        }
    }
}