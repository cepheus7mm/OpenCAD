using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.InputHelpers;
using UI.Controls.Viewport;

using ResType = UI.Commands.InputHelpers.InputResult.InputResultType;

namespace UI.Commands.Drawing
{
    /// <summary>
    /// Command to create a polyline with multiple vertices
    /// </summary>
    [InputCommand("polyline", "Create a polyline (prompts for vertices)", "pl")]
    public class PolylineCommand : CommandBase
    {
        private enum PolylineMode
        {
            Line, // Default mode
            Arc   // Arc segment mode
        }

        private PolylineMode _mode;

        private enum LineSegmentMode
        {
            Point,
            Direction,
            Length,
            Width,
        }

        private LineSegmentMode _lineSegmentMode;

        private enum ArcSegmentMode
        {
            Radius,      // Default: specify radius
            Angle,       // Specify included angle
            Center,      // Specify center point
            Direction,   // Specify tangent direction
            SecondPoint,  // Three-point arc
            Width,
        }

        private Polyline? _currentPolyline;
        private ViewportControl? _viewport;
        private PolylineVertex? _currentVertex;
        private PolylineVertex? _nextVertex;
        private double _directionAngle = double.NaN;
        private double _length = double.NaN;

        public override bool IsMultiStep => true;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
            _mode = PolylineMode.Line;
            _lineSegmentMode = LineSegmentMode.Point;
        }

        public override async Task Execute()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var document = Context?.GetDocument();
                if (document == null)
                {
                    Context?.OutputMessage("No active document to create polyline in.");
                    Cancel();
                    return;
                }

                // Get viewport reference for preview (must be done on UI thread)
                Context?.PostToUI(() =>
                {
                    _viewport = Context?.GetActiveViewport();
                });

                // Create new polyline
                _currentPolyline = new Polyline(document);

                // Get first point
                BasePoint = null;
                var result = await GetPoint(
                    "Specify start point",
                    allowLastPoint: true);

                if (result == null || result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                {
                    Cancel();
                    return;
                }

                if (result.Point is not Point3D firstPoint)
                {
                    Cancel();
                    return;
                }

                _currentVertex = _currentPolyline.AddVertex(firstPoint);

                Context?.OutputMessage(
                    string.Format(
                        OpenCADStrings.LineStartPointConfirmed,
                        firstPoint.X,
                        firstPoint.Y,
                        firstPoint.Z));

                // Add the polyline to preview once we have at least one vertex (on UI thread)
                if (_currentPolyline != null)
                {
                    var polylineToPreview = _currentPolyline;
                    Context?.PostToUI(() =>
                    {
                        _viewport?.AddPreviewObject(polylineToPreview);
                    });
                }

                // Loop to get additional vertices
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var addResult = await AddLineSegment();
                    //if (!addResult)
                    //    continue;
                    //if (_currentVertex == null)
                    //    break;
                    // Refresh preview to show new vertex (on UI thread)
                    var polylineToPreview = _currentPolyline;
                    Context?.PostToUI(() =>
                    {
                        _viewport?.AddPreviewObject(polylineToPreview);
                    });
                    RefreshPreview();
                }

                // Remove from preview before finalizing (on UI thread)
                if (_currentPolyline != null)
                {
                    var polylineToRemove = _currentPolyline;
                    RemovePreview(polylineToRemove);
                }

                // Finalize polyline if it has at least 2 vertices
                if (_currentPolyline.VertexCount >= 2)
                {
                    CreatePolyline();
                }
                else
                {
                    Context?.OutputMessage("Polyline creation cancelled - need at least 2 vertices.");
                }

                // Command completed
                Context?.OutputMessage("Polyline command completed.");
                RaiseCommandCompleted();
            }
            catch (OperationCanceledException)
            {
                Cancel();
            }
            finally
            {
                // Clean up preview (on UI thread)
                if (_currentPolyline != null)
                {
                    var polylineToRemove = _currentPolyline;
                    Context?.PostToUI(() =>
                    {
                        _viewport?.RemovePreviewObject(polylineToRemove);
                    });
                }
                
                _currentPolyline = null;
                _currentVertex = null;
                _viewport = null;
            }
        }

        private async Task<bool> AddLineSegment()
        {
            if (_currentPolyline == null || _currentVertex == null)
                return false;
            BasePoint = _currentVertex.Position;
            _nextVertex = new PolylineVertex(_currentPolyline?.Document, Point3D.Origin);
            _nextVertex.StartWidth = _currentVertex.EndWidth;
            _nextVertex.EndWidth = _currentVertex.EndWidth;

            var result = await GetLineSegmentInput();

            if (result.Point is not Point3D nextPoint)
            {
                _cancellationTokenSource?.Cancel();
                return false;
            }

            // Add vertex to polyline
            _nextVertex.Position = nextPoint;
            _currentPolyline.AddVertex(_nextVertex);
            _currentVertex = _nextVertex;
            return true;
        }

        private async Task<InputResult> GetLineSegmentInput()
        {
            switch (_lineSegmentMode)
            {
                case LineSegmentMode.Point:
                    return await GetPolylinePoint();
                case LineSegmentMode.Direction:
                    return await GetPolylineDirection();
                case LineSegmentMode.Length:
                    return await GetPolylineLength();
                case LineSegmentMode.Width:
                    return await GetPolylineWidth();
                default:
                    return new InputResult() { ResultType = InputResult.InputResultType.Cancel};
            }
        }

        private async Task<InputResult> GetPolylinePoint()
        {
            BasePoint = _currentVertex?.Position;
            var result = await GetPoint(
                "Specify next point or press ESC to finish",
                allowLastPoint: false,
                keyWords: new[] { "Close", "Undo", "Arc", "Direction", "Length", "Width" });

            if (result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
            {
                // User pressed ESC - finalize the polyline
                return result;
            }

            return await ProcessLineInput(result);
        }

        private async Task<InputResult> GetPolylineDirection()
        {
            BasePoint = _currentVertex?.Position;
            var result = await GetAngle(
                "Specify direction angle",
                defaultValue: null,
                keyWords: null);

            return GetPointFromLengthAndDirection(result, false);
        }

        private async Task<InputResult> GetPolylineLength()
        {
            BasePoint = _currentVertex?.Position;
            var result = await GetDistance(
                "Specify length",
                defaultValue: null,
                keyWords: null);

            return GetPointFromLengthAndDirection(result, true);
        }

        private InputResult GetPointFromLengthAndDirection(InputResult result, bool isLengthResult)
        {
            if (result.ResultType != ResType.Double)
            {
                result.ResultType = ResType.Cancel;
                return result;
            }
            if (isLengthResult)
                _length = result.DoubleValue;
            else
                _directionAngle = result.DoubleValue;
            if ((!isLengthResult && double.IsNaN(_length)) || (isLengthResult && double.IsNaN(_directionAngle)))
            {
                result.ResultType = ResType.ProcessingResult;
                result.ProcessingResult = InputResult.ProcessingResultType.RequiresMoreInput;
                return result;
            }

            // Calculate next point based on direction and length
            if (_currentVertex != null)
            {
                var startPoint = _currentVertex.Position;
                var directionVector = new Vector3D(Math.Cos(_directionAngle), Math.Sin(_directionAngle), 0).Normalized;
                var nextPoint = startPoint + directionVector * _length;
                result.Point = nextPoint;
                result.ResultType = ResType.Point;
                _directionAngle = double.NaN;
                _length = double.NaN;
            }

            return result;
        }

        private async Task<InputResult> GetPolylineWidth()
        {
            if (_currentVertex == null || _nextVertex == null)
            {
                return new InputResult() { ResultType = InputResult.InputResultType.Cancel };
            }
            var result = await GetDistance(
                "Specify start width",
                defaultValue: _currentVertex.StartWidth,
                keyWords: null);
            if (result.ResultType == ResType.Cancel)
            {
                return result;
            }
            double startWidth = result.DoubleValue;
            result = await GetDistance(
                "Specify end width",
                defaultValue: startWidth,
                keyWords: null);
            if (result.ResultType == ResType.Cancel)
            {
                return result;
            }
            double endWidth = result.DoubleValue;
            _currentVertex.StartWidth = startWidth;
            _nextVertex.StartWidth = endWidth;
            _nextVertex.EndWidth = endWidth;
            return new InputResult() { ResultType = ResType.ProcessingResult, ProcessingResult = InputResult.ProcessingResultType.RequiresMoreInput }; 
        }

        private async Task<InputResult> ProcessLineInput(InputResult result)
        {

            if (result.ResultType == ResType.Keyword)
            {
                var keyword = result.Keyword?.ToUpperInvariant();

                if (keyword == "C" || keyword == "CLOSE")
                {
                    // Close the polyline
                    if (_currentPolyline.VertexCount >= 2)
                    {
                        _currentPolyline.Close();
                        Context?.OutputMessage("Polyline closed.");
                        // Refresh preview to show closed polyline (on UI thread)
                        RefreshPreview();
                    }
                    return result;
                }
                else if (keyword == "U" || keyword == "UNDO")
                {
                    // Remove last vertex
                    if (_currentPolyline.VertexCount > 1)
                    {
                        _currentPolyline.RemoveVertex(_currentPolyline.VertexCount - 1);
                        var lastVertex = _currentPolyline.GetVertex(_currentPolyline.VertexCount - 1);
                        if (lastVertex != null)
                        {
                            _currentVertex = lastVertex;
                            Context?.OutputMessage("Last vertex removed.");
                            // Refresh preview after undo (on UI thread)
                            RefreshPreview();
                        }
                    }
                    else
                    {
                        Context?.OutputMessage("Cannot undo - only one vertex remains.");
                    }
                    return await GetLineSegmentInput();
                }
                else if (keyword == "D" || keyword == "DIRECTION")
                {
                    return await ProcessDirection(result);
                }
                else if (keyword == "L" || keyword == "LENGTH")
                {
                    return await ProcessLength(result);
                }
                else if (keyword == "W" || keyword == "WIDTH")
                {
                    return await ProcessWidth(result);
                }
                else if (keyword == "A" || keyword == "ARC")
                {
                    // Add arc segment
                    await AddArcSegment();
                    var lastVertex = _currentPolyline.GetVertex(_currentPolyline.VertexCount - 1);
                    if (lastVertex != null)
                    {
                        _currentVertex = lastVertex;
                        // Refresh preview after adding arc segment (on UI thread)
                        RefreshPreview();
                    }
                }
                result.ResultType = InputResult.InputResultType.Cancel;
                return result;
            }
            return result;
        }

        private async Task<InputResult> ProcessDirection(InputResult result)
        {
            var previousMode = _lineSegmentMode;
            _lineSegmentMode = LineSegmentMode.Direction;
            Context?.OutputMessage("Switched to Direction mode.");
            result = await GetPolylineDirection();
            if (result.ResultType == InputResult.InputResultType.ProcessingResult &&
                result.ProcessingResult == InputResult.ProcessingResultType.RequiresMoreInput)
            {
                _lineSegmentMode = previousMode;
                return await GetLineSegmentInput();
            }
            else
            {
                _lineSegmentMode = previousMode;
                return result;
            }
        }

        private async Task<InputResult> ProcessLength(InputResult result)
        {
            var previousMode = _lineSegmentMode;
            _lineSegmentMode = LineSegmentMode.Length;
            Context?.OutputMessage("Switched to Length mode.");
            result = await GetPolylineLength();
            if (result.ResultType == InputResult.InputResultType.ProcessingResult &&
                result.ProcessingResult == InputResult.ProcessingResultType.RequiresMoreInput)
            {
                _lineSegmentMode = previousMode;
                return await GetLineSegmentInput();
            }
            else
            {
                _lineSegmentMode = previousMode;
                return result;
            }
        }

        private async Task<InputResult> ProcessWidth(InputResult result)
        {
            var previousMode = _lineSegmentMode;
            _lineSegmentMode = LineSegmentMode.Width;
            Context?.OutputMessage("Switched to Width mode.");
            result = await GetPolylineWidth();
            if (result.ResultType == InputResult.InputResultType.ProcessingResult &&
                result.ProcessingResult == InputResult.ProcessingResultType.RequiresMoreInput)
            {
                _lineSegmentMode = previousMode;
                return await GetLineSegmentInput();
            }
            else
            {
                _lineSegmentMode = previousMode;
                return result;
            }
        }

        private void RefreshPreview()
        {
            Context?.PostToUI(() =>
            {
                _viewport?.Refresh();
            });
        }

        private void RemovePreview(Polyline polyline)
        {
            Context?.PostToUI(() =>
            {
                _viewport?.RemovePreviewObject(polyline);
            });

        }

        private async Task AddArcSegment()
        {
            if (_currentPolyline == null)
                return;

            // Step 1: Get the endpoint with all arc definition keywords
            var startPoint = _currentVertex.Position;
            BasePoint = _currentVertex.Position;
            var result = await GetPoint(
                "Specify endpoint of arc (or [Angle/CEnter/Direction/Halfwidth/Line/Radius/Second pt/Undo])",
                allowLastPoint: false,
                keyWords: new[] { "A", "Angle", "CE", "CEnter", "D", "Direction", "H", "Halfwidth", "L", "Line", "R", "Radius", "S", "Second", "U", "Undo" });

            if (result == null || result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
            {
                Context?.OutputMessage("Arc segment cancelled.");
                return;
            }

            // Handle keywords at Step 1
            if (result.ResultType == InputHelpers.InputResult.InputResultType.Keyword)
            {
                var keyword = result.Keyword?.ToUpperInvariant();
                
                if (keyword == "L" || keyword == "LINE")
                {
                    Context?.OutputMessage("Returning to line mode.");
                    return;
                }
                else if (keyword == "U" || keyword == "UNDO")
                {
                    // Undo is already handled in the main loop, just return
                    Context?.OutputMessage("Use Undo in the main prompt.");
                    return;
                }
                else if (keyword == "H" || keyword == "HALFWIDTH")
                {
                    await SetHalfwidth();
                    return;
                }
                // For other keywords, we need to get endpoint first, then process the arc definition
                else
                {
                    // Store the mode keyword and get the endpoint
                    ArcSegmentMode mode = keyword switch
                    {
                        "A" or "ANGLE" => ArcSegmentMode.Angle,
                        "CE" or "CENTER" => ArcSegmentMode.Center,
                        "D" or "DIRECTION" => ArcSegmentMode.Direction,
                        "R" or "RADIUS" => ArcSegmentMode.Radius,
                        "S" or "SECOND" => ArcSegmentMode.SecondPoint,
                        _ => ArcSegmentMode.Radius
                    };

                    // Now get the endpoint
                    BasePoint = _currentVertex.Position;
                    result = await GetPoint(
                        "Specify endpoint of arc",
                        allowLastPoint: false);

                    if (result == null || result.ResultType == InputHelpers.InputResult.InputResultType.Cancel || result.Point is not Point3D endPoint)
                    {
                        Context?.OutputMessage("Arc segment cancelled.");
                        return;
                    }

                    // Process the arc with the selected mode
                    await ProcessArcWithMode(startPoint, endPoint, mode);
                    return;
                }
            }

            // If we got a point directly, prompt for radius/options
            if (result.Point is not Point3D endpoint)
            {
                Context?.OutputMessage("Invalid endpoint.");
                return;
            }

            // Refresh preview to show endpoint
            Context?.PostToUI(() =>
            {
                _viewport?.Refresh();
            });

            // Step 2: Get the arc definition (radius is default)
            var distanceResult = await GetDistance(
                "Specify radius (or [Angle/CEnter/Direction/Halfwidth/Second pt])",
                defaultValue: null,
                keyWords: new[] { "A", "Angle", "CE", "CEnter", "D", "Direction", "H", "Halfwidth", "S", "Second" });

            if (distanceResult.ResultType == InputHelpers.InputResult.InputResultType.Keyword)
            {
                var keyword = distanceResult.Keyword?.ToUpperInvariant();

                if (keyword == "H" || keyword == "HALFWIDTH")
                {
                    await SetHalfwidth();
                    // After setting halfwidth, continue with arc definition
                    distanceResult = await GetDistance(
                        "Specify radius (or [Angle/CEnter/Direction/Second pt])",
                        defaultValue: null,
                        keyWords: new[] { "A", "Angle", "CE", "CEnter", "D", "Direction", "S", "Second" });
                }

                ArcSegmentMode mode = keyword switch
                {
                    "A" or "ANGLE" => ArcSegmentMode.Angle,
                    "CE" or "CENTER" => ArcSegmentMode.Center,
                    "D" or "DIRECTION" => ArcSegmentMode.Direction,
                    "S" or "SECOND" => ArcSegmentMode.SecondPoint,
                    _ => ArcSegmentMode.Radius
                };

                await ProcessArcWithMode(startPoint, endpoint, mode);
            }
            else if (!double.IsNaN(distanceResult.DoubleValue))
            {
                // User entered a radius value
                double radius = distanceResult.DoubleValue;
                double bulge = GeometricCalculator.GetBulgeFromRadius(startPoint, endpoint, radius);
                
                // Validate bulge
                if (double.IsNaN(bulge) || double.IsInfinity(bulge))
                {
                    Context?.OutputMessage("Invalid arc parameters. Using straight line.");
                    bulge = 0;
                }

                // Set bulge on the last vertex
                var lastVertexIndex = _currentPolyline.VertexCount - 1;
                _currentPolyline.SetVertexBulge(lastVertexIndex, bulge);

                // Add the endpoint
                _currentPolyline.AddVertex(endpoint);

                Context?.OutputMessage($"Arc segment added with radius = {radius:F3}, bulge = {bulge:F3}");
            }
            else
            {
                Context?.OutputMessage("Invalid input. Arc segment cancelled.");
            }
        }

        private async Task ProcessArcWithMode(Point3D startPoint, Point3D endPoint, ArcSegmentMode mode)
        {
            double bulge = 0;

            switch (mode)
            {
                case ArcSegmentMode.Angle:
                    bulge = await GetBulgeFromAngle(startPoint, endPoint);
                    break;
                case ArcSegmentMode.Center:
                    bulge = await GetBulgeFromCenter(startPoint, endPoint);
                    break;
                case ArcSegmentMode.Direction:
                    bulge = await GetBulgeFromDirection(startPoint, endPoint);
                    break;
                case ArcSegmentMode.SecondPoint:
                    bulge = await GetBulgeFromSecondPoint(startPoint, endPoint);
                    break;
                case ArcSegmentMode.Radius:
                    var radiusResult = await GetDistance("Specify radius", defaultValue: null);
                    if (!double.IsNaN(radiusResult.DoubleValue))
                    {
                        bulge = GeometricCalculator.GetBulgeFromRadius(startPoint, endPoint, radiusResult.DoubleValue);
                    }
                    break;
            }

            // Validate bulge
            if (double.IsNaN(bulge) || double.IsInfinity(bulge))
            {
                Context?.OutputMessage("Invalid arc parameters. Using straight line.");
                bulge = 0;
            }

            // Set bulge on the last vertex
            if (_currentPolyline != null)
            {
                var lastVertexIndex = _currentPolyline.VertexCount - 1;
                _currentPolyline.SetVertexBulge(lastVertexIndex, bulge);

                // Add the endpoint
                _currentPolyline.AddVertex(endPoint);

                Context?.OutputMessage($"Arc segment added with bulge = {bulge:F3}");
            }
        }

        private async Task<double> GetBulgeFromAngle(Point3D startPoint, Point3D endPoint)
        {
            var angleResult = await GetAngle(
                "Specify included angle",
                defaultValue: null);

            if (double.IsNaN(angleResult.DoubleValue))
            {
                Context?.OutputMessage("Invalid angle.");
                return 0;
            }

            double angle = angleResult.DoubleValue;
            double bulge = GeometricCalculator.GetBulgeFromAngle(angle);
            
            Context?.OutputMessage($"Arc segment with angle = {angle * 180 / Math.PI:F1}°");
            return bulge;
        }

        private async Task<double> GetBulgeFromCenter(Point3D startPoint, Point3D endPoint)
        {
            var centerResult = await GetPoint(
                "Specify center point",
                allowLastPoint: false);

            if (centerResult == null || centerResult.Point is not Point3D center)
            {
                Context?.OutputMessage("Invalid center point.");
                return 0;
            }

            double bulge = GeometricCalculator.GetBulgeFromCenter(startPoint, endPoint, center);
            
            Context?.OutputMessage($"Arc segment with center at ({center.X:F3}, {center.Y:F3}, {center.Z:F3})");
            return bulge;
        }

        private async Task<double> GetBulgeFromDirection(Point3D startPoint, Point3D endPoint)
        {
            var directionResult = await GetAngle(
                "Specify tangent direction",
                defaultValue: null);

            if (double.IsNaN(directionResult.DoubleValue))
            {
                Context?.OutputMessage("Invalid direction.");
                return 0;
            }

            double direction = directionResult.DoubleValue;
            double bulge = GeometricCalculator.GetBulgeFromDirection(startPoint, endPoint, direction);
            
            Context?.OutputMessage($"Arc segment with direction = {direction * 180 / Math.PI:F1}°");
            return bulge;
        }

        private async Task<double> GetBulgeFromSecondPoint(Point3D startPoint, Point3D endPoint)
        {
            var secondPointResult = await GetPoint(
                "Specify second point on arc",
                allowLastPoint: false);

            if (secondPointResult == null || secondPointResult.Point is not Point3D secondPoint)
            {
                Context?.OutputMessage("Invalid second point.");
                return 0;
            }

            double bulge = GeometricCalculator.GetBulgeFromVertices(startPoint, endPoint, secondPoint);
            
            Context?.OutputMessage($"Arc segment through point ({secondPoint.X:F3}, {secondPoint.Y:F3}, {secondPoint.Z:F3})");
            return bulge;
        }

        public override bool ProcessInput(string input)
        {
            if (_inputHelper != null)
            {
               _inputHelper.ProcessKeyboardInput(input);
            }

            return false;
        }

        private void CreatePolyline()
        {
            if (_currentPolyline == null)
                return;

            var document = Context?.GetDocument();
            if (document == null)
            {
                throw new InvalidOperationException("No active document to create polyline in.");
            }

            var polylineToAdd = _currentPolyline;
            var vertexCount = _currentPolyline.VertexCount;
            var length = _currentPolyline.Length;

            var undoManager = Context?.GetUndoRedoManager();

            if (undoManager != null)
            {
                Context?.PostToUI(() =>
                {
                    var viewport = Context.GetActiveViewport();
                    var action = new Undo.AddGeometryAction(
                        polylineToAdd,  // Use local variable instead of field
                        document,
                        $"Create Polyline with {vertexCount} vertices"
                    );
                    undoManager.ExecuteAction(action);
                });
            }
            else
            {
                Context?.RaiseGeometryCreated(polylineToAdd);  // Use local variable
            }

            Context?.OutputMessage(
                $"Polyline created with {vertexCount} vertices, " +
                $"Length: {length:F3}");
        }

        public override void Cancel()
        {
            base.Cancel();
            _cancellationTokenSource?.Cancel();
            
            // Clean up preview when cancelled (on UI thread)
            if (_currentPolyline != null)
            {
                var polylineToRemove = _currentPolyline;
                Context?.PostToUI(() =>
                {
                    _viewport?.RemovePreviewObject(polylineToRemove);
                });
            }
            
            _currentPolyline = null;
            _currentVertex = null;
            _viewport = null;
            CurrentPrompt = string.Empty;
        }

        private async Task SetHalfwidth()
        {
            var widthResult = await GetDistance(
                "Specify halfwidth",
                defaultValue: null);

            if (!double.IsNaN(widthResult.DoubleValue) && _currentPolyline != null)
            {
                double halfwidth = widthResult.DoubleValue;
                
                // Set halfwidth on the last vertex
                var lastVertexIndex = _currentPolyline.VertexCount - 1;
                var lastVertex = _currentPolyline.GetVertex(lastVertexIndex);
                
                if (lastVertex != null)
                {
                    lastVertex.StartWidth = halfwidth;
                    lastVertex.EndWidth = halfwidth;
                    Context?.OutputMessage($"Halfwidth set to {halfwidth:F3}");
                }
            }
            else
            {
                Context?.OutputMessage("Invalid halfwidth value.");
            }
        }
    }
}