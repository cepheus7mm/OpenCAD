using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using System;
using System.ComponentModel;
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

        private enum LineSegmentStep
        {
            Point,
            Direction,
            Length,
            Width,
        }

        private enum ArcSegmentStep
        {
            SecondPoint,  // First input: second point or keyword (Radius/Angle/Center/Direction)
            EndPoint,     // Third input: endpoint (after radius/center/direction chosen)
            Radius,       // Second input: specify radius
            Angle,        // Second or Third: specify angle
            Center,       // Second input: specify center
            Direction,    // Second or Third: specify direction
            Length,       // Second or Third: specify arc length
            Width,        // Modify widths
        }

        private Polyline? _currentPolyline;
        private ViewportControl? _viewport;
        private PolylineVertex? _currentVertex;
        private PolylineVertex? _nextVertex;
        private SegmentInputState _state = new(PolylineMode.Line);

        public override bool IsMultiStep => true;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
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
                _state.CurrentPolyline = _currentPolyline;

                // Get first point
                BasePoint = Point3D.NotAPoint;
                var result = await GetPoint(
                    "Specify start point",
                    allowLastPoint: true);

                if (result == null || result.ResultType == ResType.Cancel)
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
            _nextVertex = new PolylineVertex(_currentPolyline?.Document, Point3D.NotAPoint);
            _state.SetStartWidth(_currentVertex.EndWidth);
            _state.SetEndWidth(_currentVertex.EndWidth);

            var result = await GetLineSegmentInput();

            if (result.Point is not Point3D nextPoint)
            {
                _cancellationTokenSource?.Cancel();
                return false;
            }

            // Add vertex to polyline
            _nextVertex.Position = nextPoint;
            if (_state.StartWidth.HasValue)
                _nextVertex.StartWidth = _state.StartWidth.Value;
            if (_state.EndWidth.HasValue)
                _nextVertex.EndWidth = _state.EndWidth.Value;
            _currentPolyline!.AddVertex(_nextVertex);
            _currentVertex = _nextVertex;
            _state.StartNextSegment();
            return true;
        }

        private async Task<InputResult> GetLineSegmentInput()
        {
            switch (_state.CurrentLineStep)
            {
                case LineSegmentStep.Point:
                    return await GetPolylinePoint();
                case LineSegmentStep.Direction:
                    return await GetPolylineDirection();
                case LineSegmentStep.Length:
                    return await GetPolylineLength();
                case LineSegmentStep.Width:
                    return await GetPolylineWidth();
                default:
                    return new InputResult() { ResultType = ResType.Cancel};
            }
        }

        private async Task<InputResult> GetPolylinePoint()
        {
            BasePoint = _currentVertex.Position;
            var result = await GetPoint(
                "Specify next point or press ESC to finish",
                allowLastPoint: false,
                keyWords: new[] { "Close", "Undo", "Arc", "Direction", "Length", "Width" });

            if (result.ResultType == ResType.Cancel)
            {
                // User pressed ESC - finalize the polyline
                return result;
            }

            return await ProcessLineInput(result);
        }

        private async Task<InputResult> GetPolylineDirection()
        {
            BasePoint = _currentVertex.Position;
            var result = await GetAngle(
                "Specify direction angle",
                defaultValue: null,
                keyWords: null);

            return GetPointFromLengthAndDirection(result, false);
        }

        private async Task<InputResult> GetPolylineLength()
        {
            BasePoint = _currentVertex.Position;
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
                _state.SetLength(result.DoubleValue);
            else
                _state.SetDirection(result.DoubleValue);

            // Calculate next point based on direction and length
            if (_currentVertex != null && _state.TryCalculatePoint(_currentVertex.Position, out Point3D nextPoint))
            {
                result.Point = nextPoint;
                result.ResultType = ResType.Point;
            }
            else
            {
                result.ResultType = ResType.ProcessingResult;
                result.ProcessingResult = InputResult.ProcessingResultType.RequiresMoreInput;
                return result;
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
            _state.SetStartWidth(result.DoubleValue);

            if (_state.StartWidth.HasValue)
            {
                _currentVertex.StartWidth = _state.StartWidth.Value;
                result = await GetDistance(
                    "Specify end width",
                    defaultValue: _state.StartWidth.Value,
                    keyWords: null);
                if (result.ResultType == ResType.Cancel)
                {
                    return result;
                }

                _state.SetEndWidth(result.DoubleValue);
            }

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
                    // Switch to arc segment input
                    result = await AddArcSegment();
                    // Arc segment returns with Point result when complete
                    return result;
                }
                result.ResultType = InputResult.InputResultType.Cancel;
                return result;
            }
            return result;
        }

        private async Task<InputResult> ProcessDirection(InputResult result)
        {
            _state.SetLineStep(LineSegmentStep.Direction);
            Context?.OutputMessage("Switched to Direction mode.");
            result = await GetPolylineDirection();
            if (result.ResultType == InputResult.InputResultType.ProcessingResult &&
                result.ProcessingResult == InputResult.ProcessingResultType.RequiresMoreInput)
            {
                _state.ResetToPreviousLineStep();
                return await GetLineSegmentInput();
            }
            else
            {
                _state.ResetToPreviousLineStep();
                return result;
            }
        }

        private async Task<InputResult> ProcessLength(InputResult result)
        {
            _state.SetLineStep(LineSegmentStep.Length);
            Context?.OutputMessage("Switched to Length mode.");
            result = await GetPolylineLength();
            if (result.ResultType == InputResult.InputResultType.ProcessingResult &&
                result.ProcessingResult == InputResult.ProcessingResultType.RequiresMoreInput)
            {
                _state.ResetToPreviousLineStep();
                return await GetLineSegmentInput();
            }
            else
            {
                _state.ResetToPreviousLineStep();
                return result;
            }
        }

        private async Task<InputResult> ProcessWidth(InputResult result)
        {
            _state.SetLineStep(LineSegmentStep.Width);
            Context?.OutputMessage("Switched to Width mode.");
            result = await GetPolylineWidth();
            if (result.ResultType == InputResult.InputResultType.ProcessingResult &&
                result.ProcessingResult == InputResult.ProcessingResultType.RequiresMoreInput)
            {
                _state.ResetToPreviousLineStep();
                return await GetLineSegmentInput();
            }
            else
            {
                _state.ResetToPreviousLineStep();
                return result;
            }
        }

        #region Arc Segment Methods

        /// <summary>
        /// Main entry point for arc segment input. Returns InputResult with Point when complete.
        /// </summary>
        private async Task<InputResult> AddArcSegment()
        {
            if (_currentPolyline == null || _currentVertex == null)
            {
                return new InputResult() { ResultType = ResType.Cancel };
            }

            // Switch to arc mode
            _state.SwitchMode(PolylineMode.Arc);
            BasePoint = _currentVertex.Position;

            // Create and add _nextVertex for preview (similar to line segments)
            _nextVertex = new PolylineVertex(_currentPolyline?.Document, Point3D.NotAPoint);
            _state.SetStartWidth(_currentVertex.EndWidth);
            _state.SetEndWidth(_currentVertex.EndWidth);
            _currentPolyline.AddVertex(_nextVertex);

            // Setup arc preview handler
            PropertyChangedEventHandler? arcPreviewHandler = null;
            var viewModel = Context?.GetActiveViewportViewModel();
            
            if (viewModel != null)
            {
                arcPreviewHandler = (sender, e) =>
                {
                    if (e.PropertyName == nameof(ViewportViewModel.PreviewPoint))
                    {
                        UpdateArcPreview(viewModel.PreviewPoint);
                    }
                };

                // Subscribe on UI thread
                Context?.PostToUI(() =>
                {
                    viewModel.PropertyChanged += arcPreviewHandler;
                });
            }

            try
            {
                // Get arc input (will recursively handle all steps)
                var result = await GetArcSegmentInput();

                // If cancelled or failed, remove the temporary vertex
                if (result.ResultType == ResType.Cancel)
                {
                    _currentPolyline.RemoveVertex(_currentPolyline.VertexCount - 1);
                    _nextVertex = null;
                    _state.SwitchMode(PolylineMode.Line);
                    return result;
                }

                // Get endpoint (might be direct or calculated)
                if (!_state.TryGetArcEndpoint(_currentVertex.Position, out Point3D arcEndpoint))
                {
                    Context?.OutputMessage("Cannot determine arc endpoint.");
                    _currentPolyline.RemoveVertex(_currentPolyline.VertexCount - 1);
                    _nextVertex = null;
                    _state.SwitchMode(PolylineMode.Line);
                    result.ResultType = ResType.Cancel;
                    return result;
                }

                // Calculate bulge
                if (!_state.TryCalculateBulge(_currentVertex.Position, out double bulge))
                {
                    Context?.OutputMessage("Invalid arc parameters. Using straight line.");
                    bulge = 0;
                }

                // Set bulge on CURRENT vertex (arc info lives on start vertex)
                _currentVertex.Bulge = bulge;

                // Update _nextVertex with final values
                _nextVertex.Position = arcEndpoint;
                if (_state.StartWidth.HasValue)
                    _nextVertex.StartWidth = _state.StartWidth.Value;
                if (_state.EndWidth.HasValue)
                    _nextVertex.EndWidth = _state.EndWidth.Value;

                // Update current vertex for next segment
                _currentVertex = _nextVertex;

                // Return point result with endpoint
                result.Point = arcEndpoint;
                result.ResultType = ResType.Point;

                Context?.OutputMessage($"Arc segment added with bulge = {bulge:F3}");

                // Return to line mode for next segment
                _state.SwitchMode(PolylineMode.Line);

                return result;
            }
            finally
            {
                // Unsubscribe from preview updates (on UI thread)
                if (viewModel != null && arcPreviewHandler != null)
                {
                    Context?.PostToUI(() =>
                    {
                        viewModel.PropertyChanged -= arcPreviewHandler;
                    });
                }

                // Refresh one final time
                RefreshPreview();
            }
        }

        /// <summary>
        /// Update arc preview as mouse moves
        /// </summary>
        private void UpdateArcPreview(Point3D? previewPoint)
        {
            if (previewPoint == null || 
                _currentVertex == null || 
                _nextVertex == null ||
                _currentPolyline == null)
                return;

            // Update the temporary endpoint
            _nextVertex.Position = previewPoint.Value;

            // Try to calculate bulge with current constraints + mouse position
            try
            {
                // Temporarily set endpoint for preview calculation
                var originalEndpoint = _state.ArcEndPoint;
                _state.SetArcEndpoint(previewPoint.Value);

                // Only calculate bulge if we have enough constraints
                if (_state.IsArcSegmentComplete())
                {
                    if (_state.TryCalculateBulge(_currentVertex.Position, out double previewBulge))
                    {
                        _currentVertex.Bulge = previewBulge;
                    }
                    else
                    {
                        // Can't calculate yet - show straight line
                        _currentVertex.Bulge = 0;
                    }
                }
                else
                {
                    // Not enough constraints - show rubberband line
                    _currentVertex.Bulge = 0;
                }

                // Restore original endpoint state (don't commit preview point to state)
                _state.SetArcEndpoint(originalEndpoint ?? Point3D.NotAPoint);
                if (!originalEndpoint.HasValue)
                {
                    // Clear it if it wasn't set before
                    _state.ClearArcEndpoint();
                }
            }
            catch
            {
                // If calculation fails, show straight line
                _currentVertex.Bulge = 0;
            }

            // Trigger viewport refresh (on UI thread)
            RefreshPreview();
        }

        /// <summary>
        /// Step-based router for arc segment input
        /// </summary>
        private async Task<InputResult> GetArcSegmentInput()
        {
            switch (_state.CurrentArcStep)
            {
                case ArcSegmentStep.SecondPoint:
                    return await GetArcSecondPoint();
                case ArcSegmentStep.EndPoint:
                    return await GetArcEndpoint();
                case ArcSegmentStep.Radius:
                    return await GetArcRadius();
                case ArcSegmentStep.Angle:
                    return await GetArcAngle();
                case ArcSegmentStep.Center:
                    return await GetArcCenter();
                case ArcSegmentStep.Direction:
                    return await GetArcDirection();
                case ArcSegmentStep.Length:
                    return await GetArcLength();
                case ArcSegmentStep.Width:
                    return await GetPolylineWidth(); // Reuse line width logic
                default:
                    return new InputResult() { ResultType = ResType.Cancel };
            }
        }

        /// <summary>
        /// Get second point (first arc input) - allows point OR keywords
        /// </summary>
        private async Task<InputResult> GetArcSecondPoint()
        {
            BasePoint = _currentVertex.Position;
            
            // Build keyword list dynamically
            var keywords = new List<string> { "Angle", "ArcLength", "Center", "Direction", "Line", "Radius", "Width" };
            
            // Add Close if we have at least 2 vertices (current + first)
            if (_currentPolyline != null && _currentPolyline.VertexCount >= 2)
            {
                keywords.Insert(0, "Close"); // Add at beginning for visibility
            }
            
            // Add Undo if we have more than 1 vertex
            if (_currentPolyline != null && _currentPolyline.VertexCount > 1)
            {
                keywords.Insert(keywords.Contains("Close") ? 1 : 0, "Undo");
            }
            
            var result = await GetPoint(
                "Specify second point",
                allowLastPoint: false,
                keyWords: keywords.ToArray());

            if (result.ResultType == ResType.Cancel)
            {
                return result;
            }

            return await ProcessArcInput(result);
        }

        /// <summary>
        /// Get arc endpoint with dynamic keyword support
        /// </summary>
        private async Task<InputResult> GetArcEndpoint()
        {
            BasePoint = _currentVertex.Position;

            // Get dynamic keywords based on current state
            var keywords = GetAvailableEndpointKeywords();

            var result = await GetPoint(
                "Specify endpoint of arc",
                allowLastPoint: false,
                keyWords: keywords);

            if (result.ResultType == ResType.Cancel)
            {
                return result;
            }

            return await ProcessArcEndpointInput(result);
        }

        /// <summary>
        /// Get available keywords for endpoint prompt based on current constraints
        /// </summary>
        private string[] GetAvailableEndpointKeywords()
        {
            var keywords = new List<string> { "Line" }; // Always allow returning to line mode

            // Add keywords for constraints that aren't set yet
            if (!_state.IncludedAngle.HasValue)
                keywords.Add("Angle");

            if (!_state.CenterPoint.HasValue)
                keywords.Add("Center");

            if (!_state.TangentDirection.HasValue)
                keywords.Add("Direction");

            if (!_state.ArcLength.HasValue)
                keywords.Add("ArcLength");

            if (!_state.Radius.HasValue)
                keywords.Add("Radius");

            return keywords.ToArray();
        }

        /// <summary>
        /// Process endpoint input (point or keyword)
        /// </summary>
        private async Task<InputResult> ProcessArcEndpointInput(InputResult result)
        {
            if (result.ResultType == ResType.Keyword)
            {
                var keyword = result.Keyword?.ToUpperInvariant();

                if (keyword == "L" || keyword == "LINE")
                {
                    Context?.OutputMessage("Returning to line mode.");
                    _state.SwitchMode(PolylineMode.Line);
                    result.ResultType = ResType.Cancel;
                    return result;
                }
                else if (keyword == "A" || keyword == "ANGLE")
                {
                    return await ProcessArcAngle();
                }
                else if (keyword == "CE" || keyword == "CENTER")
                {
                    return await ProcessArcCenter();
                }
                else if (keyword == "D" || keyword == "DIRECTION")
                {
                    return await ProcessArcDirection();
                }
                else if (keyword == "AL" || keyword == "ARCLENGTH")
                {
                    return await ProcessArcLength();
                }
                else if (keyword == "R" || keyword == "RADIUS")
                {
                    return await ProcessArcRadius();
                }

                result.ResultType = ResType.Cancel;
                return result;
            }
            else if (result.ResultType == ResType.Point)
            {
                // User specified endpoint
                if (result.Point is Point3D endpoint)
                {
                    _state.SetArcEndpoint(endpoint);

                    // Check if arc is complete now
                    if (_state.IsArcSegmentComplete())
                    {
                        result.ResultType = ResType.Point;
                        result.Point = endpoint;
                        return result;
                    }
                    else
                    {
                        // Need more input - determine what's needed
                        var nextStep = _state.GetNextRequiredArcStep();
                        _state.SetArcStep(nextStep);
                        return await GetArcSegmentInput();
                    }
                }
            }

            result.ResultType = ResType.Cancel;
            return result;
        }

        /// <summary>
        /// Get arc included angle value
        /// </summary>
        private async Task<InputResult> GetArcAngle()
        {
            var result = await GetAngle(
                "Specify included angle",
                defaultValue: null,
                keyWords: null);

            if (result.ResultType != ResType.Double)
            {
                result.ResultType = ResType.Cancel;
                return result;
            }

            _state.SetIncludedAngle(result.DoubleValue);

            // Check if we need direction (Radius + Angle case without endpoint)
            if (_state.Radius.HasValue && !_state.ArcEndPoint.HasValue && !_state.TangentDirection.HasValue)
            {
                // Try to get direction from polyline if we have segments
                if (!_state.TryGetDirectionFromPolyline(_currentVertex!.Position))
                {
                    // Need user to provide direction
                    Context?.OutputMessage("Direction needed for radius + angle arc.");
                    _state.SetArcStep(ArcSegmentStep.Direction);
                    return await GetArcSegmentInput();
                }
            }

            // Determine next step
            if (_state.IsArcSegmentComplete())
            {
                // Arc is complete - try to calculate endpoint
                if (_state.TryGetArcEndpoint(_currentVertex!.Position, out Point3D endpoint))
                {
                    result.Point = endpoint;
                    result.ResultType = ResType.Point;
                    return result;
                }
            }

            // Need more input
            var nextStep = _state.GetNextRequiredArcStep();
            _state.SetArcStep(nextStep);
            return await GetArcSegmentInput();
        }

        /// <summary>
        /// Get arc length value
        /// </summary>
        private async Task<InputResult> GetArcLength()
        {
            var result = await GetDistance(
                "Specify arc length",
                defaultValue: null,
                keyWords: null);

            if (result.ResultType != ResType.Double)
            {
                result.ResultType = ResType.Cancel;
                return result;
            }

            _state.SetArcLength(result.DoubleValue);

            // Check if we need direction (Radius + ArcLength case without endpoint)
            if (_state.Radius.HasValue && !_state.ArcEndPoint.HasValue && !_state.TangentDirection.HasValue)
            {
                // Try to get direction from polyline if we have segments
                if (!_state.TryGetDirectionFromPolyline(_currentVertex!.Position))
                {
                    // Need user to provide direction
                    Context?.OutputMessage("Direction needed for radius + arc length arc.");
                    _state.SetArcStep(ArcSegmentStep.Direction);
                    return await GetArcSegmentInput();
                }
            }

            // Determine next step
            if (_state.IsArcSegmentComplete())
            {
                // Arc is complete - try to calculate endpoint
                if (_state.TryGetArcEndpoint(_currentVertex!.Position, out Point3D endpoint))
                {
                    result.Point = endpoint;
                    result.ResultType = ResType.Point;
                    return result;
                }
            }

            // Need more input
            var nextStep = _state.GetNextRequiredArcStep();
            _state.SetArcStep(nextStep);
            return await GetArcSegmentInput();
        }

        /// <summary>
        /// Get arc radius value
        /// </summary>
        private async Task<InputResult> GetArcRadius()
        {
            var result = await GetDistance(
                "Specify radius",
                defaultValue: null,
                keyWords: null);

            if (result.ResultType != ResType.Double)
            {
                result.ResultType = ResType.Cancel;
                return result;
            }

            _state.SetRadius(result.DoubleValue);

            // Check if we need direction (Radius + Angle/ArcLength case without endpoint)
            if (((_state.IncludedAngle.HasValue && !_state.ArcEndPoint.HasValue) ||
                 (_state.ArcLength.HasValue && !_state.ArcEndPoint.HasValue)) &&
                !_state.TangentDirection.HasValue)
            {
                // Try to get direction from polyline if we have segments
                if (!_state.TryGetDirectionFromPolyline(_currentVertex!.Position))
                {
                    // Need user to provide direction
                    Context?.OutputMessage("Direction needed for radius + angle/length arc.");
                    _state.SetArcStep(ArcSegmentStep.Direction);
                    return await GetArcSegmentInput();
                }
            }

            // Determine next step
            if (_state.IsArcSegmentComplete())
            {
                // Arc is complete - try to calculate endpoint
                if (_state.TryGetArcEndpoint(_currentVertex!.Position, out Point3D endpoint))
                {
                    result.Point = endpoint;
                    result.ResultType = ResType.Point;
                    return result;
                }
            }

            // Need more input
            var nextStep = _state.GetNextRequiredArcStep();
            _state.SetArcStep(nextStep);
            return await GetArcSegmentInput();
        }

        /// <summary>
        /// Get arc center point
        /// </summary>
        private async Task<InputResult> GetArcCenter()
        {
            BasePoint = _currentVertex.Position;
            var result = await GetPoint(
                "Specify center point",
                allowLastPoint: false,
                keyWords: null);

            if (result.ResultType == ResType.Cancel)
            {
                return result;
            }

            if (result.Point is Point3D center)
            {
                _state.SetCenterPoint(center);

                // Determine next step
                if (_state.IsArcSegmentComplete())
                {
                    // Arc is complete
                    if (_state.TryGetArcEndpoint(_currentVertex!.Position, out Point3D endpoint))
                    {
                        result.Point = endpoint;
                        result.ResultType = ResType.Point;
                        return result;
                    }
                }

                // Need more input
                var nextStep = _state.GetNextRequiredArcStep();
                _state.SetArcStep(nextStep);
                return await GetArcSegmentInput();
            }

            result.ResultType = ResType.Cancel;
            return result;
        }

        /// <summary>
        /// Get arc tangent direction
        /// </summary>
        private async Task<InputResult> GetArcDirection()
        {
            var result = await GetAngle(
                "Specify tangent direction",
                defaultValue: null,
                keyWords: null);

            if (result.ResultType != ResType.Double)
            {
                result.ResultType = ResType.Cancel;
                return result;
            }

            _state.SetTangentDirection(result.DoubleValue);

            // Determine next step
            if (_state.IsArcSegmentComplete())
            {
                // Arc is complete
                if (_state.TryGetArcEndpoint(_currentVertex!.Position, out Point3D endpoint))
                {
                    result.Point = endpoint;
                    result.ResultType = ResType.Point;
                    return result;
                }
            }

            // Need more input
            var nextStep = _state.GetNextRequiredArcStep();
            _state.SetArcStep(nextStep);
            return await GetArcSegmentInput();
        }

        /// <summary>
        /// Process arc input - handles keywords and point input at first prompt
        /// </summary>
        private async Task<InputResult> ProcessArcInput(InputResult result)
        {
            if (result.ResultType == ResType.Keyword)
            {
                var keyword = result.Keyword?.ToUpperInvariant();

                if (keyword == "C" || keyword == "CLOSE")
                {
                    return await ProcessArcClose();
                }
                else if (keyword == "U" || keyword == "UNDO")
                {
                    return await ProcessArcUndo();
                }
                else if (keyword == "L" || keyword == "LINE")
                {
                    Context?.OutputMessage("Returning to line mode.");
                    _state.SwitchMode(PolylineMode.Line);
                    result.ResultType = ResType.Cancel;
                    return result;
                }
                else if (keyword == "W" || keyword == "WIDTH")
                {
                    return await ProcessArcWidth();
                }
                else if (keyword == "A" || keyword == "ANGLE")
                {
                    return await ProcessArcAngle();
                }
                else if (keyword == "CE" || keyword == "CENTER")
                {
                    return await ProcessArcCenter();
                }
                else if (keyword == "D" || keyword == "DIRECTION")
                {
                    return await ProcessArcDirection();
                }
                else if (keyword == "AL" || keyword == "ARCLENGTH")
                {
                    return await ProcessArcLength();
                }
                else if (keyword == "R" || keyword == "RADIUS")
                {
                    return await ProcessArcRadius();
                }

                result.ResultType = ResType.Cancel;
                return result;
            }
            else if (result.ResultType == ResType.Point)
            {
                // User specified second point directly (three-point arc)
                _state.SetSecondPoint((Point3D)result.Point!);

                // Now need endpoint
                _state.SetArcStep(ArcSegmentStep.EndPoint);
                return await GetArcSegmentInput();
            }

            return result;
        }

        /// <summary>
        /// Process Arc → Close keyword - creates tangent arc to first vertex
        /// </summary>
        private async Task<InputResult> ProcessArcClose()
        {
            if (_currentPolyline == null || _currentVertex == null)
            {
                return new InputResult() { ResultType = ResType.Cancel };
            }

            // Verify we have at least 2 vertices
            if (_currentPolyline.VertexCount < 2)
            {
                Context?.OutputMessage("Cannot close - need at least 2 vertices.");
                return new InputResult() { ResultType = ResType.Cancel };
            }

            try
            {
                // Get first vertex position (endpoint for closing arc)
                var firstVertex = _currentPolyline.GetVertex(0);
                if (firstVertex == null)
                {
                    Context?.OutputMessage("Cannot find first vertex.");
                    return new InputResult() { ResultType = ResType.Cancel };
                }

                Point3D endPoint = firstVertex.Position;

                // Get tangent direction at current vertex
                var parameter = _currentPolyline.GetParameterAtPoint(_currentVertex.Position);
                var tangent = _currentPolyline.GetFirstDerivativeAtParameter(parameter);
                //if (!tangent.HasValue)
                //{
                //    Context?.OutputMessage("Cannot determine tangent direction. Using straight line to close.");
                //    _currentVertex.Bulge = 0.0;
                //    _currentPolyline.Close();
                //    Context?.OutputMessage("Polyline closed with straight segment.");

                //    // Return cancel to exit arc mode
                //    return new InputResult() { ResultType = ResType.Cancel };
                //}

                // Use ArcSolver to calculate arc with tangent constraint
                var solution = ArcSolver.SolveArc(
                    start: _currentVertex.Position,
                    end: endPoint,
                    center: null,
                    radius: null,
                    includedAngle: null,
                    arcLength: null,
                    direction: tangent,
                    bulge: null);

                if (!solution.IsSolved)
                {
                    Context?.OutputMessage("Cannot solve tangent arc. Using straight line to close.");
                    _currentVertex.Bulge = 0.0;
                    _currentPolyline.Close();
                    Context?.OutputMessage("Polyline closed with straight segment.");

                    // Return cancel to exit arc mode
                    return new InputResult() { ResultType = ResType.Cancel };
                }

                // Set the bulge on current vertex
                _currentVertex.Bulge = solution.Bulge;

                // Close the polyline
                _currentPolyline.Close();

                Context?.OutputMessage($"Polyline closed with tangent arc (bulge = {solution.Bulge:F3}).");

                // Refresh preview
                RefreshPreview();

                // Return cancel to complete the command
                return new InputResult() { ResultType = ResType.Cancel };
            }
            catch (Exception ex)
            {
                Context?.OutputMessage($"Error creating closing arc: {ex.Message}. Using straight line.");
                _currentVertex.Bulge = 0.0;
                _currentPolyline.Close();
                Context?.OutputMessage("Polyline closed with straight segment.");

                return new InputResult() { ResultType = ResType.Cancel };
            }
        }

        /// <summary>
        /// Process Arc → Undo keyword - removes last vertex and returns to previous mode
        /// </summary>
        private async Task<InputResult> ProcessArcUndo()
        {
            if (_currentPolyline == null)
            {
                return new InputResult() { ResultType = ResType.Cancel };
            }

            // Remove the temporary vertex we added when entering arc mode
            if (_nextVertex != null && _currentPolyline.VertexCount > 1)
            {
                _currentPolyline.RemoveVertex(_currentPolyline.VertexCount - 1);
                _nextVertex = null;
            }

            // Now remove the actual last vertex (if more than 1 remains)
            if (_currentPolyline.VertexCount > 1)
            {
                // Get the previous vertex before removing
                var prevVertex = _currentPolyline.GetVertex(_currentPolyline.VertexCount - 2);

                _currentPolyline.RemoveVertex(_currentPolyline.VertexCount - 1);

                if (prevVertex != null)
                {
                    _currentVertex = prevVertex;
                    Context?.OutputMessage("Last vertex removed.");

                    // Refresh preview after undo
                    RefreshPreview();
                }
            }
            else
            {
                Context?.OutputMessage("Cannot undo - only one vertex remains.");
            }

            // Switch back to line mode and cancel arc input
            _state.SwitchMode(PolylineMode.Line);

            return new InputResult() { ResultType = ResType.Cancel };
        }

        /// <summary>
        /// Process Arc → Length keyword
        /// </summary>
        private async Task<InputResult> ProcessArcLength()
        {
            _state.SetArcStep(ArcSegmentStep.Length);
            Context?.OutputMessage("Switched to Length mode.");

            var result = await GetArcLength();

            // Result will have determined next step internally
            return result;
        }

        /// <summary>
        /// Process Arc → Width keyword
        /// </summary>
        private async Task<InputResult> ProcessArcWidth()
        {
            _state.SetArcStep(ArcSegmentStep.Width);
            Context?.OutputMessage("Switched to Width mode.");
            
            var result = await GetPolylineWidth();
            
            if (result.ResultType == ResType.ProcessingResult &&
                result.ProcessingResult == InputResult.ProcessingResultType.RequiresMoreInput)
            {
                _state.ResetToPreviousArcStep();
                return await GetArcSegmentInput();
            }
            else
            {
                _state.ResetToPreviousArcStep();
                return result;
            }
        }

        /// <summary>
        /// Process Arc → Angle keyword
        /// </summary>
        private async Task<InputResult> ProcessArcAngle()
        {
            _state.SetArcStep(ArcSegmentStep.Angle);
            Context?.OutputMessage("Switched to Angle mode.");
            
            var result = await GetArcAngle();
            
            // Result will have determined next step internally
            return result;
        }

        /// <summary>
        /// Process Arc → Center keyword
        /// </summary>
        private async Task<InputResult> ProcessArcCenter()
        {
            _state.SetArcStep(ArcSegmentStep.Center);
            Context?.OutputMessage("Switched to Center mode.");
            
            var result = await GetArcCenter();
            
            // Result will have determined next step internally
            return result;
        }

        /// <summary>
        /// Process Arc → Direction keyword
        /// </summary>
        private async Task<InputResult> ProcessArcDirection()
        {
            _state.SetArcStep(ArcSegmentStep.Direction);
            Context?.OutputMessage("Switched to Direction mode.");
            
            var result = await GetArcDirection();
            
            // Result will have determined next step internally
            return result;
        }

        /// <summary>
        /// Process Arc → Radius keyword
        /// </summary>
        private async Task<InputResult> ProcessArcRadius()
        {
            _state.SetArcStep(ArcSegmentStep.Radius);
            Context?.OutputMessage("Switched to Radius mode.");
            
            var result = await GetArcRadius();
            
            // Result will have determined next step internally
            return result;
        }

        #endregion

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
                        polylineToAdd,
                        document,
                        $"Create Polyline with {vertexCount} vertices"
                    );
                    undoManager.ExecuteAction(action);
                });
            }
            else
            {
                Context?.RaiseGeometryCreated(polylineToAdd);
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

        /// <summary>
        /// Manages state for polyline segment creation, handling both line and arc segments.
        /// Acts as single source of truth for pending input data AND current step.
        /// </summary>
        private class SegmentInputState
        {
            public Polyline CurrentPolyline { get; set; }

            #region Current Step Tracking

            public PolylineMode CurrentMode { get; private set; }
            public LineSegmentStep CurrentLineStep { get; private set; }
            public ArcSegmentStep CurrentArcStep { get; private set; }

            private LineSegmentStep _previousLineStep;
            private ArcSegmentStep _previousArcStep;

            #endregion

            #region Line Segment State

            public double? DirectionAngle { get; private set; }
            public double? Length { get; private set; }

            #endregion

            #region Arc Segment State

            #region Arc Segment State

            public Point3D? ArcEndPoint { get; private set; }
            public double? Radius { get; private set; }
            public double? IncludedAngle { get; private set; }
            public Point3D? CenterPoint { get; private set; }
            public Vector3D? TangentDirection { get; private set; }
            public Point3D? SecondPoint { get; private set; }
            public double? ArcLength { get; private set; }

            #endregion

            #endregion

            #region Width State (Mode-Agnostic)

            public double? StartWidth { get; private set; }
            public double? EndWidth { get; private set; }

            #endregion

            #region Constructor

            public SegmentInputState(PolylineMode initialMode = PolylineMode.Line)
            {
                CurrentMode = initialMode;
                CurrentLineStep = LineSegmentStep.Point;
                CurrentArcStep = ArcSegmentStep.SecondPoint;
            }

            #endregion

            #region Mode and Step Management

            public void SwitchMode(PolylineMode mode)
            {
                if (mode != CurrentMode)
                {
                    ClearModeSpecificState();
                    CurrentMode = mode;

                    if (mode == PolylineMode.Line)
                        CurrentLineStep = LineSegmentStep.Point;
                    else
                        CurrentArcStep = ArcSegmentStep.SecondPoint;
                }
            }

            public void SetLineStep(LineSegmentStep step)
            {
                ValidateMode(PolylineMode.Line, "SetLineStep");
                _previousLineStep = CurrentLineStep;
                CurrentLineStep = step;
            }

            public void ResetToPreviousLineStep()
            {
                ValidateMode(PolylineMode.Line, "ResetToPreviousLineStep");
                CurrentLineStep = _previousLineStep;
            }

            public void ResetToPointStep()
            {
                if (CurrentMode == PolylineMode.Line)
                    CurrentLineStep = LineSegmentStep.Point;
            }

            public void SetArcStep(ArcSegmentStep step)
            {
                ValidateMode(PolylineMode.Arc, "SetArcStep");
                _previousArcStep = CurrentArcStep;
                CurrentArcStep = step;
            }

            public void ResetToPreviousArcStep()
            {
                ValidateMode(PolylineMode.Arc, "ResetToPreviousArcStep");
                CurrentArcStep = _previousArcStep;
            }

            public string GetCurrentStepDescription()
            {
                if (CurrentMode == PolylineMode.Line)
                    return $"Line/{CurrentLineStep}";
                else
                    return $"Arc/{CurrentArcStep}";
            }

            private void ClearModeSpecificState()
            {
                if (CurrentMode == PolylineMode.Line)
                {
                    DirectionAngle = null;
                    Length = null;
                }
                else
                {
                    ArcEndPoint = null;
                    Radius = null;
                    IncludedAngle = null;
                    CenterPoint = null;
                    TangentDirection = null;
                    SecondPoint = null;
                    Length = null;
                }
            }

            public void StartNextSegment()
            {
                DirectionAngle = null;
                Length = null;
                ArcEndPoint = null;
                Radius = null;
                IncludedAngle = null;
                CenterPoint = null;
                TangentDirection = null;
                SecondPoint = null;

                ResetToPointStep();
            }

            #endregion

            #region Line Segment Methods

            public void SetDirection(double angle)
            {
                ValidateMode(PolylineMode.Line, "SetDirection");
                DirectionAngle = angle;
            }

            public void SetLength(double length)
            {
                ValidateMode(PolylineMode.Line, "SetLength");
                Length = length;
            }

            public bool IsLineSegmentComplete()
            {
                return DirectionAngle.HasValue && Length.HasValue;
            }

            public LineSegmentStep GetNextRequiredLineStep()
            {
                if (DirectionAngle.HasValue && !Length.HasValue)
                    return LineSegmentStep.Length;

                if (Length.HasValue && !DirectionAngle.HasValue)
                    return LineSegmentStep.Direction;

                return LineSegmentStep.Point;
            }

            public bool TryCalculatePoint(Point3D startPoint, out Point3D point)
            {
                point = Point3D.Origin;

                if (!IsLineSegmentComplete())
                    return false;

                var directionVector = new Vector3D(
                    Math.Cos(DirectionAngle!.Value),
                    Math.Sin(DirectionAngle!.Value),
                    0).Normalized;

                point = startPoint + directionVector * Length!.Value;
                return true;
            }

            #endregion

            #region Arc Segment Methods

            public void SetArcEndpoint(Point3D endpoint)
            {
                ValidateMode(PolylineMode.Arc, "SetArcEndpoint");
                ArcEndPoint = endpoint;
            }

            /// <summary>
            /// Clear the arc endpoint (for preview rollback)
            /// </summary>
            public void ClearArcEndpoint()
            {
                ValidateMode(PolylineMode.Arc, "ClearArcEndpoint");
                ArcEndPoint = null;
            }

            public void SetRadius(double radius)
            {
                ValidateMode(PolylineMode.Arc, "SetRadius");
                Radius = radius;
            }

            public void SetIncludedAngle(double angle)
            {
                ValidateMode(PolylineMode.Arc, "SetIncludedAngle");
                IncludedAngle = angle;
            }

            public void SetCenterPoint(Point3D center)
            {
                ValidateMode(PolylineMode.Arc, "SetCenterPoint");
                CenterPoint = center;
            }

            public void SetTangentDirection(double angleInRadians)
            {
                ValidateMode(PolylineMode.Arc, "SetTangentDirection");
                // Convert angle to vector
                TangentDirection = new Vector3D(
                    Math.Cos(angleInRadians),
                    Math.Sin(angleInRadians),
                    0);
            }

            public void SetArcLength(double length)
            {
                ValidateMode(PolylineMode.Arc, "SetArcLength");
                Length = length;
            }

            public void SetSecondPoint(Point3D point)
            {
                ValidateMode(PolylineMode.Arc, "SetSecondPoint");
                SecondPoint = point;
            }

            public bool IsArcSegmentComplete()
            {
                // Three-point arc: second point + endpoint
                if (SecondPoint.HasValue && (ArcEndPoint.HasValue))
                    return true;

                // Endpoint + one definition method
                if ((ArcEndPoint.HasValue) && (Radius.HasValue || IncludedAngle.HasValue || CenterPoint.HasValue || TangentDirection.HasValue || Length.HasValue))
                    return true;

                // Special case: Center + Angle
                if (CenterPoint.HasValue && IncludedAngle.HasValue)
                    return true;

                // Special case: Center + Radius
                if (CenterPoint.HasValue && Radius.HasValue)
                    return true;

                // Special case: Center + ArcLength
                if (CenterPoint.HasValue && Length.HasValue)
                    return true;

                // Special case: Radius + Angle + Direction
                if (Radius.HasValue && IncludedAngle.HasValue && TangentDirection.HasValue)
                    return true;

                // Special case: Radius + ArcLength + Direction
                if (Radius.HasValue && Length.HasValue && TangentDirection.HasValue)
                    return true;

                // Special case: IncludedAngle + ArcLength + Direction
                if (IncludedAngle.HasValue && Length.HasValue && TangentDirection.HasValue)
                    return true;

                return false;
            }

            /// <summary>
            /// Determine what arc input is needed next based on current state
            /// </summary>
            public ArcSegmentStep GetNextRequiredArcStep()
            {
                // If we have second point but not endpoint, need endpoint
                if (SecondPoint.HasValue)
                    return ArcSegmentStep.EndPoint;

                // If we have radius but not endpoint, need endpoint
                if (Radius.HasValue && !(ArcEndPoint.HasValue))
                    return ArcSegmentStep.EndPoint;

                // If we have center but not angle or radius, need angle
                if (CenterPoint.HasValue && !IncludedAngle.HasValue && !Radius.HasValue)
                    return ArcSegmentStep.Angle;

                // If we have direction but not endpoint, need endpoint
                if (TangentDirection.HasValue && !(ArcEndPoint.HasValue))
                    return ArcSegmentStep.EndPoint;

                // If we have angle but not endpoint, need endpoint
                if (IncludedAngle.HasValue && !(ArcEndPoint.HasValue))
                    return ArcSegmentStep.EndPoint;

                // Default: back to second point
                return ArcSegmentStep.SecondPoint;
            }

            public bool TryCalculateBulge(Point3D startPoint, out double bulge)
            {
                bulge = 0;

                if (!IsArcSegmentComplete())
                    return false;

                try
                {
                    // Special case: Three-point arc uses geometric calculator directly
                    if (SecondPoint.HasValue && ArcEndPoint.HasValue)
                    {
                        bulge = GeometricCalculator.GetBulgeFromThreePoints(startPoint, SecondPoint.Value, ArcEndPoint.Value);
                        return true;
                    }


                    // Let ArcSolver figure out which case to use based on available constraints
                    var solution = GetArcSolution(startPoint);

                    bulge = solution.Bulge;
                    
                    // Cache endpoint if it was calculated
                    if (!ArcEndPoint.HasValue)
                    {
                        ArcEndPoint = solution.End;
                    }

                    return solution.IsSolved;
                }
                catch (Exception)
                {
                    // Arc solver failed - invalid constraint combination
                    return false;
                }
            }

            public bool TryGetArcEndpoint(Point3D startPoint, out Point3D endpoint)
            {
                endpoint = Point3D.Origin;

                // If endpoint already provided, use it
                if (ArcEndPoint.HasValue)
                {
                    endpoint = ArcEndPoint.Value;
                    return true;
                }

                try
                {
                    // Let ArcSolver calculate endpoint from available constraints
                    var solution = GetArcSolution(startPoint);

                    endpoint = solution.End;
                    ArcEndPoint = endpoint; // Cache it
                    return solution.IsSolved;
                }
                catch (Exception)
                {
                    // Arc solver failed - insufficient constraints or invalid combination
                    return false;
                }
            }

            /// <summary>
            /// Try to get direction from polyline's first derivative
            /// Returns true if direction was set, false if polyline has only one vertex
            /// </summary>
            public bool TryGetDirectionFromPolyline(Point3D startPoint)
            {
                if (CurrentPolyline == null || CurrentPolyline.VertexCount < 2)
                    return false;
                var parameter = CurrentPolyline.GetParameterAtPoint(startPoint);
                var direction = CurrentPolyline.GetFirstDerivativeAtParameter(parameter);
                if (direction != Vector3D.Zero)
                {
                    // Convert vector to angle
                    TangentDirection = direction;
                    return true;
                }

                return false;
            }

            private ArcSolution GetArcSolution(Point3D startPoint)
            {
                // Convert tangent direction to vector if present
                var parameter = CurrentPolyline.GetParameterAtPoint(startPoint);
                Vector3D? direction = TangentDirection ?? CurrentPolyline.GetFirstDerivativeAtParameter(parameter);

                return ArcSolver.SolveArc(
                        start: startPoint,
                        end: ArcEndPoint,  // We're trying to calculate this
                        center: CenterPoint,
                        radius: Radius,
                        includedAngle: IncludedAngle,
                        arcLength: Length,
                        direction: direction,
                        bulge: null);
            }

            #endregion

            #region Width Methods (Mode-Agnostic)

            public void SetStartWidth(double width)
            {
                StartWidth = width;
            }

            public void SetEndWidth(double width)
            {
                EndWidth = width;
            }

            public bool HasStartWidth() => StartWidth.HasValue;

            public bool HasEndWidth() => EndWidth.HasValue;

            public bool IsWidthComplete() => StartWidth.HasValue && EndWidth.HasValue;

            #endregion

            #region Validation

            private void ValidateMode(PolylineMode expectedMode, string operationName)
            {
                if (CurrentMode != expectedMode)
                {
                    throw new InvalidOperationException(
                        $"Cannot {operationName} in {CurrentMode} mode. Expected {expectedMode} mode.");
                }
            }

            #endregion
        }
    }
}