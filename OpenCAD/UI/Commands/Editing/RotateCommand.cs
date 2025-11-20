using System.ComponentModel;
using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("rotate", "Rotate selected objects", "ro")]
    public class RotateCommand : EditCommandBase
    {
        protected override string SelectObjectsPrompt => OpenCADStrings.SelectObjectsToRotatePrompt;
        protected override string SelectObjectsMessage => OpenCADStrings.SelectObjectsToRotateMessage + "\nClick objects to select them, then press ENTER to rotate (or ESC to cancel).";

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
                Context?.OutputMessage(OpenCADStrings.NoObjectsToRotate);
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
                // Ensure any previous preview state is cleared so the first pick is truly the base point
                var initialViewport = Context?.GetActiveViewport();
                var initialViewModel = initialViewport?.DataContext as ViewportViewModel;
                if (initialViewModel != null)
                {
                    // Clear any temp points / preview that may be left over from other commands
                    System.Diagnostics.Debug.WriteLine("RotateCommand: Clearing existing preview/temp points before base pick");
                    initialViewModel.DisablePreviewMode();
                    initialViewModel.ClearTempPoints();
                }

                // Prompt for rotation center (base point)
                CurrentPrompt = OpenCADStrings.RotateBasePointPrompt;
                var basePoint = await _pointInputHelper!.GetPointAsync(
                    OpenCADStrings.RotateBasePointPrompt,
                    allowLastPoint: false,
                    basePoint: null,
                    _cancellationTokenSource.Token);

                if (basePoint == null)
                {
                    Cancel();
                    return;
                }
                _basePoint = basePoint;

                // Cache providers immediately (guard against await issues)
                cachedViewport = Context?.GetActiveViewport();
                cachedDocument = Context?.GetDocument();
                cachedUndo = Context?.GetUndoRedoManager();
                viewModel = cachedViewport?.DataContext as ViewportViewModel;

                // Now that we have the base point, add it as a temp point and subscribe for preview updates
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
                            {
                                UpdateRotationPreview(previewPoint, cachedViewport, cachedDocument);
                            }
                            else
                            {
                                ClearRotationPreview(cachedViewport);
                            }
                        }
                    };
                    viewModel.PropertyChanged += previewHandler;
                }

                // Ask for a target point that defines the rotation angle (relative to center)
                CurrentPrompt = OpenCADStrings.RotateTargetPointPrompt;
                var targetPoint = await _pointInputHelper.GetPointAsync(
                    OpenCADStrings.RotateTargetPointPrompt,
                    allowLastPoint: false,
                    basePoint: _basePoint,
                    _cancellationTokenSource.Token);

                // If user cancelled
                if (targetPoint == null)
                {
                    // Clean up preview subscription/state before exiting
                    if (viewModel != null && previewHandler != null)
                        viewModel.PropertyChanged -= previewHandler;
                    if (cachedViewport != null)
                        ClearRotationPreview(cachedViewport);
                    Cancel();
                    return;
                }
                _targetPoint = targetPoint;

                // Compute final rotation angle (angle from center to picked target, relative to +X)
                double angleRad = Math.Atan2(_targetPoint.Y - _basePoint.Y, _targetPoint.X - _basePoint.X);

                // Build rotation matrix around base point (Z axis)
                var translateToOrigin = Matrix4x4.CreateTranslation((float)-_basePoint.X, (float)-_basePoint.Y, (float)-_basePoint.Z);
                var rotate = Matrix4x4.CreateRotationZ((float)angleRad);
                var translateBack = Matrix4x4.CreateTranslation((float)_basePoint.X, (float)_basePoint.Y, (float)_basePoint.Z);
                var rotationMatrix = translateToOrigin * rotate * translateBack;

                // Apply rotation (use cached providers)
                if (cachedDocument == null || cachedViewport == null)
                {
                    Context?.OutputMessage(OpenCADStrings.UnableToRotateObjectsMissingContext);
                    Cancel();
                    return;
                }

                if (cachedUndo != null)
                {
                    var action = new TransformGeometryAction(
                        SelectedObjects,
                        rotationMatrix,
                        string.Format(OpenCADStrings.UndoRotateObjectsFormat, SelectedObjects.Count)
                    );
                    cachedUndo.ExecuteAction(action);
                    Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsRotatedFormat, SelectedObjects.Count));
                }
                else
                {
                    foreach (var obj in SelectedObjects)
                    {
                        if (obj is GeometryBase geom)
                        {
                            try { geom.Transform(rotationMatrix); }
                            catch (NotImplementedException)
                            {
                                // fallback to translate by translation component if Transform not implemented
                                geom.Move(new Vector3D(rotationMatrix.M41, rotationMatrix.M42, rotationMatrix.M43));
                            }
                        }
                    }
                    Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsRotatedNoUndoFormat, SelectedObjects.Count));
                }

                // Clean up preview visuals and subscription
                if (cachedViewport != null)
                    ClearRotationPreview(cachedViewport);

                if (viewModel != null && previewHandler != null)
                    viewModel.PropertyChanged -= previewHandler;

                // Ensure point picking mode is disabled
                var vp = Context?.GetActiveViewport();
                var vm = vp?.DataContext as ViewportViewModel;
                if (vm != null && vm.IsPointPickingMode)
                    vm.DisablePointPickingMode();

                RaiseCommandCompleted();
            }
            catch (OperationCanceledException)
            {
                // Cancellation -> remove preview and exit
                if (cachedViewport != null)
                    ClearRotationPreview(cachedViewport);
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

        private void UpdateRotationPreview(Point3D previewPoint, ViewportControl viewport, OpenCADDocument? document)
        {
            // Remove previous preview clones
            ClearRotationPreview(viewport);

            if (document == null || _basePoint == null)
                return;

            // Compute angle from base to preview
            double angleRad = Math.Atan2(previewPoint.Y - _basePoint.Y, previewPoint.X - _basePoint.X);

            // Build rotation matrix (about Z)
            var tToOrigin = Matrix4x4.CreateTranslation((float)-_basePoint.X, (float)-_basePoint.Y, (float)-_basePoint.Z);
            var rot = Matrix4x4.CreateRotationZ((float)angleRad);
            var tBack = Matrix4x4.CreateTranslation((float)_basePoint.X, (float)_basePoint.Y, (float)_basePoint.Z);
            var matrix = tToOrigin * rot * tBack;

            // Create transformed clones for preview
            foreach (var src in SelectedObjects)
            {
                var clone = CreateTransformedClone(src, matrix, document);
                if (clone != null)
                {
                    _previewObjects.Add(clone);
                    viewport.AddObject(clone);
                }
            }

            viewport.Refresh();
        }

        private void ClearRotationPreview(ViewportControl viewport)
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
        /// Create a transformed clone using a 4x4 matrix.  Implemented for Line; extend for other types.
        /// </summary>
        private OpenCADObject? CreateTransformedClone(OpenCADObject source, Matrix4x4 matrix, OpenCADDocument document)
        {
            if (source is Line line)
            {
                var s = Vector3.Transform(new Vector3((float)line.StartPoint.X, (float)line.StartPoint.Y, (float)line.StartPoint.Z), matrix);
                var e = Vector3.Transform(new Vector3((float)line.EndPoint.X, (float)line.EndPoint.Y, (float)line.EndPoint.Z), matrix);

                var clone = new Line(document, new Point3D(s.X, s.Y, s.Z), new Point3D(e.X, e.Y, e.Z))
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