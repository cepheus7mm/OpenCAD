using OpenCAD;
using OpenCAD.Geometry;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands
{
    /// <summary>
    /// Command to create a line
    /// </summary>
    [InputCommand("line", "Create a line (prompts for start and end points or click in viewport)", "l")]
    public class LineCommand : CommandBase
    {
        private PointInputHelper? _pointInputHelper;
        private CancellationTokenSource? _cancellationTokenSource;
        private Point3D? _firstStartPoint; // Store the very first start point for closing

        public override bool IsMultiStep => true;

        public override void Initialize(ICommandContext context)
        {
            base.Initialize(context);
            _pointInputHelper = new PointInputHelper(context, context.GetActiveViewport());
            System.Diagnostics.Debug.WriteLine(OpenCADStrings.LineCommandInitialized);
        }

        public override async void Execute()
        {
            if (_pointInputHelper == null)
                return;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Get initial start point (allow using last point)
                CurrentPrompt = OpenCADStrings.LineStartPointPrompt;
                var startPoint = await _pointInputHelper.GetPointAsync(
                    "Specify start point",
                    allowLastPoint: true,
                    basePoint: null,
                    _cancellationTokenSource.Token);

                if (startPoint == null)
                {
                    Cancel();
                    return;
                }

                // Store the very first start point for closing
                _firstStartPoint = startPoint;

                Context?.OutputMessage(
                    string.Format(
                        OpenCADStrings.LineStartPointConfirmed,
                        startPoint.X,
                        startPoint.Y,
                        startPoint.Z));

                // Loop to create continuous line segments
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Get end point with rubberband line from start point
                    CurrentPrompt = OpenCADStrings.LineEndPointPrompt;
                    var result = await _pointInputHelper.GetPointOrKeywordAsync(
                        "Specify next point or [Close/Undo] or press ESC to finish",
                        allowLastPoint: false,
                        basePoint: startPoint,
                        keywords: new[] { "C", "Close", "U", "Undo" },
                        _cancellationTokenSource.Token);

                    if (result.IsCancelled)
                    {
                        // User cancelled - exit the loop
                        break;
                    }

                    if (result.IsKeyword)
                    {
                        var keyword = result.Keyword?.ToUpperInvariant();
                        
                        if (keyword == "C" || keyword == "CLOSE")
                        {
                            // Close the figure by connecting to the first start point
                            if (_firstStartPoint != null && !startPoint.Equals(_firstStartPoint))
                            {
                                CreateLine(startPoint, _firstStartPoint);
                                Context?.OutputMessage("Figure closed.");
                            }
                            break;
                        }
                        else if (keyword == "U" || keyword == "UNDO")
                        {
                            // TODO: Implement undo for last line segment
                            Context?.OutputMessage("Undo not yet implemented in line command.");
                            continue;
                        }
                    }

                    var endPoint = result.Point;
                    if (endPoint == null)
                    {
                        // Shouldn't happen, but handle gracefully
                        break;
                    }

                    // Create the line segment
                    CreateLine(startPoint, endPoint);

                    // Use the endpoint as the new start point for the next segment
                    startPoint = endPoint;
                }

                // Command completed successfully
                Context?.OutputMessage("Line command completed.");
                RaiseCommandCompleted();
            }
            catch (OperationCanceledException)
            {
                Cancel();
            }
            finally
            {
                _firstStartPoint = null;
            }
        }

        public override bool ProcessInput(string input)
        {
            // Pass keyboard input to the PointInputHelper to complete the async task
            if (_pointInputHelper != null)
            {
                return _pointInputHelper.ProcessKeyboardInput(input);
            }
            
            return false;
        }

        private void CreateLine(Point3D start, Point3D end)
        {
            Line line = null;
            
            // Get the document to apply current properties
            var document = Context?.GetDocument();
            if (document != null)
            {
                line = new Line(document, start, end);
                // Apply current layer and drawing properties to the new line
                document.ApplyCurrentProperties(line);
                System.Diagnostics.Debug.WriteLine($"Line created on layer: {document.CurrentLayer?.Name ?? "none"}");
            }
            else
            {
                throw new InvalidOperationException("No active document to create line in.");
            }

            // Use undo/redo system if available
            var undoManager = Context?.GetUndoRedoManager();
            var viewport = Context?.GetActiveViewport();
            
            if (undoManager != null && document != null)
            {
                var action = new Undo.AddGeometryAction(
                    line, 
                    document, 
                    viewport, 
                    string.Format(
                        OpenCADStrings.UndoCreateLine,
                        start.X,
                        start.Y,
                        start.Z,
                        end.X,
                        end.Y,
                        end.Z)
                );
                undoManager.ExecuteAction(action);
            }
            else
            {
                // Fallback to direct creation
                Context?.RaiseGeometryCreated(line);
            }
            
            Context?.OutputMessage(
                string.Format(
                    OpenCADStrings.LineCreated,
                    start.X,
                    start.Y,
                    start.Z,
                    end.X,
                    end.Y,
                    end.Z));
        }

        public override void Cancel()
        {
            base.Cancel();
            _cancellationTokenSource?.Cancel();
            _pointInputHelper?.Cancel();
            _firstStartPoint = null;
            CurrentPrompt = string.Empty;
        }
    }
}