using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;
using UI.Commands.Undo;

namespace UI.Commands.Editing
{
    /// <summary>
    /// Base class for editing commands that require object selection.
    /// Includes preview support (uses shared preview in CommandBase).
    /// </summary>
    public abstract class EditCommandBase : CommandBase
    {
        protected enum InputMode
        {
            ObjectSelection,
            PointInput
        }
        protected InputMode _currentInputMode = InputMode.ObjectSelection;

        private bool _needsSelection = false;
        private int _initialSelectionCount = 0;
        protected string _commandName = string.Empty;
        protected bool _preserveOriginal = false;
        protected bool _isRepeatable = false;

        public string SelectObjectsPrompt => string.Format(OpenCADStrings.SelectObjectsToActOnPrompt, _commandName);

        public string SelectObjectsMessage => string.Format(OpenCADStrings.SelectObjectsToActOnMessage, _commandName);

        public string NoObjectsMessage => OpenCADStrings.NoObjectsSelectedToActOnCancelled;

        public string UnableToActOnObjectsMissingContext => OpenCADStrings.UnableToActOnObjectsMissingContext;

        public string NoObjectsSelectedToActOnCancelled => OpenCADStrings.NoObjectsSelectedToActOnCancelled;

        public string BasePointPrompt => OpenCADStrings.BasePointPrompt;

        public string TargetPointPrompt => OpenCADStrings.TargetPointPrompt;

        public string InvalidPointInput => OpenCADStrings.InvalidPointInput;

        public override bool IsMultiStep => _needsSelection;

        public override bool RequiresSelection => true;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
        }

        public override async Task Execute()
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            if (viewModel == null)
            {
                Context?.OutputMessage(OpenCADStrings.UnableToAccessViewport);
                Cancel();
                return;
            }

            if (viewModel.SelectedObjects.Count > 0)
            {
                // Pre-selected objects - proceed immediately
                SelectedObjects = new List<OpenCADObject>(viewModel.SelectedObjects);
                _needsSelection = false;
                await OnObjectsSelected();
                // Derived class will call RaiseCommandCompleted when done
            }
            else
            {
                // No selection - prompt user
                _needsSelection = true;
                _initialSelectionCount = 0;
                //CurrentPrompt = SelectObjectsPrompt;
                Context?.OutputMessage(SelectObjectsMessage);

                // Ensure subscription happens on UI thread
                Context?.PostToUI(() => viewModel.SelectionChanged += OnSelectionChanged);
            }
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            var viewModel = Context?.GetActiveViewportViewModel();
            if (viewModel == null) 
                return;


            int count = viewModel.SelectedObjects.Count;
            if (count != _initialSelectionCount)
            {
                Context?.OutputMessage($"Selected {count} object(s). Press ENTER to continue, or continue selecting objects.");
                _initialSelectionCount = count;
            }
        }

        public override bool ProcessInput(string input)
        {
            if (!_needsSelection)
                return base.ProcessInput(input);  // Let derived class handle it

            // User pressed ENTER to confirm selection
            if (string.IsNullOrWhiteSpace(input))
            {

                var viewmodel = Context?.GetActiveViewportViewModel();
                if (viewmodel == null || viewmodel.SelectedObjects.Count == 0)
                {
                    Context?.OutputMessage(NoObjectsMessage);
                    Cancel();
                    return false;
                }

                _currentInputMode = InputMode.PointInput;

                // Unsubscribe on UI thread and capture selected objects
                Context?.PostToUI(() => viewmodel.SelectionChanged -= OnSelectionChanged);
                SelectedObjects = new List<OpenCADObject>(viewmodel.SelectedObjects);
                _needsSelection = false;

                Task.Run(async () => { await OnObjectsSelected(); });
                return true;
            }

            return false;
        }

        public override void Cancel()
        {
            base.Cancel();

            // Ensure preview stopped and preview objects removed
            StopPreview();

            Context?.PostToUI(() =>
            {
                var viewmodel = Context.GetActiveViewportViewModel();
                if (viewmodel != null)
                {
                    viewmodel.SelectionChanged -= OnSelectionChanged;
                    viewmodel.ClearSelection();
                }
            });

            CurrentPrompt = string.Empty;
            SelectedObjects = null;
            _needsSelection = false;
            _initialSelectionCount = 0;
            _cancellationTokenSource?.Cancel();
            BasePoint = null;
            TargetPoint = null;
        }

        /// <summary>
        /// Called when objects have been selected and the command should proceed.
        /// </summary>
        protected virtual async Task OnObjectsSelected()
        {
            if (SelectedObjects == null || SelectedObjects.Count == 0)
            {
                Context?.OutputMessage(NoObjectsMessage);
                Cancel();
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Prompt for base point using shared GetPoint on CommandBase
                BasePoint = null;
                var result = await GetPoint(BasePointPrompt, null);

                if (result != null && result.Point is Point3D basePoint)
                {
                    BasePoint = basePoint;
                }
                else
                {
                    Cancel();
                    return;
                }

                // Start unified preview support (now provided by CommandBase)
                StartPreview();

                try
                {
                    do
                    {
                        // Prompt for target point using shared GetPoint on CommandBase
                        result = await GetPoint(TargetPointPrompt);
                        if (result != null && result.Point is Point3D targetPoint)
                        {
                            TargetPoint = targetPoint;
                        }

                        else
                        {
                            Cancel();
                            return;
                        }

                        // Perform the transformation WHILE cached providers are still available
                        TransformSelectedObjects();
                    } while (_isRepeatable);
                }
                finally
                {
                    // Ensure preview is cleaned up if something goes wrong
                    StopPreview();
                } 
            }
            catch (OperationCanceledException)
            {
                // Ensure preview objects removed when cancelled
                StopPreview();
                Cancel();
            }
        }

        protected abstract Matrix4D GetTransformation();

        public bool TryGetTransformation(out Matrix4D transformation)
        {
            if (BasePoint == null || TargetPoint == null)
            {
                transformation = Matrix4D.Identity;
                return false;
            }

            transformation = GetTransformation();
            return true;
        }

        protected virtual void TransformSelectedObjects()
        {
            var transformationMatrix = GetTransformation();

            if (transformationMatrix == Matrix4D.Identity)
            {
                return;
            }

            // Apply rotation (use cached providers)
            if (CachedDocument == null || CachedViewport == null)
            {
                Context?.OutputMessage(UnableToActOnObjectsMissingContext);
                Cancel();
                return;
            }

            List<OpenCADObject> objectsToTransform = SelectedObjects!;
            if (_preserveOriginal)
            {
                // Clone objects before transforming
                var clonedObjects = new List<OpenCADObject>();
                foreach (var obj in SelectedObjects!)
                {
                    var clone = obj.Clone(CachedDocument);
                    if (clone != null)
                    {
                        CachedDocument.Add(clone);
                        clonedObjects.Add(clone);
                    }
                }
                objectsToTransform = clonedObjects;
            }

            if (CachedUndoManager != null)
            {
                try
                {
                    var action = new TransformGeometryAction(
                        objectsToTransform,
                        transformationMatrix,
                        string.Format(OpenCADStrings.UndoTransformedObjectsFormat, objectsToTransform.Count, GetCommandName(capitized: true))
                    );
                    CachedUndoManager.ExecuteAction(action);
                    Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsTransformedFormat, objectsToTransform.Count, GetCommandName(true, true)));
                }
                catch (InvalidOperationException)
                {
                    // Non-invertible matrix should be treated as invalid input
                    if (CachedViewport != null)
                        ClearPreviewObjects(CachedViewport);
                    if (CachedViewModel != null && _previewHandler != null)
                        CachedViewModel.PropertyChanged -= _previewHandler;

                    Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                    Cancel();
                    return;
                }
            }
        }

        private object? GetCommandName(bool capitized = false, bool pastTense = false)
        {
            var commandName = capitized ? char.ToUpper(_commandName[0]) + _commandName.Substring(1) : _commandName;
            if (pastTense)
            {
                // Simple past tense conversion (may not be accurate for all commands)
                if (commandName.EndsWith("e"))
                    commandName += "d";
                else
                    commandName += "ed";
                commandName = commandName.Replace("y", "i");
            }
            return commandName;
        }
    }
}
