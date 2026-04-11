using OpenCAD;
using OpenCAD.Geometry;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Drawing.LineCommand.Modes;
using UI.Commands.InputHelpers;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;
using static UI.Commands.InputHelpers.InputResult;

namespace UI.Commands.Drawing.LineCommand
{
    /// <summary>
    /// Command to create a line
    /// </summary>
    [InputCommand("line", "Create a line (prompts for start and end points or click in viewport)", "l")]
    public class LineCommand : CommandBase
    {
        private Point3D? _firstStartPoint; // Store the very first start point for closing

        public Point3D? FirstStartPoint { get; set; }

        public override bool IsMultiStep => true;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
        }

        public override async Task Execute()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            ILineCreationMode mode = new FirstPointMode();

            while (!(mode is FinishedMode))
            {
                BasePoint = mode.GetBasePoint();

                var result = await GetPoint(new InputParams
                {
                    Prompt = mode.Prompt,
                    BasePoint = BasePoint,
                    Keywords = mode.Keywords,
                    AllowLastPoint = true,
                    CancellationToken = _cancellationTokenSource.Token
                });

                if (result.IsCancel)
                    break;

                if (result.IsKeyword)
                {
                    mode = HandleKeyword(mode, result.Keyword);
                    continue;
                }

                if (result.IsPoint)
                {
                    mode.SetPoint(result.Point.Value);
                    if (mode.IsComplete)
                        mode = mode.Apply(this);
                }
            }
        }

        public override bool ProcessInput(string input)
        {
            // Pass keyboard input to the PointInputHelper to complete the async task
            if (_inputHelper != null)
            {
                _inputHelper.ProcessKeyboardInput(input);
                return IsCommandCompleted;
            }
            
            return false;
        }

        public void CreateLine(Point3D start, Point3D end)
        {
            Line line;

            var document = Context?.GetDocument();
                line = new Line(start, end, document);

            CreateObject(line);
        }

        protected override string GetUndoCreateString(OpenCADObject obj)
        {
            if (obj is not Line line)
                return base.GetUndoCreateString(obj);

            return string.Format(
                OpenCADStrings.UndoCreateLine,
                line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z,
                line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z);
        }

        //public override void Cancel()
        //{
        //    base.Cancel();
        //    _cancellationTokenSource?.Cancel();
        //    _firstStartPoint = null;
        //    CurrentPrompt = string.Empty;
        //}

        private ILineCreationMode HandleKeyword(ILineCreationMode mode, string keyword)
        {
            keyword = keyword.ToUpperInvariant();

            if ((keyword == "C" || keyword == "CLOSE") && FirstStartPoint.HasValue)
            {
                var closeMode = new CloseMode(mode.GetBasePoint(), FirstStartPoint.Value, Document);
                return closeMode.Apply(this);
            }

            if (keyword == "U" || keyword == "UNDO")
                return new UndoMode();

            return mode;
        }

        internal OpenCADDocument GetDocument()
        {
            if (Document != null)
                return Document;
            if (Context != null)
                return Context.GetDocument()!;
            throw new InvalidOperationException("No document available");
        }
    }
}