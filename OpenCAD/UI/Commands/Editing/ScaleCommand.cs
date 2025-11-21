using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("scale", "Scale selected objects", "sc")]
    public class ScaleCommand : EditCommandBase
    {
        protected override string SelectObjectsPrompt => OpenCADStrings.SelectObjectsToScalePrompt;
        protected override string SelectObjectsMessage => OpenCADStrings.SelectObjectsToScaleMessage + "\nClick objects to select them, then press ENTER to scale (or ESC to cancel).";

        private readonly List<OpenCADObject> _previewObjects = new();

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

        protected override async void OnObjectsSelected()
        {
            if (SelectedObjects == null || SelectedObjects.Count == 0)
            {
                Context?.OutputMessage(OpenCADStrings.NoObjectsToScale);
                Cancel();
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            ViewportControl? cachedViewport = null;
            OpenCADDocument? cachedDocument = null;
            UndoRedoManager? cachedUndo = null;
            ViewportViewModel? viewModel = null;
            PropertyChangedEventHandler? previewHandler = null;

            try
            {
                // Clear any prior preview/temp points
                var initialViewport = Context?.GetActiveViewport();
                var initialViewModel = initialViewport?.DataContext as ViewportViewModel;
                if (initialViewModel != null)
                {
                    initialViewModel.DisablePreviewMode();
                    initialViewModel.ClearTempPoints();
                }

                // Prompt for scale center (base point)
                CurrentPrompt = OpenCADStrings.ScaleBasePointPrompt;
                var basePoint = await _pointInputHelper!.GetPointAsync(
                    OpenCADStrings.ScaleBasePointPrompt,
                    allowLastPoint: false,
                    basePoint: null,
                    _cancellationTokenSource.Token);

                if (basePoint == null)
                {
                    Cancel();
                    return;
                }
                _basePoint = basePoint;

                // Cache providers
                cachedViewport = Context?.GetActiveViewport();
                cachedDocument = Context?.GetDocument();
                cachedUndo = Context?.GetUndoRedoManager();
                viewModel = cachedViewport?.DataContext as ViewportViewModel;

                // Add temp base point and subscribe for preview updates
                if (viewModel != null && cachedViewport != null)
                {
                    viewModel.ClearTempPoints();
                    viewModel.AddTempPoint(_basePoint);

                    previewHandler = (s, e) =>
                    {
                        if (e.PropertyName == nameof(ViewportViewModel.PreviewPoint))
                        {
                            var previewPoint = viewModel.PreviewPoint;
                            if (previewPoint != null)
                                UpdateScalePreview(previewPoint, cachedViewport, cachedDocument);
                            else
                                ClearScalePreview(cachedViewport);
                        }
                    };
                    viewModel.PropertyChanged += previewHandler;
                }

                // Ask for target point that defines scale (distance from base -> scale factor)
                CurrentPrompt = OpenCADStrings.ScaleTargetPointPrompt;
                var targetPoint = await _pointInputHelper.GetPointAsync(
                    OpenCADStrings.ScaleTargetPointPrompt,
                    allowLastPoint: false,
                    basePoint: _basePoint,
                    _cancellationTokenSource.Token);

                if (targetPoint == null)
                {
                    // cleanup
                    if (viewModel != null && previewHandler != null)
                        viewModel.PropertyChanged -= previewHandler;
                    if (cachedViewport != null)
                        ClearScalePreview(cachedViewport);
                    Cancel();
                    return;
                }
                _targetPoint = targetPoint;

                // Build scale matrix using shared helper (returns false for invalid / zero scale)
                if (!Matrix4D.TryCreateUniformScaleMatrix(_basePoint!, _targetPoint!, out var finalMatrix))
                {
                    if (cachedViewport != null)
                        ClearScalePreview(cachedViewport);
                    if (viewModel != null && previewHandler != null)
                        viewModel.PropertyChanged -= previewHandler;

                    Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                    Cancel();
                    return;
                }

                if (cachedDocument == null || cachedViewport == null)
                {
                    Context?.OutputMessage(OpenCADStrings.UnableToScaleObjectsMissingContext);
                    Cancel();
                    return;
                }

                if (cachedUndo != null)
                {
                    try
                    {
                        var action = new TransformGeometryAction(
                            SelectedObjects,
                            finalMatrix,
                            string.Format(OpenCADStrings.UndoScaleObjectsFormat, SelectedObjects.Count)
                        );
                        cachedUndo.ExecuteAction(action);
                        Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsScaledFormat, SelectedObjects.Count));
                    }
                    catch (InvalidOperationException)
                    {
                        // If matrix ends up non-invertible despite checks, handle gracefully
                        if (cachedViewport != null)
                            ClearScalePreview(cachedViewport);
                        if (viewModel != null && previewHandler != null)
                            viewModel.PropertyChanged -= previewHandler;

                        Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                        Cancel();
                        return;
                    }
                }
                else
                {
                    foreach (var obj in SelectedObjects)
                    {
                        if (obj is GeometryBase geom)
                        {
                            try { geom.Transform(finalMatrix); }
                            catch (NotImplementedException)
                            {
                                // Fallback: apply translation component if Transform not implemented
                                var tx = finalMatrix.M41;
                                var ty = finalMatrix.M42;
                                var tz = finalMatrix.M43;
                                geom.Move(new Vector3D(tx, ty, tz));
                            }
                        }
                    }
                    Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsScaledNoUndoFormat, SelectedObjects.Count));
                }

                // Clean up preview and subscriptions
                if (cachedViewport != null)
                    ClearScalePreview(cachedViewport);

                if (viewModel != null && previewHandler != null)
                    viewModel.PropertyChanged -= previewHandler;

                // Ensure point picking mode disabled
                var vp = Context?.GetActiveViewport();
                var vm = vp?.DataContext as ViewportViewModel;
                if (vm != null && vm.IsPointPickingMode)
                    vm.DisablePointPickingMode();

                RaiseCommandCompleted();
            }
            catch (OperationCanceledException)
            {
                if (cachedViewport != null)
                    ClearScalePreview(cachedViewport);
                if (viewModel != null && previewHandler != null)
                    viewModel.PropertyChanged -= previewHandler;
                Cancel();
            }
        }

        public override bool ProcessInput(string input)
        {
            if (SelectedObjects == null)
                return base.ProcessInput(input);

            if (_pointInputHelper != null)
                return _pointInputHelper.ProcessKeyboardInput(input);

            return false;
        }

        private void UpdateScalePreview(Point3D previewPoint, ViewportControl viewport, OpenCADDocument? document)
        {
            // Remove previous preview clones
            ClearScalePreview(viewport);

            if (document == null || _basePoint == null)
                return;

            // Use shared helper to construct the matrix for preview; bail out if invalid
            if (!Matrix4D.TryCreateUniformScaleMatrix(_basePoint, previewPoint, out var matrix))
                return;

            foreach (var src in SelectedObjects)
            {
                var clone = CreateScaledClone(src, matrix, document);
                if (clone != null)
                {
                    _previewObjects.Add(clone);
                    viewport.AddObject(clone);
                }
            }

            viewport.Refresh();
        }

        private void ClearScalePreview(ViewportControl viewport)
        {
            if (_previewObjects.Count == 0) return;
            foreach (var p in _previewObjects.ToArray())
            {
                try { viewport.RemoveObject(p); }
                catch { }
            }
            _previewObjects.Clear();
            viewport.Refresh();
        }

        /// <summary>
        /// Create a scaled clone using a 4x4 double-precision matrix. Implemented for Line; extend for other types.
        /// </summary>
        private OpenCADObject? CreateScaledClone(OpenCADObject source, Matrix4D matrix, OpenCADDocument document)
        {
            if (source is Line line)
            {
                var s = matrix.Transform(line.StartPoint);
                var e = matrix.Transform(line.EndPoint);

                var clone = new Line(document, s, e)
                {
                    Color = line.Color,
                    LineType = line.LineType,
                    LineWeight = line.LineWeight
                };
                try { clone.Layer = line.Layer; } catch { /* ignore */ }
                return clone;
            }

            // Add other geometry types here...

            return null;
        }
    }
}