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
    [InputCommand("move", "Move selected objects", "m")]
    public class MoveCommand : EditCommandBase
    {
        protected override string SelectObjectsPrompt => OpenCADStrings.SelectObjectsToCopyPrompt;
        protected override string SelectObjectsMessage => OpenCADStrings.SelectObjectsToCopyMessage + "\nClick objects to select them, then press ENTER to move (or ESC to cancel).";

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
                Context?.OutputMessage(OpenCADStrings.NoObjectsToCopy);
                Cancel();
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Prompt for base point
                CurrentPrompt = OpenCADStrings.CopyBasePointPrompt;
                var basePoint = await _pointInputHelper!.GetPointAsync(
                    OpenCADStrings.CopyBasePointPrompt,
                    allowLastPoint: false,
                    basePoint: null,
                    _cancellationTokenSource.Token);

                if (basePoint == null)
                {
                    Cancel();
                    return;
                }
                _basePoint = basePoint;

                // Start unified preview support provided by EditCommandBase
                StartPreview(_basePoint);

                try
                {
                    // Prompt for target point (PointInputHelper will enable preview mode / rubberband)
                    CurrentPrompt = OpenCADStrings.CopyTargetPointPrompt;
                    var targetPoint = await _pointInputHelper.GetPointAsync(
                        OpenCADStrings.CopyTargetPointPrompt,
                        allowLastPoint: false,
                        basePoint: _basePoint,
                        _cancellationTokenSource.Token);

                    if (targetPoint == null)
                    {
                        Cancel();
                        return;
                    }
                    _targetPoint = targetPoint;

                    // Perform the move WHILE cached providers are still available
                    MoveSelectedObjects();

                    // Stop preview after performing the real move (clears preview clones and cached refs)
                    StopPreview();

                    // Ensure point picking mode is fully disabled before completing
                    var viewport = Context?.GetActiveViewport();
                    var viewModel = viewport?.DataContext as ViewportViewModel;
                    if (viewModel != null && viewModel.IsPointPickingMode)
                    {
                        System.Diagnostics.Debug.WriteLine("MoveCommand: Manually disabling point picking mode before completion");
                        viewModel.DisablePointPickingMode();
                    }

                    // NOW raise command completed (after async work is done AND point picking is disabled)
                    System.Diagnostics.Debug.WriteLine("MoveCommand: Raising CommandCompleted");
                    RaiseCommandCompleted();
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

        public override bool ProcessInput(string input)
        {
            // During selection phase, let EditCommandBase handle it
            if (SelectedObjects == null)
            {
                return base.ProcessInput(input);
            }

            // During point picking phase, pass to PointInputHelper
            if (_pointInputHelper != null)
            {
                return _pointInputHelper.ProcessKeyboardInput(input);
            }

            return false;
        }

        private void MoveSelectedObjects()
        {
            if (SelectedObjects == null || _basePoint == null || _targetPoint == null)
            {
                Context?.OutputMessage(OpenCADStrings.UnableToCopyObjectsMissingContext);
                Cancel();
                return;
            }

            // Prefer cached references captured by EditCommandBase; fall back to context providers
            var document = CachedDocument ?? Context?.GetDocument();
            var viewport = CachedViewport ?? Context?.GetActiveViewport();
            var undoManager = CachedUndoManager ?? Context?.GetUndoRedoManager();

            if (document == null || viewport == null)
            {
                System.Diagnostics.Debug.WriteLine("MoveSelectedObjects: required document or viewport is null. Cancelling command.");
                Context?.OutputMessage(OpenCADStrings.UnableToCopyObjectsMissingContext);
                Cancel();
                return;
            }

            Vector3D v = _targetPoint.AsVector3D() - _basePoint.AsVector3D();

            // Build a translation matrix (you can replace this with any Matrix4x4 for rotate/scale)
            var translation = Matrix4x4.CreateTranslation((float)v.X, (float)v.Y, (float)v.Z);

            if (undoManager != null)
            {
                var action = new TransformGeometryAction(
                    SelectedObjects,
                    translation,
                    string.Format(OpenCADStrings.UndoCopyObjectsFormat, SelectedObjects.Count)
                );
                undoManager.ExecuteAction(action);
                Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsCopiedFormat, SelectedObjects.Count));
            }
            else
            {
                // Apply transform directly
                foreach (var obj in SelectedObjects)
                {
                    if (obj is GeometryBase geom)
                    {
                        try { geom.Transform(translation); }
                        catch (NotImplementedException)
                        {
                            geom.Move(v);
                        }
                    }
                }
                Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsCopiedNoUndoFormat, SelectedObjects.Count));
            }

            var viewModel = viewport?.DataContext as ViewportViewModel;
            viewModel?.ClearSelection();
            viewport?.Refresh();
        }
    }

    /// <summary>
    /// Undoable action for moving geometry.
    /// </summary>
    public class MoveGeometryAction : IUndoableAction
    {
        private readonly List<OpenCADObject> _objects;
        private readonly Vector3D _moveVector;
        private readonly string _description;

        public MoveGeometryAction(List<OpenCADObject> objects, Vector3D moveVector, string description)
        {
            _objects = objects.Select(o => o).ToList();
            _moveVector = moveVector;
            _description = description;
        }

        public string Description => _description;

        public void Execute()
        {
            foreach (var obj in _objects)
                if (obj is GeometryBase geom)
                    geom.Move(_moveVector);
        }

        public void Undo()
        {
            foreach (var obj in _objects)
                if (obj is GeometryBase geom)
                    geom.Move(-_moveVector);
        }
    }
}