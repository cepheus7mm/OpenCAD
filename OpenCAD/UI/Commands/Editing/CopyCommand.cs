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
    [InputCommand("copy", "Copy selected objects", "cp")]
    public class CopyCommand : EditCommandBase
    {
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

            try
            {
                _basePoint = await GetBasePoint();
                if (_basePoint == null)
                {
                    Cancel();
                    return;
                }

                // Start preview (keeps cached providers and preview subscription active)
                StartPreview(_basePoint);

                try
                {
                    // Loop: allow the user to pick as many targets as they want.
                    // Each target creates a copy; ESC (or cancel) ends the loop.
                    while (true)
                    {
                        _targetPoint = await GetTargetPoint();

                        // If null, user pressed ESC or cancelled -> finish gracefully
                        if (_targetPoint == null)
                            break;

                        // Commit a copy for this target while cached providers still exist
                        CommitCopyForCurrentTarget();

                        // Remove preview clones (but keep preview mode active so user can pick more points)
                        ClearPreviewClones();

                        // Continue loop for more targets
                    }

                    // User finished (ESC). Stop preview and complete command.
                    StopPreview();

                    //System.Diagnostics.Debug.WriteLine("CopyCommand: Raising CommandCompleted");
                    CommandCompleted();
                }
                finally
                {
                    // Ensure preview cleaned up
                    StopPreview();
                }
            }
            catch (OperationCanceledException)
            {
                // Token cancelled -> clear preview and finish
                ClearPreviewClones();
                StopPreview();
                CommandCompleted();
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

        /// <summary>
        /// Commit a single copy operation using currently selected objects and _basePoint/_targetPoint.
        /// This will add clones to the document (and optionally record an undo action).
        /// </summary>
        private void CommitCopyForCurrentTarget()
        {
            if (SelectedObjects == null || _basePoint == null || _targetPoint == null)
            {
                Context?.OutputMessage(UnableToActOnObjectsMissingContext);
                return;
            }

            // Use cached providers from EditCommandBase when available
            var document = CachedDocument ?? Context?.GetDocument();
            var viewport = CachedViewport ?? Context?.GetActiveViewport();
            var undoManager = CachedUndoManager ?? Context?.GetUndoRedoManager();

            if (document == null || viewport == null)
            {
                //System.Diagnostics.Debug.WriteLine("CopySelectedObjects: required document or viewport is null. Cancelling command.");
                Context?.OutputMessage(UnableToActOnObjectsMissingContext);
                return;
            }

            Vector3D moveVector = _targetPoint.AsVector3D() - _basePoint.AsVector3D();

            // Create clones (independent objects)
            var clones = new List<OpenCADObject>();
            foreach (var obj in SelectedObjects)
            {
                var clone = CreateTranslatedClone(obj, moveVector, document);
                if (clone != null)
                    clones.Add(clone);
            }

            if (clones.Count == 0)
            {
                Context?.OutputMessage(UnableToActOnObjectsMissingContext);
                return;
            }

            if (undoManager != null)
            {
                // Add as single undoable group (so one undo removes all clones from this pick)
                var action = new AddMultipleGeometryAction(clones, document, viewport, string.Format(OpenCADStrings.UndoMoveObjectsFormat, clones.Count));
                undoManager.ExecuteAction(action);
                Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsMovedFormat, clones.Count));
            }
            else
            {
                // Add directly
                foreach (var c in clones)
                {
                    document.Add(c);
                    viewport.AddObject(c);
                }
                Context?.OutputMessage(string.Format(OpenCADStrings.ObjectsMovedNoUndoFormat, clones.Count));
            }

            // Clear selection and refresh the viewport so new copies are visible
            var vm = viewport?.DataContext as ViewportViewModel;
            vm?.ClearSelection();
            viewport?.Refresh();
        }
    }
}