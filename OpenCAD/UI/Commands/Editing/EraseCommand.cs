using OpenCAD;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Undo;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("erase", "Erase selected objects (or prompts for selection)", "e")]
    public class EraseCommand : EditCommandBase
    {
        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
            _commandName = OpenCADStrings.EraseCommandName;
        }

        protected override Matrix4D GetTransformation()
        {
            return Matrix4D.Identity;
        }

        protected override async Task OnObjectsSelected()
        {
            if (SelectedObjects == null || SelectedObjects.Count == 0)
            {
                Context?.OutputMessage(NoObjectsMessage);
                return;
            }

            var document = Context?.GetDocument();
            var undoManager = Context?.GetUndoRedoManager();

            // Capture the viewport reference on UI thread when needed by UI calls
            Context?.PostToUI(() =>
            {
                var viewport = Context.GetActiveViewport();

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
                viewModel?.SelectionManager.ClearSelection();
                viewport.Refresh();
                CommandCompleted();
            });
        }
    }
}