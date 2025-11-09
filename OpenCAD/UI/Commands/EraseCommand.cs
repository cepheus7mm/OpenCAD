using OpenCAD;
using OpenCAD.Geometry;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands
{
    /// <summary>
    /// Command to erase (delete) selected objects
    /// </summary>
    [InputCommand("erase", "Erase selected objects (or prompts for selection)", "e")]
    public class EraseCommand : CommandBase
    {
        private List<OpenCADObject>? _objectsToErase;
        private bool _needsSelection = false;
        private int _initialSelectionCount = 0;

        public override bool IsMultiStep => _needsSelection;
        public override bool RequiresSelection => true;

        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
            System.Diagnostics.Debug.WriteLine(OpenCADStrings.EraseCommandInitialized);
        }

        public override void Execute()
        {
            System.Diagnostics.Debug.WriteLine("=== EraseCommand.Execute() called ===");
            
            var viewport = Context?.GetActiveViewport();
            if (viewport == null)
            {
                System.Diagnostics.Debug.WriteLine("  ERROR: No active viewport");
                Context?.OutputMessage(OpenCADStrings.NoActiveViewport);
                Cancel();
                return;
            }

            var viewModel = viewport.DataContext as ViewportViewModel;
            if (viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("  ERROR: ViewportViewModel is null");
                Context?.OutputMessage(OpenCADStrings.UnableToAccessViewport);
                Cancel();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"  Current selection count: {viewModel.SelectedObjects.Count}");
            System.Diagnostics.Debug.WriteLine($"  Selection mode enabled: {viewModel.IsSelectionMode}");

            // Check for existing selection
            if (viewModel.SelectedObjects.Count > 0)
            {
                // Use the current selection and erase immediately
                _objectsToErase = new List<OpenCADObject>(viewModel.SelectedObjects);
                _needsSelection = false;

                System.Diagnostics.Debug.WriteLine($"  Erasing {_objectsToErase.Count} pre-selected objects immediately");
                Context?.OutputMessage($"Erasing {_objectsToErase.Count} selected object(s)...");
                EraseObjects();
                RaiseCommandCompleted();
            }
            else
            {
                // No selection - prompt user to select objects
                _needsSelection = true;
                _initialSelectionCount = 0;
                
                System.Diagnostics.Debug.WriteLine("  No pre-selected objects - entering selection mode");
                
                CurrentPrompt = OpenCADStrings.SelectObjectsToErasePrompt;
                Context?.OutputMessage(OpenCADStrings.SelectObjectsToEraseMessage);
                Context?.OutputMessage("Click objects to select them, then press ENTER to erase (or ESC to cancel).");

                // Enable selection mode - the ViewportViewModel will handle object picking
                viewport.EnableSelectionMode();
                System.Diagnostics.Debug.WriteLine($"  Selection mode enabled: {viewModel.IsSelectionMode}");
                
                // Subscribe to selection changes to provide feedback
                System.Diagnostics.Debug.WriteLine("  Subscribing to SelectionChanged event");
                viewModel.SelectionChanged += OnSelectionChanged;
            }
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== EraseCommand.OnSelectionChanged() called ===");
            
            var viewport = Context?.GetActiveViewport();
            if (viewport == null)
            {
                System.Diagnostics.Debug.WriteLine("  ERROR: viewport is null");
                return;
            }

            var viewModel = viewport.DataContext as ViewportViewModel;
            if (viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("  ERROR: viewModel is null");
                return;
            }

            int count = viewModel.SelectedObjects.Count;
            System.Diagnostics.Debug.WriteLine($"  Current selection count: {count}, Initial count: {_initialSelectionCount}");
            
            if (count > _initialSelectionCount)
            {
                var message = $"Selected {count} object(s). Press ENTER to erase, or continue selecting objects.";
                Context?.OutputMessage(message);
                System.Diagnostics.Debug.WriteLine($"  Output message: {message}");
                _initialSelectionCount = count;
            }
            else if (count < _initialSelectionCount)
            {
                var message = $"Selected {count} object(s).";
                Context?.OutputMessage(message);
                System.Diagnostics.Debug.WriteLine($"  Output message: {message}");
                _initialSelectionCount = count;
            }
        }

        public override bool ProcessInput(string input)
        {
            System.Diagnostics.Debug.WriteLine($"=== EraseCommand.ProcessInput() called with input: '{input}' ===");
            System.Diagnostics.Debug.WriteLine($"  _needsSelection: {_needsSelection}");
            
            if (!_needsSelection)
            {
                System.Diagnostics.Debug.WriteLine("  Not in selection mode - returning true");
                return true;
            }

            // Check if user pressed Enter to confirm selection
            if (string.IsNullOrWhiteSpace(input))
            {
                System.Diagnostics.Debug.WriteLine("  User pressed ENTER to confirm selection");
                
                var viewport = Context?.GetActiveViewport();
                if (viewport == null)
                {
                    System.Diagnostics.Debug.WriteLine("  ERROR: viewport is null");
                    Cancel();
                    return false;
                }

                var viewModel = viewport.DataContext as ViewportViewModel;
                if (viewModel == null || viewModel.SelectedObjects.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"  ERROR: viewModel={viewModel != null}, count={viewModel?.SelectedObjects.Count ?? 0}");
                    Context?.OutputMessage(OpenCADStrings.NoObjectsSelectedCancelled);
                    Cancel();
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"  Confirmed selection of {viewModel.SelectedObjects.Count} objects");
                
                // Unsubscribe from selection changes
                System.Diagnostics.Debug.WriteLine("  Unsubscribing from SelectionChanged event");
                viewModel.SelectionChanged -= OnSelectionChanged;

                // Store the selection
                _objectsToErase = new List<OpenCADObject>(viewModel.SelectedObjects);

                // Erase the objects
                EraseObjects();
                RaiseCommandCompleted();
                return true;
            }

            System.Diagnostics.Debug.WriteLine("  Input not handled - returning false");
            return false;
        }

        private void EraseObjects()
        {
            System.Diagnostics.Debug.WriteLine("=== EraseCommand.EraseObjects() called ===");
            
            if (_objectsToErase == null || _objectsToErase.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("  No objects to erase");
                Context?.OutputMessage(OpenCADStrings.NoObjectsToErase);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"  Erasing {_objectsToErase.Count} objects");

            var document = Context?.GetDocument();
            var viewport = Context?.GetActiveViewport();
            var undoManager = Context?.GetUndoRedoManager();

            if (document == null || viewport == null)
            {
                System.Diagnostics.Debug.WriteLine($"  ERROR: document={document != null}, viewport={viewport != null}");
                Context?.OutputMessage(OpenCADStrings.UnableToEraseObjectsMissingContext);
                Cancel();
                return;
            }

            // Create an undo action
            if (undoManager != null)
            {
                System.Diagnostics.Debug.WriteLine("  Using undo/redo manager");
                var action = new RemoveGeometryAction(
                    _objectsToErase,
                    document,
                    viewport,
                    string.Format(OpenCADStrings.UndoEraseObjectsFormat, _objectsToErase.Count)
                );

                undoManager.ExecuteAction(action);

                Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsErasedFormat, _objectsToErase.Count));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("  WARNING: No undo manager - using direct removal");
                // Fallback: direct removal without undo support
                foreach (var obj in _objectsToErase)
                {
                    document.Remove(obj);
                    viewport.RemoveObject(obj);
                }

                Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsErasedNoUndoFormat, _objectsToErase.Count));
            }

            // Clear the selection
            var viewModel = viewport.DataContext as ViewportViewModel;
            viewModel?.ClearSelection();

            // Refresh viewport
            viewport.Refresh();
            
            System.Diagnostics.Debug.WriteLine("  Erase operation completed successfully");
        }

        public override void Cancel()
        {
            System.Diagnostics.Debug.WriteLine("=== EraseCommand.Cancel() called ===");
            
            base.Cancel();

            var viewport = Context?.GetActiveViewport();
            if (viewport != null)
            {
                // Unsubscribe from selection changes if we were subscribed
                var viewModel = viewport.DataContext as ViewportViewModel;
                if (viewModel != null)
                {
                    System.Diagnostics.Debug.WriteLine("  Unsubscribing from SelectionChanged event");
                    viewModel.SelectionChanged -= OnSelectionChanged;
                }
                
                // Disable selection mode
                System.Diagnostics.Debug.WriteLine("  Disabling selection mode");
                viewport.DisableSelectionMode();
            }

            CurrentPrompt = string.Empty;
            _objectsToErase = null;
            _needsSelection = false;
            _initialSelectionCount = 0;
            
            System.Diagnostics.Debug.WriteLine("  Cancel completed");
        }
    }
}