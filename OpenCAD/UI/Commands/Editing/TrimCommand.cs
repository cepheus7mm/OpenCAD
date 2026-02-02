using GraphicsEngine;
using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers.GeoPoints;
using OpenCAD.Interfaces;
using OpenCAD.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands.Editing
{
    [InputCommand("trim", "Trim objects to cutting edges", "tr")]
    public class TrimCommand : CommandBase
    {
        private enum TrimPhase
        {
            SelectCuttingEdge,
            SelectObjectsToTrim
        }

        private TrimPhase _currentPhase = TrimPhase.SelectCuttingEdge;
        private IDrawable? _cuttingEdge;
        internal readonly List<TrimOperation> _trimOperations = new();
        private readonly List<GeoPoint> _currentIntersectionPoints = new();
        private ViewportViewModel? _viewModel;
        private ViewportControl? _viewport;
        private OpenCADDocument? _document;

        internal class TrimOperation
        {
            public OpenCADObject OriginalObject { get; set; }
            public List<OpenCADObject> ResultObjects { get; set; } = new();

            public TrimOperation(OpenCADObject original)
            {
                OriginalObject = original;
            }
        }

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
            _undoTransaction = Context?.GetUndoRedoManager()?.BeginTransaction("Trim");
            _trimOperations.Clear();
            _document = Context?.GetDocument();
            _viewport = Context?.GetActiveViewport();
            _viewModel = Context?.GetActiveViewportViewModel();
            if (_viewModel != null)
            {
                _viewModel.ObjectClicked += OnObjectClicked;
                _viewModel.SetCommandInputMode();
            }
            CurrentPrompt = "Select cutting edge:";
            Context?.OutputMessage("TRIM - Select cutting edge object");
        }

        public override async Task Execute()
        {
            // No-op: all setup is in Initialize
            await Task.CompletedTask;
        }

        public override bool ProcessInput(string input)
        {
            // Handle ENTER to finish trim operation
            if (string.IsNullOrWhiteSpace(input))
            {
                if (_currentPhase == TrimPhase.SelectObjectsToTrim)
                {
                    CompleteTrimCommand();
                    return true;
                }
                else
                {
                    Context?.OutputMessage("No cutting edge selected. Command cancelled.");
                    Cancel();
                    return false;
                }
            }
            return false;
        }

        private void OnObjectClicked(object? sender, ObjectClickedEventArgs e)
        {
            if (e == null || e.Object == null || !e.PickedPoint.IsValid)
                return;

            switch (_currentPhase)
            {
                case TrimPhase.SelectCuttingEdge:
                    HandleCuttingEdgeClick(e);
                    break;

                case TrimPhase.SelectObjectsToTrim:
                    HandleObjectToTrimClick(e);
                    break;
            }
        }

        private void HandleCuttingEdgeClick(ObjectClickedEventArgs e)
        {
            if (e.Object is not IDrawable drawable)
            {
                Context?.OutputMessage("Selected object cannot be used as cutting edge.");
                return;
            }

            _cuttingEdge = drawable;
            Context?.OutputMessage($"Cutting edge selected: {e.Object.GetType().Name}");
            CurrentPrompt = "Select object to trim (or press ENTER to finish):";
            _currentPhase = TrimPhase.SelectObjectsToTrim;

            if (_viewModel != null)
            {
                _viewModel.SelectionManager.AddToSelection((OpenCADObject)_cuttingEdge);
                _viewport?.Refresh();
            }
        }

        private void HandleObjectToTrimClick(ObjectClickedEventArgs e)
        {
            if (e.Object is not IDrawable objectToTrim)
            {
                Context?.OutputMessage("Selected object cannot be trimmed.");
                return;
            }

            TrimObject(objectToTrim, e.PickedPoint);
        }

        private void TrimObject(IDrawable objectToTrim, Point3D pickPoint)
        {
            var document = Context?.GetDocument();
            var viewport = Context?.GetActiveViewport();

            if (_cuttingEdge == null || document == null || viewport == null)
                return;

            // 1. Compute intersection
            var (pt1, pt2) = GeometricCalculator.Intersection(_cuttingEdge, objectToTrim);
            if (!pt1.IsValid)
            {
                Context?.OutputMessage("Objects do not intersect.");
                return;
            }

            ShowIntersectionPoints(pt1, pt2);

            // 2. Compute trim result
            var trimResult = CalculateTrimResult(document, objectToTrim, pt1, pt2, pickPoint);
            if (!IsValidTrimResult(trimResult))
            {
                Context?.OutputMessage("Cannot trim at this location.");
                return;
            }

            // 3. Record operation (but do NOT apply it yet)
            _trimOperations.Add(trimResult!);

            // 4. Preview only (no document mutation)
            PreviewTrimResult(trimResult!);

            viewport.Refresh();
            Context?.OutputMessage($"Trimmed {trimResult!.OriginalObject.GetType().Name}");
        }

        private void PreviewTrimResult(TrimOperation op)
        {
            var preview = _viewModel?.PreviewManager;
            if (preview == null)
                return;

            // Hide the original object visually
            preview.HideOriginal(op.OriginalObject);

            // Show each trimmed result visually
            foreach (var result in op.ResultObjects)
                preview.ShowPreview(result);
        }

        private static bool IsValidTrimResult(TrimOperation? op)
        {
            return op != null && op.ResultObjects.Count > 0;
        }

        private void ApplyTrimResult(OpenCADDocument document, ViewportControl viewport, TrimOperation op)
        {
            if (document == null || viewport == null || op == null)
                return;

            var original = op.OriginalObject;

            // Remove original geometry
            RemoveGeometry(document, viewport, original);

            // Add trimmed result geometry
            foreach (var result in op.ResultObjects)
                AddGeometry(document, viewport, result);
        }

        private static void RemoveGeometry(OpenCADDocument document, ViewportControl viewport, OpenCADObject obj)
        {
            document.Remove(obj);
            viewport.RemoveObject(obj);
        }

        private static void AddGeometry(OpenCADDocument document, ViewportControl viewport, OpenCADObject obj)
        {
            document.Add(obj);
            viewport.AddObject(obj);
        }

        private TrimOperation? CalculateTrimResult( OpenCADDocument document, IDrawable objectToTrim, Point3D pt1, Point3D pt2, Point3D pickPoint)
        {
            if (document == null)
                return null;

            return objectToTrim switch
            {
                Line line => TrimLineInternal(document, line, pt1, pt2, pickPoint),
                Arc arc => TrimArcInternal(arc, pt1, pt2, pickPoint),
                Circle circle => TrimCircleInternal(circle, pt1, pt2, pickPoint),
                _ => null
            };
        }

        private TrimOperation TrimLineInternal(OpenCADDocument doc, Line line, Point3D pt1, Point3D pt2, Point3D pickPoint)
        {
            var op = new TrimOperation(line);
            var pieces = TrimLine(doc, line, pt1, pt2, pickPoint);
            op.ResultObjects.AddRange(pieces);
            return op;
        }

        private TrimOperation? TrimArcInternal(Arc arc, Point3D pt1, Point3D pt2, Point3D pickPoint)
        {
            bool pt1OnArc = GeometricCalculator.IsPointOnArc(pt1, arc);
            bool pt2Valid = pt2.IsValid;
            bool pt2OnArc = pt2Valid && GeometricCalculator.IsPointOnArc(pt2, arc);

            Point3D? validPt1 = pt1OnArc ? pt1 : null;
            Point3D? validPt2 = pt2OnArc ? pt2 : null;

            if (validPt1 == null && validPt2 == null)
            {
                Context?.OutputMessage("No intersections found on arc sweep.");
                return null;
            }

            var op = new TrimOperation(arc);
            var pieces = TrimArc(arc, validPt1, validPt2, pickPoint);
            op.ResultObjects.AddRange(pieces);
            return op;
        }

        private TrimOperation? TrimCircleInternal(Circle circle, Point3D pt1, Point3D pt2, Point3D pickPoint)
        {
            if (!pt2.IsValid)
            {
                Context?.OutputMessage("Circle requires two intersection points to trim.");
                return null;
            }

            var trimmed = TrimCircle(circle, pt1, pt2, pickPoint);
            if (trimmed == null)
                return null;

            var op = new TrimOperation(circle);
            op.ResultObjects.Add(trimmed);
            return op;
        }

        private List<Line> TrimLine(OpenCADDocument document, Line line, Point3D pt1, Point3D pt2, Point3D pickPoint)
        {
            var results = new List<Line>();

            if (!pt2.IsValid)
            {
                double distToStart = pickPoint.DistanceTo(line.Start);
                double distToEnd = pickPoint.DistanceTo(line.End);

                if (distToStart < distToEnd)
                {
                    results.Add(new Line(document, pt1, line.End));
                }
                else
                {
                    results.Add(new Line(document, line.Start, pt1));
                }
            }
            else
            {
                double dist1 = pickPoint.DistanceTo(pt1);
                double dist2 = pickPoint.DistanceTo(pt2);

                Point3D nearPoint, farPoint;
                if (dist1 < dist2)
                {
                    nearPoint = pt1;
                    farPoint = pt2;
                }
                else
                {
                    nearPoint = pt2;
                    farPoint = pt1;
                }

                double totalDist = pt1.DistanceTo(pt2);
                double pickDist1 = pickPoint.DistanceTo(pt1);
                double pickDist2 = pickPoint.DistanceTo(pt2);

                if (pickDist1 + pickDist2 <= totalDist + 1e-6)
                {
                    results.Add(new Line(document, line.Start, nearPoint));
                    results.Add(new Line(document, farPoint, line.End));
                }
                else
                {
                    results.Add(new Line(document, nearPoint, farPoint));
                }
            }

            return results;
        }

        private List<Arc> TrimArc(Arc arc, Point3D? pt1, Point3D? pt2, Point3D pickPoint)
        {
            var results = new List<Arc>();
            Context?.OutputMessage("Arc trimming not yet implemented.");
            return results;
        }

        private Arc? TrimCircle(Circle circle, Point3D pt1, Point3D pt2, Point3D pickPoint)
        {
            double angle1 = Math.Atan2(pt1.Y - circle.Center.Y, pt1.X - circle.Center.X);
            double angle2 = Math.Atan2(pt2.Y - circle.Center.Y, pt2.X - circle.Center.X);

            angle1 = GeometricCalculator.NormalizeUnsigned(angle1);
            angle2 = GeometricCalculator.NormalizeUnsigned(angle2);

            double pickAngle = Math.Atan2(pickPoint.Y - circle.Center.Y, pickPoint.X - circle.Center.X);
            pickAngle = GeometricCalculator.NormalizeUnsigned(pickAngle);

            Context?.OutputMessage("Circle trimming not yet implemented.");
            return null;
        }

        private void ShowIntersectionPoints(Point3D pt1, Point3D pt2)
        {
            _currentIntersectionPoints.Clear();

            if (pt1.IsValid)
            {
                _currentIntersectionPoints.Add(new GeoPoint(pt1, GeoPointModes.Intersection));
            }

            if (pt2.IsValid)
            {
                _currentIntersectionPoints.Add(new GeoPoint(pt2, GeoPointModes.Intersection));
            }

            // TODO: Display these as visual markers in the viewport
        }

        private void CompleteTrimCommand()
        {
            // Stop listening for clicks
            if (_viewModel != null)
            {
                _viewModel.ObjectClicked -= OnObjectClicked;
                _viewModel.SetSelectionInputMode();
            }

            // Nothing trimmed → abort transaction and exit
            if (_trimOperations.Count == 0)
            {
                _document?.GetUndoRedoManager().AbortTransaction();
                Context?.OutputMessage("No trim operations performed.");
                RaiseCommandCompleted();
                return;
            }

            var undo = _document?.GetUndoRedoManager();
            if (undo == null || _document == null)
            {
                Context?.OutputMessage("Internal error: no document or undo manager.");
                RaiseCommandCompleted();
                return;
            }

            // Apply all trim operations for real (document mutation happens INSIDE the actions)
            foreach (var op in _trimOperations)
            {
                // 1. Remove original
                var remove = new RemoveGeometryAction(op.OriginalObject, "Trim remove");
                undo.ExecuteAction(remove);

                // 2. Add each trimmed result
                foreach (var result in op.ResultObjects)
                {
                    var add = new AddGeometryAction(result, "Trim add");
                    undo.ExecuteAction(add);
                }
            }

            // Commit the transaction (all actions were added above)
            undo.CommitTransaction();

            // Now commit the preview (finalize visual state)
            _viewModel.PreviewManager.Commit();

            // Clean up selection
            if (_cuttingEdge != null)
                _viewModel.SelectionManager.Deselect((OpenCADObject)_cuttingEdge);

            Context?.OutputMessage($"Trim completed: {_trimOperations.Count} operation(s)");
            RaiseCommandCompleted();
        }

        public override void Cancel()
        {
            if (_viewModel == null)
                return;

            // Stop listening for clicks and restore normal input mode
            _viewModel.ObjectClicked -= OnObjectClicked;
            _viewModel.SetSelectionInputMode();

            if (_cuttingEdge != null)
                _viewModel.SelectionManager.Deselect((OpenCADObject)_cuttingEdge);

            _viewModel.PreviewManager.Clear();
            // Abort the trim transaction (no geometry changes were committed)
            _document?.GetUndoRedoManager()?.AbortTransaction();

            // Clear transient state
            _currentIntersectionPoints.Clear();
            _trimOperations.Clear();
            _cuttingEdge = null;

            base.Cancel();
        }
    }
}
