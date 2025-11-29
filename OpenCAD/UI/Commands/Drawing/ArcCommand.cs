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
            Radius,
            SecondPoint
        }

        private enum ArcInputMode
        {
            CSE, // Center, Start, End
            SCE, // Start, Center, End
            SER,  // Start, End, Center
            PT3,  // 3 Points
            Last
        }

        private ArcInputStep _step;
        private ArcInputMode _arcInputMode = ArcInputMode.SCE;
        private Point3D _center;
        private Point3D _start;
        private Point3D _end;

        // preview arc instance (removed/added during preview)
        private Arc? _previewArc;
        private PropertyChangedEventHandler? _arcPreviewHandler;
        private Point3D _second;

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
                _step = ArcInputStep.StartPoint;
                var result = await GetInitialInput();
                if (result == null)
                {
                    Cancel();
                    return;
                }
                SetArcPoint(result);

                // Special handling for Last mode:
                // initial (start) was taken from last drawable's end point.
                // now request radius (GetDistance) and compute center from last drawable's second derivative.
                if (_arcInputMode == ArcInputMode.Last)
                {
                    _step = ArcInputStep.CenterPoint;
                    var centerResult = await CalculateCenterFromRadius();
                    if (!centerResult)
                    {
                        Cancel();
                        return;
                    }
                }
                else
                {
                    // Normal flow for non-Last modes
                    result = await GetSecondInput();
                    if (result == null)
                    {
                        Cancel();
                        return;
                    }
                    SetArcPoint(result);
                }

                result = await GetLastInput();
                if (result == null)
                {
                    Cancel();
                    return;
                }
                SetArcPoint(result);

                // If PT3, compute center from three points now so CreateArc has a valid center
                if (_arcInputMode == ArcInputMode.PT3)
                {
                    if (!TryGetCircleThroughThreePoints(_start, _second, _end, out var c, out var r))
                    {
                        Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                        Cancel();
                        return;
                    }
                    _center = c;
                }

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

        private async Task<bool> CalculateCenterFromRadius()
        {
            var distResult = await GetDistance(OpenCADStrings.ArcRadiusPrompt);
            if (double.IsNaN(distResult.DoubleValue))
            {
                return false;
            }

            double radius = distResult.DoubleValue;

            var doc = Context?.GetDocument();
            var lastDrawable = doc?.GetLastGeometricChild();
            if (lastDrawable == null)
            {
                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                return false;
            }

            // determine end point of last drawable
            Point3D lastEnd;
            if (lastDrawable is Line lastLine)
                lastEnd = lastLine.EndPoint;
            else if (lastDrawable is Arc lastArc)
                lastEnd = lastArc.EndPoint;
            else
            {
                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                return false;
            }

            var secondDeriv = lastDrawable.GetSecondDerivate(lastEnd);
            if (secondDeriv == null || secondDeriv.Length < 1e-12)
            {
                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                return false;
            }

            // center = start + normalized(secondDeriv) * radius
            var perpUnit = secondDeriv.Normalized;
            SetArcPoint(_start + perpUnit * radius);
            return true;
        }

        private async Task<Point3D> GetSecondInput()
        {
            // Determine next step based on input mode
            _step = _arcInputMode switch
            {
                ArcInputMode.CSE => ArcInputStep.StartPoint,
                ArcInputMode.SCE => ArcInputStep.CenterPoint,
                ArcInputMode.SER => ArcInputStep.EndPoint,
                ArcInputMode.PT3 => ArcInputStep.SecondPoint,
                ArcInputMode.Last => ArcInputStep.Radius,
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
                ArcInputStep.SecondPoint => _second = result,
                _ => throw new NotImplementedException()
            };
        }

        private async Task<Point3D> GetInitialInput()
        {
            BasePoint = null;
            var step = _step switch
            {
                ArcInputStep.CenterPoint => OpenCADStrings.Center,
                ArcInputStep.StartPoint => OpenCADStrings.StartPoint,
                ArcInputStep.EndPoint => OpenCADStrings.EndPoint,
                _ => throw new NotImplementedException(),
            };
            var keyWords = new string[] { "CSE", "SCE", "SER", "3PT", "Last" };
            var result = await GetPoint(string.Format(OpenCADStrings.ArcPointPrompt, step), keyWords);

            if (result == null || result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
                throw new OperationCanceledException();

            if (result.Keyword is string keyWord)
            {
                KeyWordInput(keyWord);

                // If user requested Last, immediately obtain start point from last drawable's end point.
                if (_arcInputMode == ArcInputMode.Last)
                {
                    var doc = Context?.GetDocument();
                    var lastDrawable = doc?.GetLastGeometricChild();
                    if (lastDrawable == null)
                    {
                        Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                        throw new OperationCanceledException();
                    }

                    if (lastDrawable is Line lastLine)
                        return lastLine.EndPoint;
                    if (lastDrawable is Arc lastArc)
                        return lastArc.EndPoint;

                    // unsupported drawable type for Last
                    Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
                    throw new OperationCanceledException();
                }

                return await GetArcPoint();
            }

            else if (result.Point is Point3D point)
                return point;

            return null;
        }

        private void KeyWordInput(string? keyWord)
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
                case "3PT":
                    _arcInputMode = ArcInputMode.PT3;
                    _step = ArcInputStep.StartPoint;
                    break;
                case "LAST":
                case "Last":
                    _arcInputMode = ArcInputMode.Last;
                    _step = ArcInputStep.StartPoint;
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
                ArcInputMode.PT3 => ArcInputStep.EndPoint,
                ArcInputMode.Last => ArcInputStep.EndPoint,
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
                        // Two possible preview computations:
                        // - non-PT3: center & start already known -> radius from start, angles from center
                        // - PT3: start, second are known and previewPoint is the end -> compute circle through three points
                        Point3D previewCenter = default;
                        double radius = 0.0;
                        double startAngle = 0.0;
                        double endAngle = 0.0;
                        bool haveCircle = false;

                        if (_arcInputMode == ArcInputMode.PT3)
                        {
                            // compute circle from three points: _start, _second, previewPoint
                            if (TryGetCircleThroughThreePoints(_start, _second, previewPoint, out var c, out var r))
                            {
                                previewCenter = c;
                                radius = r;
                                startAngle = CalculateAngle(previewCenter, _start);
                                endAngle = CalculateAngle(previewCenter, previewPoint);

                                // Ensure the CCW arc from startAngle to endAngle includes _second.
                                // If it doesn't, swap start/end so the arc contains the second point.
                                double aStart = NormalizeAngle(startAngle);
                                double aEnd = NormalizeAngle(endAngle);
                                double aSecond = NormalizeAngle(CalculateAngle(previewCenter, _second));
                                if (!IsAngleBetweenCCW(aStart, aSecond, aEnd))
                                {
                                    // swap so that arc chosen CCW passes through second
                                    double tmp = aStart;
                                    aStart = aEnd;
                                    aEnd = tmp;
                                    startAngle = aStart;
                                    endAngle = aEnd;
                                }

                                haveCircle = true;
                            }
                            else
                            {
                                // cannot form circle (colinear) -> skip preview arc
                                haveCircle = false;
                            }
                        }
                        else
                        {
                            // existing behavior (covers Last as well since center is precomputed)
                            previewCenter = _center;
                            radius = CalculateDistance(_center, _start);
                            startAngle = CalculateAngle(_center, _start);
                            endAngle = CalculateAngle(_center, previewPoint);
                            haveCircle = true;
                        }

                        if (haveCircle)
                        {
                            var doc = Context.GetDocument();
                            if (doc != null)
                            {
                                var arc = new Arc(previewCenter, radius, startAngle, endAngle, doc);
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

            // Attach the arc preview handler on the UI thread after StartPreview has captured cached providers
            if (Context != null && _arcPreviewHandler != null)
            {
                Context.PostToUI(() =>
                {
                    if (CachedViewModel != null)
                        CachedViewModel.PropertyChanged += _arcPreviewHandler;
                });
            }

            try
            {
                // Allow the user to either type an angle or pick a point (angle computed from center to picked point)
                var result = await GetAngle(OpenCADStrings.ArcEndAnglePrompt);

                if (result == null || double.IsNaN(result.DoubleValue))
                {
                    // user cancelled or invalid -> treat as cancel
                    return null;
                }
                var angle = result.DoubleValue;

                // compute endpoint from center, radius and angle
                double radiusVal = CalculateDistance(_center, _start);
                double x = _center.X + radiusVal * Math.Cos(angle);
                double y = _center.Y + radiusVal * Math.Sin(angle);
                var endPoint = new Point3D(x, y, _center.Z);

                return endPoint;
            }
            finally
            {
                // cleanup preview handler and preview arc
                try
                {
                    if (Context != null && _arcPreviewHandler != null)
                    {
                        Context.PostToUI(() =>
                        {
                            if (CachedViewModel != null)
                                CachedViewModel.PropertyChanged -= _arcPreviewHandler;
                        });
                    }
                }
                catch { }

                try
                {
                    if (_previewArc != null && Context != null)
                    {
                        // Remove preview arc on UI thread via Context to avoid cross-thread access
                        Context.PostToUI(() =>
                        {
                            try
                            {
                                if (_previewArc != null && CachedViewport != null)
                                {
                                    CachedViewport.RemoveObject(_previewArc);
                                    _previewArc = null;
                                }
                            }
                            catch { }
                        });
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

        /// <summary>
        /// Try to compute circle center and radius passing through three non-colinear points (XY plane).
        /// Returns false if points are colinear or computation unstable.
        /// </summary>
        private bool TryGetCircleThroughThreePoints(Point3D p1, Point3D p2, Point3D p3, out Point3D center, out double radius)
        {
            center = Point3D.Origin;
            radius = double.NaN;

            double x1 = p1.X, y1 = p1.Y;
            double x2 = p2.X, y2 = p2.Y;
            double x3 = p3.X, y3 = p3.Y;

            double a = x1 - x2;
            double b = y1 - y2;
            double c = x1 - x3;
            double d = y1 - y3;

            double e = ((x1 * x1 - x2 * x2) + (y1 * y1 - y2 * y2)) / 2.0;
            double f = ((x1 * x1 - x3 * x3) + (y1 * y1 - y3 * y3)) / 2.0;

            double det = a * d - b * c;
            if (Math.Abs(det) < 1e-12)
                return false; // colinear or nearly so

            double cx = (d * e - b * f) / det;
            double cy = (-c * e + a * f) / det;

            center = new Point3D(cx, cy, (p1.Z + p2.Z + p3.Z) / 3.0);
            radius = Math.Sqrt((cx - x1) * (cx - x1) + (cy - y1) * (cy - y1));
            if (double.IsNaN(radius) || double.IsInfinity(radius) || radius < 1e-12)
                return false;

            return true;
        }

        /// <summary>
        /// Normalize angle to [0, 2*PI)
        /// </summary>
        private static double NormalizeAngle(double angle)
        {
            double twoPi = Math.PI * 2.0;
            double a = angle % twoPi;
            if (a < 0) a += twoPi;
            return a;
        }

        /// <summary>
        /// Returns true if moving CCW from start to end (inclusive) the test angle is encountered.
        /// Angles must be normalized to [0,2PI) or the function will normalize them.
        /// </summary>
        private static bool IsAngleBetweenCCW(double start, double test, double end)
        {
            start = NormalizeAngle(start);
            test = NormalizeAngle(test);
            end = NormalizeAngle(end);

            if (start <= end)
                return test >= start && test <= end;

            // wrapped case: e.g., start=300deg, end=60deg -> true if test >= start || test <= end
            return test >= start || test <= end;
        }

        private async Task<Point3D?> GetArcPoint(Point3D? basePoint = null)
        {
            var step = _step switch
            {
                ArcInputStep.CenterPoint => OpenCADStrings.Center,
                ArcInputStep.StartPoint => OpenCADStrings.StartPoint,
                ArcInputStep.EndPoint => OpenCADStrings.EndPoint,
                ArcInputStep.Radius => OpenCADStrings.Radius,
                ArcInputStep.SecondPoint => OpenCADStrings.SecondPoint,
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

            if (undoManager != null)
            {
                // Execute undo action creation/execution on UI thread so any viewport access is safe
                Context?.PostToUI(() =>
                {
                    var viewport = Context.GetActiveViewport();
                    var action = new Undo.AddGeometryAction(
                        arc,
                        document,
                        viewport,
                        $"Create Arc at ({_center.X:F3}, {_center.Y:F3}, {_center.Z:F3}), " +
                        $"Radius: {radius:F3}, " +
                        $"Angles: {startAngle * 180 / Math.PI:F1}° to {endAngle * 180 / Math.PI:F1}°"
                    );
                    undoManager.ExecuteAction(action);
                });
            }
            else
            {
                // Fallback to direct creation (CommandContext.RaiseGeometryCreated posts to UI)
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