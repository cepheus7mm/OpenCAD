using System.ComponentModel;
using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Undo;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("rotate", "Rotate selected objects", "ro")]
    public class RotateCommand : EditCommandBase
    {
        protected new string _commandName = OpenCADStrings.RotateCommandName;

        private readonly List<OpenCADObject> _previewObjects = new();

        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
        }

        protected override async void OnObjectsSelected()
        {
            if (SelectedObjects == null || SelectedObjects.Count == 0)
            {
                Context?.OutputMessage(NoObjectsMessage);
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
                    //System.Diagnostics.Debug.WriteLine("RotateCommand: Clearing existing preview/temp points before base pick");
                    initialViewModel.DisablePreviewMode();
                    initialViewModel.ClearTempPoints();
                }

                // Prompt for rotation center (base point)
                _basePoint = await GetBasePoint();
                if (_basePoint == null)
                {
                    Cancel();
                    return;
                }

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
                CurrentPrompt = TargetPointPrompt;
                _targetPoint = await GetTargetPoint();

                // If user cancelled
                if (_targetPoint == null)
                {
                    // Clean up preview subscription/state before exiting
                    if (viewModel != null && previewHandler != null)
                        viewModel.PropertyChanged -= previewHandler;
                    if (cachedViewport != null)
                        ClearRotationPreview(cachedViewport);
                    Cancel();
                    return;
                }

                // Use shared helper to build rotation matrix; treat invalid as user error
                if (!Matrix4D.TryCreateRotationMatrix(_basePoint!, _targetPoint!, out var rotationMatrix))
                {
                    if (cachedViewport != null)
                        ClearRotationPreview(cachedViewport);
                    if (viewModel != null && previewHandler != null)
                        viewModel.PropertyChanged -= previewHandler;

                    Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                    Cancel();
                    return;
                }

                // Apply rotation (use cached providers)
                if (cachedDocument == null || cachedViewport == null)
                {
                    Context?.OutputMessage(UnableToActOnObjectsMissingContext);
                    Cancel();
                    return;
                }

                if (cachedUndo != null)
                {
                    try
                    {
                        var action = new TransformGeometryAction(
                            SelectedObjects,
                            rotationMatrix,
                            string.Format(OpenCADStrings.UndoRotateObjectsFormat, SelectedObjects.Count)
                        );
                        cachedUndo.ExecuteAction(action);
                        Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsRotatedFormat, SelectedObjects.Count));
                    }
                    catch (InvalidOperationException)
                    {
                        // Non-invertible matrix should be treated as invalid input
                        if (cachedViewport != null)
                            ClearRotationPreview(cachedViewport);
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

                CommandCompleted();
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

            // Use shared helper to construct the rotation matrix for preview; bail out if invalid
            if (!Matrix4D.TryCreateRotationMatrix(_basePoint, previewPoint, out var matrix))
                return;

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
        /// Create a transformed clone using a 4x4 double-precision matrix.  Implemented for Line; extend for other types.
        /// </summary>
        private OpenCADObject? CreateTransformedClone(OpenCADObject source, Matrix4D matrix, OpenCADDocument document)
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