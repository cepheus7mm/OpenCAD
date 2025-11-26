using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands.Drawing
{
    /// <summary>
    /// Command to create an arc by specifying center, start point (defines radius and start angle),
    /// and end point (defines end angle)
    /// </summary>
    [InputCommand("arc", "Create an arc (prompts for center, start point, and end point)", "a")]
    public class ArcCommand : CommandBase
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private enum ArcInputStep
        {
            CenterPoint,
            StartPoint,
            EndPoint,
            Radius
        }

        private enum ArcInputMode
        {
            CSE, // Center, Start, End
            SCE, // Start, Center, End
            SER  // Start, End, Center
        }

        private ArcInputStep _step;
        private ArcInputMode _arcInputMode = ArcInputMode.CSE;
        private Point3D _center;
        private Point3D _start;
        private Point3D _end;

        // preview arc instance (removed/added during preview)
        private Arc? _previewArc;
        private PropertyChangedEventHandler? _arcPreviewHandler;

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
                _step = ArcInputStep.CenterPoint;
                var result = await GetInitialInput(); 
                if (result == null)
                {
                    Cancel();
                    return;
                }
                SetArcPoint(result);

                result = await GetSecondInput();
                if (result == null)
                {
                    Cancel();
                    return;
                }
                SetArcPoint(result);

                result = await GetLastInput();
                if (result == null)
                {
                    Cancel();
                    return;
                }

                SetArcPoint(result);

                // Create the arc
                CreateArc();

                // Command completed successfully
                Context?.OutputMessage("Arc command completed.");
                RaiseCommandCompleted();
            }
            catch (OperationCanceledException)
            {
                Cancel();
            }
        }

        private async Task<Point3D> GetSecondInput()
        {
            // Determine next step based on input mode
            _step = _arcInputMode switch
            {
                ArcInputMode.CSE => ArcInputStep.StartPoint,
                ArcInputMode.SCE => ArcInputStep.CenterPoint,
                ArcInputMode.SER => ArcInputStep.EndPoint,
                _ => throw new NotImplementedException()
            };

            return await GetArcPoint();
        }

        private void SetArcPoint(Point3D result)
        {
            BasePoint = result;

            _ = _step switch
            {
                ArcInputStep.CenterPoint => _center = result,
                ArcInputStep.StartPoint => _start = result,
                ArcInputStep.EndPoint => _end = result,
                ArcInputStep.Radius => _center = result,
                _ => throw new NotImplementedException()
            };
        }

        private async Task<Point3D> GetInitialInput()
        {
            var keyWords = new string[] { "CSE", "SCE", "SER", "Last" };
            var result = await GetPoint(string.Format(OpenCADStrings.ArcPointPrompt, "center"), keyWords);

            if (result == null || result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                throw new OperationCanceledException();

            if (result.Keyword is string keyWord)
            {
                KeyWordInput(keyWord);
                return await GetArcPoint();
            }

            else if (result.Point is Point3D point)
                return  point;

            return null;
        }

        private void KeyWordInput (string? keyWord)
        {
            if (string.IsNullOrWhiteSpace(keyWord))
                return;

            // Handle specific keywords
            switch (keyWord.ToUpperInvariant())
            {
                case "CSE":
                    _arcInputMode = ArcInputMode.CSE;
                    _step = ArcInputStep.CenterPoint;
                    break;
                case "SCE":
                    _arcInputMode = ArcInputMode.SCE;
                    _step = ArcInputStep.StartPoint;
                    break;
                case "SER":
                    _arcInputMode = ArcInputMode.SER;
                    _step = ArcInputStep.StartPoint;
                    break;
                case "Last":
                    break;
                default:
                    return;
            }
        }

        private async Task<Point3D?> GetLastInput()
        {
            // Determine next step based on input mode
            _step = _arcInputMode switch
            {
                ArcInputMode.CSE => ArcInputStep.EndPoint,
                ArcInputMode.SCE => ArcInputStep.EndPoint,
                ArcInputMode.SER => ArcInputStep.Radius,
                _ => throw new NotImplementedException()
            };

            if (_step == ArcInputStep.Radius)
            {
                // For SER mode, calculate center from radius
                var result = await GetDistance(OpenCADStrings.ArcRadiusPrompt);
                if (double.IsNaN(result.DoubleValue))
                    return null;
                return GetCenterFromStartEndRadius(result.DoubleValue);
            }

            // For EndPoint mode we want live arc preview and also allow angle input.
            // Set base as center and start previewing.
            BasePoint = _center;
            StartPreview();

            // preview handler: build temporary arc from current preview point
            _previewArc = null;
            _arcPreviewHandler = (sender, e) =>
            {
                if (e.PropertyName != nameof(ViewportViewModel.PreviewPoint))
                    return;

                var previewPoint = CachedViewModel?.PreviewPoint;
                try
                {
                    // remove previous preview arc
                    if (_previewArc != null && CachedViewport != null)
                    {
                        try { CachedViewport.RemoveObject(_previewArc); } catch { }
                        _previewArc = null;
                    }

                    if (previewPoint != null && CachedViewport != null && Context != null)
                    {
                        double radius = CalculateDistance(_center, _start);
                        double startAngle = CalculateAngle(_center, _start);
                        double endAngle = CalculateAngle(_center, previewPoint);
                        var doc = Context.GetDocument();
                        if (doc != null)
                        {
                            var arc = new Arc(_center, radius, startAngle, endAngle, doc);
                            CachedViewport.AddObject(arc);
                            _previewArc = arc;
                            CachedViewport.Refresh();
                        }
                    }
                    else
                    {
                        if (CachedViewport != null)
                            CachedViewport.Refresh();
                    }
                }
                catch
                {
                    // ignore preview errors
                }
            };

            if (CachedViewModel != null && _arcPreviewHandler != null)
                CachedViewModel.PropertyChanged += _arcPreviewHandler;

            try
            {
                // Allow the user to either type an angle or pick a point (angle computed from center to picked point)
                var result = await GetAngle(OpenCADStrings.ArcEndAnglePrompt);

                if (result == null || result.DoubleValue == double.NaN)
                {
                    // user cancelled or invalid -> treat as cancel
                    return null;
                }
                var angle = result.DoubleValue;
                // compute endpoint from center, radius and angle
                double radius = CalculateDistance(_center, _start);
                double x = _center.X + radius * Math.Cos(angle);
                double y = _center.Y + radius * Math.Sin(angle);
                var endPoint = new Point3D(x, y, _center.Z);

                return endPoint;
            }
            finally
            {
                // cleanup preview handler and preview arc
                try
                {
                    if (CachedViewModel != null && _arcPreviewHandler != null)
                        CachedViewModel.PropertyChanged -= _arcPreviewHandler;
                }
                catch { }

                try
                {
                    if (_previewArc != null && CachedViewport != null)
                    {
                        CachedViewport.RemoveObject(_previewArc);
                        _previewArc = null;
                    }
                }
                catch { }

                StopPreview();
            }
        }

        private Point3D GetCenterFromStartEndRadius(double radius)
        {
            // chord vector from start to end
            var chord = _end - _start; // Vector3D
            double d = chord.Length;

            // Guard: identical points -> cannot determine a unique center
            if (d < 1e-12)
            {
                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                return _start.Clone();
            }

            double absRadius = Math.Abs(radius);

            // radius must be at least half the chord length
            if (absRadius < d * 0.5)
            {
                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                // return midpoint as a safe fallback
                return new Point3D(
                    (_start.X + _end.X) * 0.5,
                    (_start.Y + _end.Y) * 0.5,
                    (_start.Z + _end.Z) * 0.5);
            }

            // midpoint of the chord
            var midpoint = new Point3D(
                (_start.X + _end.X) * 0.5,
                (_start.Y + _end.Y) * 0.5,
                (_start.Z + _end.Z) * 0.5);

            // distance from midpoint to center along the perpendicular bisector
            double halfChord = d * 0.5;
            double h = Math.Sqrt(Math.Max(0.0, absRadius * absRadius - halfChord * halfChord));

            // perpendicular direction in XY plane
            var perp = new Vector3D(-chord.Y, chord.X, 0.0);
            var perpUnit = perp.Normalized;

            // sign chosen by radius sign: positive -> one side, negative -> opposite side
            double sign = radius >= 0.0 ? 1.0 : -1.0;

            var offset = perpUnit * (h * sign);

            // Keep Z consistent with midpoint (offset.Z is zero since perp.Z == 0)
            return midpoint + offset;
        }

        private async Task<Point3D?> GetArcPoint(Point3D? basePoint = null)
        {
            var step = _step switch
            {
                ArcInputStep.CenterPoint => OpenCADStrings.Center,
                ArcInputStep.StartPoint => OpenCADStrings.StartPoint,
                ArcInputStep.EndPoint => OpenCADStrings.EndPoint,
                _ => throw new NotImplementedException(),
            };
            if (basePoint != null)
            {
                BasePoint = basePoint;
            }
            var prompt = string.Format(OpenCADStrings.ArcPointPrompt, step);
            var result = await GetPoint(prompt);
            if (result != null && result.Point is Point3D point)
                return point;

            return null;
        }

        public override bool ProcessInput(string input)
        {
            // Route keyboard input to shared helper in base (created during Initialize)
            if (_inputHelper != null)
            {
                return _inputHelper.ProcessKeyboardInput(input);
            }
            
            return false;
        }

        /// <summary>
        /// Calculate the distance between two points (used for radius)
        /// </summary>
        private double CalculateDistance(Point3D center, Point3D point)
        {
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculate the angle from center to point (in radians, counter-clockwise from positive X-axis)
        /// </summary>
        private double CalculateAngle(Point3D center, Point3D point)
        {
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            return Math.Atan2(dy, dx);
        }

        private void CreateArc()
        {
            Arc arc = null;

            var startAngle = CalculateAngle(_center, _start);
            var endAngle = CalculateAngle(_center, _end);
            var radius = CalculateDistance(_center, _start);

            // Get the document to apply current properties
            var document = Context?.GetDocument();
            if (document != null)
            {
                arc = new Arc(_center, radius, startAngle, endAngle, document);
            }
            else
            {
                throw new InvalidOperationException("No active document to create arc in.");
            }

            // Use undo/redo system if available
            var undoManager = Context?.GetUndoRedoManager();
            var viewport = Context?.GetActiveViewport();
            
            if (undoManager != null && document != null)
            {
                var action = new Undo.AddGeometryAction(
                    arc, 
                    document, 
                    viewport, 
                    $"Create Arc at ({_center.X:F3}, {_center.Y:F3}, {_center.Z:F3}), " +
                    $"Radius: {radius:F3}, " +
                    $"Angles: {startAngle * 180 / Math.PI:F1}° to {endAngle * 180 / Math.PI:F1}°"
                );
                undoManager.ExecuteAction(action);
            }
            else
            {
                // Fallback to direct creation
                Context?.RaiseGeometryCreated(arc);
            }
            
            Context?.OutputMessage(
                $"Arc created: Center=({_center.X:F3}, {_center.Y:F3}, {_center.Z:F3}), " +
                $"Radius={radius:F3}, " +
                $"Start angle={startAngle * 180 / Math.PI:F1}°, " +
                $"End angle={endAngle * 180 / Math.PI:F1}°");
        }

        public override void Cancel()
        {
            base.Cancel();
            _cancellationTokenSource?.Cancel();
            CurrentPrompt = string.Empty;
        }
    }
}