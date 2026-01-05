using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UI.Commands.Undo;
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

            if (_currentPhase == TrimPhase.SelectCuttingEdge)
            {
                if (e.Object is IDrawable drawable)
                {
                    _cuttingEdge = drawable;
                    Context?.OutputMessage($"Cutting edge selected: {e.Object.GetType().Name}");
                    CurrentPrompt = "Select object to trim (or press ENTER to finish):";
                    _currentPhase = TrimPhase.SelectObjectsToTrim;

                    if (_viewModel != null)
                    {
                        _viewModel.SelectObject((OpenCADObject)_cuttingEdge);
                        _viewport?.Refresh();
                    }
                }
                else
                {
                    Context?.OutputMessage("Selected object cannot be used as cutting edge.");
                }
            }
            else if (_currentPhase == TrimPhase.SelectObjectsToTrim)
            {
                if (e.Object is IDrawable objectToTrim)
                {
                    TrimObject(objectToTrim, e.PickedPoint);
                }
                else
                {
                    Context?.OutputMessage("Selected object cannot be trimmed.");
                }
            }
        }

        private void TrimObject(IDrawable objectToTrim, Point3D pickPoint)
        {
            var document = Context?.GetDocument();
            var viewport = Context?.GetActiveViewport();

            if (_cuttingEdge == null || document == null || viewport == null)
                return;

            var (pt1, pt2) = GeometricCalculator.Intersection(_cuttingEdge, objectToTrim);

            if (!pt1.IsValid)
            {
                Context?.OutputMessage("Objects do not intersect.");
                return;
            }

            ShowIntersectionPoints(pt1, pt2);

            var trimResult = CalculateTrimResult(document, objectToTrim, pt1, pt2, pickPoint);

            if (trimResult == null || trimResult.ResultObjects.Count == 0)
            {
                Context?.OutputMessage("Cannot trim at this location.");
                return;
            }

            _trimOperations.Add(trimResult);

            var originalObj = trimResult.OriginalObject;
            document.Remove(originalObj);
            viewport.RemoveObject(originalObj);

            foreach (var resultObj in trimResult.ResultObjects)
            {
                document.Add(resultObj);
                viewport.AddObject(resultObj);
            }

            viewport.Refresh();
            Context?.OutputMessage($"Trimmed {originalObj.GetType().Name}");
        }

        private TrimOperation? CalculateTrimResult(OpenCADDocument document, IDrawable objectToTrim, Point3D pt1, Point3D pt2, Point3D pickPoint)
        {
            if (document == null)
                return null;

            var operation = new TrimOperation((OpenCADObject)objectToTrim);

            if (objectToTrim is Line line)
            {
                var trimmedLines = TrimLine(document, line, pt1, pt2, pickPoint);
                operation.ResultObjects.AddRange(trimmedLines);
                return operation;
            }

            if (objectToTrim is Arc arc)
            {
                bool pt1OnArc = GeometricCalculator.IsPointOnArc(pt1, arc);
                bool pt2Valid = pt2.IsValid;
                bool pt2OnArc = pt2Valid && GeometricCalculator.IsPointOnArc(pt2, arc);

                Point3D? validPt1 = pt1OnArc ? pt1 : null;
                Point3D? validPt2 = (pt2Valid && pt2OnArc) ? pt2 : null;

                if (validPt1 == null && validPt2 == null)
                {
                    Context?.OutputMessage("No intersections found on arc sweep.");
                    return null;
                }

                var trimmedArcs = TrimArc(arc, validPt1, validPt2, pickPoint);
                operation.ResultObjects.AddRange(trimmedArcs);
                return operation;
            }

            if (objectToTrim is Circle circle)
            {
                if (!pt2.IsValid)
                {
                    Context?.OutputMessage("Circle requires two intersection points to trim.");
                    return null;
                }

                var trimmedArc = TrimCircle(circle, pt1, pt2, pickPoint);
                if (trimmedArc != null)
                {
                    operation.ResultObjects.Add(trimmedArc);
                }
                return operation;
            }

            return null;
        }

        private List<Line> TrimLine(OpenCADDocument document, Line line, Point3D pt1, Point3D pt2, Point3D pickPoint)
        {
            var results = new List<Line>();

            if (!pt2.IsValid)
            {
                double distToStart = pickPoint.DistanceTo(line.StartPoint);
                double distToEnd = pickPoint.DistanceTo(line.EndPoint);

                if (distToStart < distToEnd)
                {
                    results.Add(new Line(document, pt1, line.EndPoint));
                }
                else
                {
                    results.Add(new Line(document, line.StartPoint, pt1));
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
                    results.Add(new Line(document, line.StartPoint, nearPoint));
                    results.Add(new Line(document, farPoint, line.EndPoint));
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
            if (_viewModel != null)
            {
                _viewModel.ObjectClicked -= OnObjectClicked;
                _viewModel.SetSelectionInputMode();
            }

            if (_trimOperations.Count == 0)
            {
                Context?.OutputMessage("No trim operations performed.");
                RaiseCommandCompleted();
                return;
            }

            var undoManager = Context?.GetUndoRedoManager();

            if (undoManager != null && _document != null && _viewport != null)
            {
                var action = new TrimUndoAction(_trimOperations, _document);
                undoManager.AddActionWithoutExecute(action);
            }

            if (_cuttingEdge != null)
                _viewModel?.DeselectObject((OpenCADObject)_cuttingEdge);

            Context?.OutputMessage($"Trim completed: {_trimOperations.Count} operation(s)");
            RaiseCommandCompleted();
        }

        public override void Cancel()
        {
            if (_viewModel != null)
            {
                _viewModel.ObjectClicked -= OnObjectClicked;
                _viewModel.SetSelectionInputMode();
                if (_cuttingEdge != null)
                    _viewModel?.DeselectObject((OpenCADObject)_cuttingEdge);
            }

            _currentIntersectionPoints.Clear();
            _trimOperations.Clear();
            _cuttingEdge = null;

            base.Cancel();
        }
    }
}
