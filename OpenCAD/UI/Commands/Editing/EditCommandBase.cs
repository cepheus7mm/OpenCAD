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

        // Preview fields (moved here for reuse)
        private readonly List<OpenCADObject> _previewObjects = new();
        private PropertyChangedEventHandler? _previewHandler;
        private ViewportControl? _previewViewport;

        // Cached providers captured when preview starts (so awaits won't lose them)
        protected ViewportControl? CachedViewport { get; private set; }
        protected OpenCADDocument? CachedDocument { get; private set; }
        protected UndoRedoManager? CachedUndoManager { get; private set; }

        public override bool IsMultiStep => _needsSelection;
        public override bool RequiresSelection => true;

        /// <summary>
        /// The prompt to display when asking the user to select objects.
        /// Derived classes should set this in their constructor or before Execute.
        /// </summary>
        protected virtual string SelectObjectsPrompt => "Select objects to edit:";

        /// <summary>
        /// The message to display when entering selection mode.
        /// Derived classes should set this in their constructor or before Execute.
        /// </summary>
        protected virtual string SelectObjectsMessage => "Click objects to select them, then press ENTER to continue (or ESC to cancel).";

        public override void Execute()
        {
            var viewport = Context?.GetActiveViewport();
            if (viewport == null)
            {
                Context?.OutputMessage(OpenCADStrings.NoActiveViewport);
                Cancel();
                return;
            }

            var viewModel = viewport.DataContext as ViewportViewModel;
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
                OnObjectsSelected();
                // Derived class will call RaiseCommandCompleted when done
            }
            else
            {
                // No selection - prompt user
                _needsSelection = true;
                _initialSelectionCount = 0;
                CurrentPrompt = SelectObjectsPrompt;
                Context?.OutputMessage(SelectObjectsMessage);
                viewModel.SelectionChanged += OnSelectionChanged;
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
                    Context?.OutputMessage(OpenCADStrings.NoObjectsSelectedCancelled);
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
        /// Derived classes must implement this to perform their specific action.
        /// </summary>
        protected abstract void OnObjectsSelected();

        #region Preview support (cloning + translation)

        /// <summary>
        /// Start preview mode using the provided base point.
        /// Subscribes to ViewportViewModel.PreviewPoint changes and shows translated clones.
        /// Captures providers (viewport/document/undo manager) to avoid them becoming null after awaits.
        /// </summary>
        protected void StartPreview(Point3D basePoint)
        {
            _basePoint = basePoint;

            var viewport = Context?.GetActiveViewport();
            if (viewport == null) return;

            var viewModel = viewport.DataContext as ViewportViewModel;
            if (viewModel == null) return;

            // Cache providers immediately so awaiting user input cannot lose them
            CachedViewport = Context?.GetActiveViewport();
            CachedDocument = Context?.GetDocument();
            CachedUndoManager = Context?.GetUndoRedoManager();

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
                        Vector3D moveVector = previewPoint.AsVector3D() - _basePoint.AsVector3D();
                        UpdatePreviewObjects(moveVector, viewport);
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

            _basePoint = null;
        }

        /// <summary>
        /// Update preview clones using the given translation vector.
        /// Existing preview clones are removed and replaced.
        /// </summary>
        private void UpdatePreviewObjects(Vector3D moveVector, ViewportControl viewport)
        {
            // Clear any existing preview clones first
            ClearPreviewObjects(viewport);

            var document = viewport.Document;
            if (document == null || SelectedObjects == null)
                return;

            foreach (var obj in SelectedObjects)
            {
                var clone = CreateTranslatedClone(obj, moveVector, document);
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
        protected virtual OpenCADObject? CreateTranslatedClone(OpenCADObject source, Vector3D translation, OpenCADDocument document)
        {
            if (source is OpenCAD.Geometry.Line line)
            {
                var start = line.StartPoint + translation;
                var end = line.EndPoint + translation;
                var clone = new OpenCAD.Geometry.Line(document, start, end)
                {
                    Color = line.Color,
                    LineType = line.LineType,
                    LineWeight = line.LineWeight
                };
                try
                {
                    clone.Layer = line.Layer;
                }
                catch
                {
                    // ignore if layer assignment fails
                }
                return clone;
            }

            // Default: unsupported geometry -> no preview clone
            return null;
        }

        #endregion
    }
}