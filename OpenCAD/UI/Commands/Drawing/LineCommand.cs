using OpenCAD;
using OpenCAD.Geometry;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands.Drawing
{
    /// <summary>
    /// Command to create a line
    /// </summary>
    [InputCommand("line", "Create a line (prompts for start and end points or click in viewport)", "l")]
    public class LineCommand : CommandBase
    {
        private Point3D? _firstStartPoint; // Store the very first start point for closing

        public override bool IsMultiStep => true;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
        }

        public override async Task Execute()
        {
            //if (_inputHelper == null)
                //return;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Get initial start point (allow using last point)
                BasePoint = _firstStartPoint;
                var result = await GetPoint(
                    "Specify start point",
                    allowLastPoint: true);

                if (result != null && result.Point is Point3D startPoint)
                {
                    // Store the very first start point for closing
                    _firstStartPoint = startPoint;
                }
                else
                {
                    Cancel();
                    return;
                }


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
                    BasePoint = startPoint;
                    result = await GetPoint(
                        "Specify next point or press ESC to finish",
                        allowLastPoint: false,
                        keyWords: new[] { "C", "Close", "U", "Undo" });

                    var endPoint = null as Point3D;
                    if (result != null && result.Point is Point3D)
                    {
                        endPoint = result.Point;
                    }

                    if (result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                    {
                        // User cancelled - exit the loop
                        break;
                    }

                    if (result.ResultType == InputHelpers.InputResult.InputResultType.Keyword)
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

                    // Create the line segment
                    CreateLine(startPoint, endPoint);

                    // Use the endpoint as the new start point for the next segment
                    startPoint = endPoint;
                }

            }
            catch (OperationCanceledException)
            {
                Cancel();
            }
            finally
            {
                // Command completed successfully
                Context?.OutputMessage("Line command completed.");
                RaiseCommandCompleted();
            }
        }

        public override bool ProcessInput(string input)
        {
            // Pass keyboard input to the PointInputHelper to complete the async task
            if (_inputHelper != null)
            {
                return _inputHelper.ProcessKeyboardInput(input);
            }
            
            return false;
        }

        private void CreateLine(Point3D start, Point3D end)
        {
            Line line;

            var document = Context?.GetDocument();
            if (document != null)
            {
                line = new Line(document, start, end);
            }
            else
            {
                throw new InvalidOperationException("No active document to create line in.");
            }

            var undoManager = Context?.GetUndoRedoManager();

            // If undo available, the action may need a UI-thread viewport reference; capture + execute on UI thread
            if (undoManager != null)
            {
                Context?.PostToUI(() =>
                {
                    var viewport = Context.GetActiveViewport();
                    var action = new Undo.AddGeometryAction(
                        line,
                        document,
                        viewport,
                        string.Format(
                            OpenCADStrings.UndoCreateLine,
                            start.X, start.Y, start.Z,
                            end.X, end.Y, end.Z)
                    );
                    undoManager.ExecuteAction(action);
                });
            }
            else
            {
                // CommandContext.RaiseGeometryCreated already posts to UI (our implementation does), so safe to call directly
                Context?.RaiseGeometryCreated(line);
            }

            Context?.OutputMessage(
                string.Format(
                    OpenCADStrings.LineCreated,
                    start.X, start.Y, start.Z,
                    end.X, end.Y, end.Z));
        }

        public override void Cancel()
        {
            base.Cancel();
            _cancellationTokenSource?.Cancel();
            _firstStartPoint = null;
            CurrentPrompt = string.Empty;
        }
    }
}