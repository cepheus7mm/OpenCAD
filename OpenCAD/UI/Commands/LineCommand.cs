using OpenCAD;
using OpenCAD.Geometry;
using UI.Controls.Viewport;

namespace UI.Commands
{
    /// <summary>
    /// Command to create a line
    /// </summary>
    [InputCommand("line", "Create a line (prompts for start and end points or click in viewport)", "l")]
    public class LineCommand : CommandBase
    {
        private enum LineState
        {
            WaitingForStart,
            WaitingForEnd
        }

        private LineState _state;
        private Point3D? _startPoint;
        private Point3D? _previewEndPoint;
        private bool _waitingForMouseInput;
        private ViewportControl? _viewport;
        private EventHandler<PointPickedEventArgs>? _pointPickedHandler;

        public override bool IsMultiStep => true;

        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
            System.Diagnostics.Debug.WriteLine($"LineCommand initialized");
        }

        public override void Execute()
        {
            _state = LineState.WaitingForStart;
            _startPoint = null;
            _previewEndPoint = null;
            _waitingForMouseInput = false;
            _viewport = Context.GetActiveViewport();
            
            System.Diagnostics.Debug.WriteLine($"LineCommand.Execute: viewport = {(_viewport != null ? "available" : "null")}");
            
            var lastPoint = Context?.GetLastPoint();
            if (lastPoint != null)
            {
                CurrentPrompt = $"Specify start point (enter coordinates, click in viewport, or press Enter for last point {lastPoint.X:F3}, {lastPoint.Y:F3}, {lastPoint.Z:F3})";
            }
            else
            {
                CurrentPrompt = "Specify start point (enter coordinates or click in viewport)";
            }

            // Enable mouse input mode
            EnableMouseInput();
        }

        public override bool ProcessInput(string input)
        {
            if (_state == LineState.WaitingForStart)
            {
                return ProcessStartPoint(input);
            }
            else if (_state == LineState.WaitingForEnd)
            {
                return ProcessEndPoint(input);
            }

            return true;
        }

        private bool ProcessStartPoint(string input)
        {
            // Handle empty input - use last point
            if (string.IsNullOrWhiteSpace(input))
            {
                var lastPoint = Context?.GetLastPoint();
                if (lastPoint != null)
                {
                    _startPoint = lastPoint;
                    Context?.OutputMessage($"Start point: ({_startPoint.X:F3}, {_startPoint.Y:F3}, {_startPoint.Z:F3})");

                    // Store the start point in TempPoints for preview rendering
                    _viewport?.TempPoints.Add(_startPoint);
                    
                    DisableMouseInput();
                    _state = LineState.WaitingForEnd;
                    CurrentPrompt = "Specify end point (enter coordinates or click in viewport)";
                    EnableMouseInput();
                    return false;
                }
                return true;
            }

            var point = ParsePoint(input);
            if (point != null)
            {
                _startPoint = point;
                Context?.SetLastPoint(point);
                Context?.OutputMessage($"Start point: ({point.X:F3}, {point.Y:F3}, {point.Z:F3})");

                // Store the start point in TempPoints for preview rendering
                _viewport?.TempPoints.Add(point);

                DisableMouseInput();
                _state = LineState.WaitingForEnd;
                CurrentPrompt = "Specify end point (enter coordinates or click in viewport)";
                EnableMouseInput();
                return false;
            }
            else
            {
                Context?.OutputMessage("Invalid point format. Use: x y z (e.g., 0 0 0) or click in viewport");
                CurrentPrompt = "Specify start point";
                return false;
            }
        }

        private bool ProcessEndPoint(string input)
        {
            var point = ParsePoint(input);
            if (point != null)
            {
                Context?.SetLastPoint(point);
                CreateLine(_startPoint!, point);
                DisableMouseInput();
                return true; // Command complete
            }
            else
            {
                Context?.OutputMessage("Invalid point format. Use: x y z (e.g., 5 5 0) or click in viewport");
                CurrentPrompt = "Specify end point";
                return false; // Keep waiting
            }
        }

        private void CreateLine(Point3D start, Point3D end)
        {
            var line = new Line(start, end);
            
            // Use undo/redo system if available
            var undoManager = Context?.GetUndoRedoManager();
            var document = Context?.GetDocument();
            var viewport = Context?.GetActiveViewport();
            
            if (undoManager != null && document != null)
            {
                var action = new Undo.AddGeometryAction(
                    line, 
                    document, 
                    viewport, 
                    $"Create Line ({start.X:F3}, {start.Y:F3}, {start.Z:F3}) to ({end.X:F3}, {end.Y:F3}, {end.Z:F3})"
                );
                undoManager.ExecuteAction(action);
            }
            else
            {
                // Fallback to direct creation
                Context?.RaiseGeometryCreated(line);
            }
            
            Context?.OutputMessage($"Line created from ({start.X:F3}, {start.Y:F3}, {start.Z:F3}) to ({end.X:F3}, {end.Y:F3}, {end.Z:F3})");
        }

        private void EnableMouseInput()
        {
            if (_viewport != null && !_waitingForMouseInput)
            {
                System.Diagnostics.Debug.WriteLine("LineCommand: Enabling mouse input");
                _waitingForMouseInput = true;
                
                // Set up point picked handler
                _pointPickedHandler = OnPointPicked;
                _viewport.PointPicked += _pointPickedHandler;
                
                // Enable picking mode
                _viewport.EnablePointPickingMode();
                
                // Enable preview mode for rubber band line if we have a start point
                if (_startPoint != null)
                {
                    System.Diagnostics.Debug.WriteLine("LineCommand: Enabling preview mode for rubber band");
                    _viewport.EnablePreviewMode(OnPreviewPointChanged);
                }
            }
        }

        private void DisableMouseInput()
        {
            if (_viewport != null && _waitingForMouseInput)
            {
                System.Diagnostics.Debug.WriteLine("LineCommand: Disabling mouse input");
                _waitingForMouseInput = false;
                
                // Disable picking mode
                _viewport.DisablePointPickingMode();
                
                // Disable preview mode
                _viewport.DisablePreviewMode();
                
                // Clean up event handler
                if (_pointPickedHandler != null)
                {
                    _viewport.PointPicked -= _pointPickedHandler;
                    _pointPickedHandler = null;
                }
                
                // Clear preview
                _previewEndPoint = null;
            }
        }

        private void OnPointPicked(object? sender, PointPickedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"LineCommand: Point picked ({e.Point.X:F3}, {e.Point.Y:F3}, {e.Point.Z:F3})");
            
            // Temporarily disable mouse input to prevent duplicate picks
            DisableMouseInput();
            
            // Format the point as input string and process it
            string input = $"{e.Point.X} {e.Point.Y} {e.Point.Z}";
            
            // Process the point based on current state
            bool isComplete = ProcessInput(input);
            
            // If command is complete, notify the UI
            if (isComplete)
            {
                System.Diagnostics.Debug.WriteLine("LineCommand: Command completed via mouse input");
                RaiseCommandCompleted();
            }
            // Otherwise mouse input will be re-enabled by ProcessInput
        }

        private void OnPreviewPointChanged(Point3D previewPoint)
        {
            // Update the preview end point
            _previewEndPoint = previewPoint;
            
            // Update the viewport's preview point so it can be rendered
            _viewport?.SetPreviewPoint(previewPoint);
            
            System.Diagnostics.Debug.WriteLine($"LineCommand: Preview point updated to ({previewPoint.X:F3}, {previewPoint.Y:F3}, {previewPoint.Z:F3})");
        }

        public override void Cancel()
        {
            base.Cancel();
            DisableMouseInput();
            _state = LineState.WaitingForStart;
            _startPoint = null;
            _previewEndPoint = null;
            CurrentPrompt = string.Empty;
        }
    }
}