using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using UI.Controls.Viewport;
using UI.Commands.Undo;

namespace UI.Commands.Editing
{
    /// <summary>
    /// Base class for editing commands that require object selection.
    /// Includes preview support (translated clones) for point-pick workflows.
    /// </summary>
    public abstract class EditCommandBase : CommandBase
    {
        protected List<OpenCADObject>? SelectedObjects;
        protected PointInputHelper? _pointInputHelper;
        protected Point3D? _basePoint;
        protected Point3D? _targetPoint;
        protected CancellationTokenSource? _cancellationTokenSource;
        private bool _needsSelection = false;
        private int _initialSelectionCount = 0;
        protected string _commandName = string.Empty;
        protected bool _preserveOriginal = false;
        protected bool _isRepeatable = false;

        // Preview fields (moved here for reuse)
        private readonly List<OpenCADObject> _previewObjects = new();
        protected PropertyChangedEventHandler? _previewHandler;
        private ViewportControl? _previewViewport;

        // Cached providers captured when preview starts (so awaits won't lose them)
        protected ViewportControl? CachedViewport { get; private set; }
        protected OpenCADDocument? CachedDocument { get; private set; }
        protected UndoRedoManager? CachedUndoManager { get; private set; }

        protected ViewportViewModel? CachedViewModel { get; private set; }

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

        /// <summary>
        /// The message to display when entering selection mode.
        /// Derived classes should set this in their constructor or before Execute.
        /// </summary>
        //protected virtual string SelectObjectsMessage => "Click objects to select them, then press ENTER to continue (or ESC to cancel).";

        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
            var viewport = context.GetActiveViewport();
            var viewModel = viewport?.DataContext as ViewportViewModel;
            if (viewModel != null)
            {
                _pointInputHelper = new PointInputHelper(context, viewModel);
            }
        }


        public override async Task Execute()
        {
            var viewport = Context?.GetActiveViewport();
            if (viewport == null)
            {
                Context?.OutputMessage(OpenCADStrings.NoActiveViewport);
                Cancel();
                return;
            }
            CachedViewport = viewport;

            var viewModel = CachedViewport.DataContext as ViewportViewModel;
            if (viewModel == null)
            {
                Context?.OutputMessage(OpenCADStrings.UnableToAccessViewport);
                Cancel();
                return;
            }

            CachedViewModel = viewModel;

            if (CachedViewModel.SelectedObjects.Count > 0)
            {
                // Pre-selected objects - proceed immediately
                SelectedObjects = new List<OpenCADObject>(CachedViewModel.SelectedObjects);
                _needsSelection = false;
                await OnObjectsSelected();
                // Derived class will call RaiseCommandCompleted when done
            }
            else
            {
                // No selection - prompt user
                _needsSelection = true;
                _initialSelectionCount = 0;
                CurrentPrompt = SelectObjectsPrompt;
                Context?.OutputMessage(SelectObjectsMessage);
                CachedViewModel.SelectionChanged += OnSelectionChanged;
            }
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            var viewport = Context?.GetActiveViewport();
            var viewModel = viewport?.DataContext as ViewportViewModel;
            if (viewModel == null) return;

            int count = viewModel.SelectedObjects.Count;
            if (count != _initialSelectionCount)
            {
                var message = $"Selected {count} object(s). Press ENTER to continue, or continue selecting objects.";
                Context?.OutputMessage(message);
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
                var viewport = Context?.GetActiveViewport();
                var viewModel = viewport?.DataContext as ViewportViewModel;
                if (viewModel == null || viewModel.SelectedObjects.Count == 0)
                {
                    Context?.OutputMessage(NoObjectsMessage);
                    Cancel();
                    return false;
                }

                viewModel.SelectionChanged -= OnSelectionChanged;
                SelectedObjects = new List<OpenCADObject>(viewModel.SelectedObjects);
                _needsSelection = false;

                OnObjectsSelected();
                // Derived class will call RaiseCommandCompleted when done
                return true;
            }

            return false;
        }

        public override void Cancel()
        {
            base.Cancel();

            // Ensure preview stopped and preview objects removed
            StopPreview();

            var viewport = Context?.GetActiveViewport();
            if (viewport != null)
            {
                var viewModel = viewport.DataContext as ViewportViewModel;
                if (viewModel != null)
                {
                    viewModel.SelectionChanged -= OnSelectionChanged;
                    viewModel.ClearSelection();
                }
            }

            CurrentPrompt = string.Empty;
            SelectedObjects = null;
            _pointInputHelper?.Cancel();
            _needsSelection = false;
            _initialSelectionCount = 0;
            _cancellationTokenSource?.Cancel();
            _basePoint = null;
            _targetPoint = null;
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
                // Prompt for base point
                _basePoint = await GetBasePoint();

                if (_basePoint == null)
                {
                    Cancel();
                    return;
                }

                // Start unified preview support provided by EditCommandBase
                StartPreview();

                try
                {
                    do
                    {
                        // Prompt for target point
                        _targetPoint = await GetTargetPoint();

                        if (_targetPoint == null)
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

        #region Preview support (cloning + translation)

        /// <summary>
        /// Start preview mode using the provided base point.
        /// Subscribes to ViewportViewModel.PreviewPoint changes and shows translated clones.
        /// Captures providers (viewport/document/undo manager) to avoid them becoming null after awaits.
        /// </summary>
        protected void StartPreview()
        {
            var viewport = Context?.GetActiveViewport();
            if (viewport == null) return;

            var viewModel = viewport.DataContext as ViewportViewModel;
            if (viewModel == null) return;

            // Cache providers immediately so awaiting user input cannot lose them
            CachedViewport = viewport;
            CachedDocument = Context?.GetDocument();
            CachedUndoManager = Context?.GetUndoRedoManager();
            CachedViewModel = viewModel;

            // Remember viewport used for preview so we can remove clones later
            _previewViewport = viewport;

            // Attach handler to respond to PreviewPoint changes
            _previewHandler = (sender, e) =>
            {
                if (e.PropertyName == nameof(ViewportViewModel.PreviewPoint))
                {
                    var previewPoint = viewModel.PreviewPoint;
                    if (previewPoint != null && _basePoint != null)
                    {
                        _targetPoint = previewPoint;
                        UpdatePreviewObjects(viewport);
                    }
                    else
                    {
                        ClearPreviewObjects(viewport);
                    }
                }
            };

            viewModel.PropertyChanged += _previewHandler;
        }

        /// <summary>
        /// Stop preview mode and remove any preview clones.
        /// Clears cached providers.
        /// </summary>
        protected void StopPreview()
        {
            try
            {
                if (_previewViewport != null)
                {
                    var vm = _previewViewport.DataContext as ViewportViewModel;
                    if (vm != null && _previewHandler != null)
                    {
                        vm.PropertyChanged -= _previewHandler;
                    }
                }
            }
            catch
            {
                // ignore cleanup errors
            }

            if (_previewViewport != null)
                ClearPreviewObjects(_previewViewport);

            _previewHandler = null;
            _previewViewport = null;

            // Clear cached provider references
            CachedViewport = null;
            CachedDocument = null;
            CachedUndoManager = null;
            CachedViewModel = null;

            _basePoint = null;
            _targetPoint = null;
            CommandCompleted();
        }

        /// <summary>
        /// Update preview clones using the given translation vector.
        /// Existing preview clones are removed and replaced.
        /// </summary>
        private void UpdatePreviewObjects(ViewportControl viewport)
        {
            // Clear any existing preview clones first
            ClearPreviewObjects(viewport);

            var document = viewport.Document;
            if (document == null || SelectedObjects == null)
                return;

            foreach (var obj in SelectedObjects)
            {
                var clone = CreateTranslatedClone(obj, GetTransformation(), document);
                if (clone != null)
                {
                    _previewObjects.Add(clone);
                    viewport.AddObject(clone);
                }
            }

            viewport.Refresh();
        }

        /// <summary>
        /// Remove preview clones previously added to viewport/document.
        /// </summary>
        protected void ClearPreviewObjects(ViewportControl viewport)
        {
            if (_previewObjects.Count == 0)
                return;

            foreach (var p in _previewObjects.ToList())
            {
                try
                {
                    viewport.RemoveObject(p);
                }
                catch
                {
                    // Swallow errors during preview removal
                }
            }
            _previewObjects.Clear();
            viewport.Refresh();
        }
        
        /// <summary>
        /// Remove preview clones previously added to the current preview viewport,
        /// but keep preview mode active (do not unsubscribe handlers or clear cached providers).
        /// Useful when a command wants to commit a copy/move while continuing point picking.
        /// </summary>
        protected void ClearPreviewClones()
        {
            try
            {
                if (_previewViewport != null)
                    ClearPreviewObjects(_previewViewport);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Create a translated clone for a given source object.
        /// Default implementation supports Line; override to support more types.
        /// </summary>
        protected virtual OpenCADObject? CreateTranslatedClone(OpenCADObject source, Matrix4D translation, OpenCADDocument document)
        {
            if (source is GeometryBase geom)
            {
                var clone = geom.Clone(document) as GeometryBase;
                clone?.Transform(translation);
                return clone;
            }

            // Default: unsupported geometry -> no preview clone
            return null;
        }

        #endregion

        protected async Task<Point3D?> GetBasePoint()
        {
            // Prompt for base point (anchor for copies)
            CurrentPrompt = BasePointPrompt;
            var basePoint = await _pointInputHelper!.GetPointAsync(
                BasePointPrompt,
                allowLastPoint: false,
                basePoint: null,
                _cancellationTokenSource.Token);

            if (basePoint == null)
            {
                // user cancelled before starting - finish command
                StopPreview();
                return null;
            }

            return basePoint;
        }

        protected async Task<Point3D?> GetTargetPoint()
        {
            // Prompt for target point (destination for copies)
            CurrentPrompt = TargetPointPrompt;
            var targetPoint = await _pointInputHelper.GetPointAsync(
                TargetPointPrompt,
                allowLastPoint: false,
                basePoint: _basePoint,
                _cancellationTokenSource.Token);

            return targetPoint;
        }

        protected void CommandCompleted()
        {
            // Ensure point picking fully disabled
            var viewport = Context?.GetActiveViewport();
            var viewModel = viewport?.DataContext as ViewportViewModel;
            if (viewModel != null && viewModel.IsPointPickingMode)
            {
                viewModel.DisablePointPickingMode();
            }
            RaiseCommandCompleted();
        }
    }
}
