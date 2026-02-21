using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Drawing.ArcCommand.ArcModes;
using UI.Commands.InputHelpers;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.Drawing.ArcCommand
{
    /// <summary>
    /// Command to create an arc by specifying center, start point (defines radius and start angle),
    /// and end point (defines end angle)
    /// </summary>
    [InputCommand("arc", "Create an arc (prompts for center, start point, and end point)", "a")]
    public class ArcCommand : CommandBase
    {
        private IArcCreationMode _mode;

        public override async Task Execute()
        {
            var doc = Context?.GetDocument();
            _mode = new StartCenterEndMode(doc);

            BasePoint = Point3D.NotAPoint;

            while (!_mode.IsComplete)
            {
                BasePoint = _mode.GetBasePoint();
                var input = _mode.UserInputType switch
                {
                    UserInputType.Point => await GetPoint(_mode.Prompt, BasePoint, _mode.Keywords),
                    UserInputType.Distance => await GetDistance(_mode.Prompt),
                    UserInputType.Angle => await GetAngle(_mode.Prompt),
                    _ => new InputResult { ResultType = InputResult.InputResultType.Cancel }
                };

                if (input.IsKeyword)
                {
                    _mode = SwitchMode(_mode, input.Keyword, doc);
                    continue;
                }

                if (input.IsPoint)
                {
                    _mode.SetPoint(input.Point.Value);
                    UpdatePreview();
                }

                if (input.IsDouble)
                {
                    _mode.SetDouble(input.DoubleValue);
                }
            }

            var arc = _mode.CreateArc();
            CreateObject(arc);
            RaiseCommandCompleted();
        }

        private IArcCreationMode SwitchMode(IArcCreationMode current, string keyword, OpenCADDocument doc)
        {
            return keyword.ToUpperInvariant() switch
            {
                "CEN" => new StartCenterEndMode(doc),
                "ANG" => new StartCenterAngleMode(doc),
                "DIR" => new StartEndDirectionMode(doc),
                "RAD" => new StartEndRadiusMode(doc),
                "LEN" => new StartEndLengthMode(doc),
                "3PT" => new ThreePointArcMode(doc),
                "CONT" => new ContinueArcMode(doc),
                _ => current
            };
        }

        protected override IEnumerable<OpenCADObject> ComputePreviewObjects()
        {
            yield return _mode.GetPreview(TargetPoint);
        }
    }
    //        private enum ArcInputStep
    //        {
    //            CenterPoint,
    //            StartPoint,
    //            EndPoint,
    //            Radius,
    //            SecondPoint
    //        }

    //        private enum ArcInputMode
    //        {
    //            CSE, // Center, Start, End
    //            SCE, // Start, Center, End
    //            SER,  // Start, End, Center
    //            PT3,  // 3 Points
    //            Last
    //        }

    //        private ArcInputStep _step;
    //        private ArcInputMode _arcInputMode = ArcInputMode.SCE;
    //        private OpenCAD.Geometry.Point3D _center = OpenCAD.Geometry.Point3D.NotAPoint;
    //        private OpenCAD.Geometry.Point3D _start = OpenCAD.Geometry.Point3D.NotAPoint;
    //        private OpenCAD.Geometry.Point3D _end = OpenCAD.Geometry.Point3D.NotAPoint;

    //        // preview arc instance (removed/added during preview)
    //        private Arc? _previewArc;
    //        private PropertyChangedEventHandler? _arcPreviewHandler;
    //        private OpenCAD.Geometry.Point3D _second;

    //        public override bool IsMultiStep => true;

    //        public override async Task Initialize(ICommandContext context)
    //        {
    //            await base.Initialize(context);
    //        }

    //        public override async Task Execute()
    //        {
    //            _cancellationTokenSource = new CancellationTokenSource();

    //            try
    //            {
    //                _step = ArcInputStep.StartPoint;
    //                var result = await GetInitialInput();
    //                if (!result.HasValue)
    //                {
    //                    Cancel();
    //                    return;
    //                }
    //                SetArcPoint(result.Value);

    //                // Special handling for Last mode:
    //                // initial (start) was taken from last drawable's end point.
    //                // now request radius (GetDistance) and compute center from last drawable's second derivative.
    //                if (_arcInputMode == ArcInputMode.Last)
    //                {
    //                    _step = ArcInputStep.CenterPoint;
    //                    var centerResult = await CalculateCenterFromRadius();
    //                    if (!centerResult)
    //                    {
    //                        Cancel();
    //                        return;
    //                    }
    //                }
    //                else
    //                {
    //                    // Normal flow for non-Last modes
    //                    result = await GetSecondInput();
    //                    if (!result.HasValue)
    //                    {
    //                        Cancel();
    //                        return;
    //                    }
    //                    SetArcPoint(result.Value);
    //                }

    //                result = await GetLastInput();
    //                if (!result.HasValue)
    //                {
    //                    Cancel();
    //                    return;
    //                }
    //                SetArcPoint(result.Value);

    //                // If PT3, compute center from three points now so CreateArc has a valid center
    //                if (_arcInputMode == ArcInputMode.PT3)
    //                {
    //                    if (!GeometricCalculator.TryGetCircleThroughThreePoints(_start, _second, _end, out var c, out var r))
    //                    {
    //                        Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
    //                        Cancel();
    //                        return;
    //                    }
    //                    _center = c;
    //                }

    //                // Create the arc
    //                CreateArc();

    //                // Command completed successfully
    //                Context?.OutputMessage("Arc command completed.");
    //                RaiseCommandCompleted();
    //            }
    //            catch (OperationCanceledException)
    //            {
    //                Cancel();
    //            }
    //        }

    //        private async Task<bool> CalculateCenterFromRadius()
    //        {
    //            var distResult = await GetDistance(OpenCADStrings.ArcRadiusPrompt);
    //            if (double.IsNaN(distResult.DoubleValue))
    //            {
    //                return false;
    //            }

    //            double radius = distResult.DoubleValue;

    //            var doc = Context?.GetDocument();
    //            var lastCurve = doc?.GetLastGeometricChild();
    //            if (lastCurve == null)
    //            {
    //                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
    //                return false;
    //            }

    //            var secondDeriv = lastCurve.GetSecondDerivativeAtParameter(1.0);
    //            if (secondDeriv.Length < 1e-12)
    //            {
    //                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
    //                return false;
    //            }

    //            // center = start + normalized(secondDeriv) * radius
    //            var perpUnit = secondDeriv.Normalized;
    //            SetArcPoint(_start + perpUnit * radius);
    //            return true;
    //        }

    //        private async Task<Point3D?> GetSecondInput()
    //        {
    //            // Determine next step based on input mode
    //            _step = _arcInputMode switch
    //            {
    //                ArcInputMode.CSE => ArcInputStep.StartPoint,
    //                ArcInputMode.SCE => ArcInputStep.CenterPoint,
    //                ArcInputMode.SER => ArcInputStep.EndPoint,
    //                ArcInputMode.PT3 => ArcInputStep.SecondPoint,
    //                ArcInputMode.Last => ArcInputStep.Radius,
    //                _ => throw new NotImplementedException()
    //            };

    //            return await GetArcPoint();
    //        }

    //        private void SetArcPoint(OpenCAD.Geometry.Point3D result)
    //        {
    //            BasePoint = result;

    //            _ = _step switch
    //            {
    //                ArcInputStep.CenterPoint => _center = result,
    //                ArcInputStep.StartPoint => _start = result,
    //                ArcInputStep.EndPoint => _end = result,
    //                ArcInputStep.Radius => _center = result,
    //                ArcInputStep.SecondPoint => _second = result,
    //                _ => throw new NotImplementedException()
    //            };
    //        }

    //        private async Task<Point3D?> GetInitialInput()
    //        {
    //            BasePoint = Point3D.NotAPoint;
    //            var step = _step switch
    //            {
    //                ArcInputStep.CenterPoint => OpenCADStrings.Center,
    //                ArcInputStep.StartPoint => OpenCADStrings.StartPoint,
    //                ArcInputStep.EndPoint => OpenCADStrings.EndPoint,
    //                _ => throw new NotImplementedException(),
    //            };
    //            var keyWords = new string[] { "CSE", "SCE", "SER", "3PT", "Last" };
    //            var result = await GetPoint(string.Format(OpenCADStrings.ArcPointPrompt, step), null, keyWords);

    //            if (result == null || result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
    //                throw new OperationCanceledException();

    //            if (result.Keyword is string keyWord)
    //            {
    //                KeyWordInput(keyWord);

    //                // If user requested Last, immediately obtain start point from last drawable's end point.
    //                if (_arcInputMode == ArcInputMode.Last)
    //                {
    //                    var doc = Context?.GetDocument();
    //                    var lastDrawable = doc?.GetLastGeometricChild();
    //                    if (lastDrawable == null)
    //                    {
    //                        Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
    //                        throw new OperationCanceledException();
    //                    }

    //                    if (lastDrawable is Line lastLine)
    //                        return lastLine.EndPoint;
    //                    if (lastDrawable is Arc lastArc)
    //                        return lastArc.EndPoint;

    //                    // unsupported drawable type for Last
    //                    Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
    //                    throw new OperationCanceledException();
    //                }

    //                return await GetArcPoint();
    //            }

    //            else if (result.Point is OpenCAD.Geometry.Point3D point)
    //                return point;

    //            return null;
    //        }

    //        private void KeyWordInput(string? keyWord)
    //        {
    //            if (string.IsNullOrWhiteSpace(keyWord))
    //                return;

    //            // Handle specific keywords
    //            switch (keyWord.ToUpperInvariant())
    //            {
    //                case "CSE":
    //                    _arcInputMode = ArcInputMode.CSE;
    //                    _step = ArcInputStep.CenterPoint;
    //                    break;
    //                case "SCE":
    //                    _arcInputMode = ArcInputMode.SCE;
    //                    _step = ArcInputStep.StartPoint;
    //                    break;
    //                case "SER":
    //                    _arcInputMode = ArcInputMode.SER;
    //                    _step = ArcInputStep.StartPoint;
    //                    break;
    //                case "3PT":
    //                    _arcInputMode = ArcInputMode.PT3;
    //                    _step = ArcInputStep.StartPoint;
    //                    break;
    //                case "LAST":
    //                case "Last":
    //                    _arcInputMode = ArcInputMode.Last;
    //                    _step = ArcInputStep.StartPoint;
    //                    break;
    //                default:
    //                    return;
    //            }
    //        }

    //        private async Task<Point3D?> GetLastInput()
    //        {
    //            // Determine next step based on input mode
    //            _step = _arcInputMode switch
    //            {
    //                ArcInputMode.CSE => ArcInputStep.EndPoint,
    //                ArcInputMode.SCE => ArcInputStep.EndPoint,
    //                ArcInputMode.SER => ArcInputStep.Radius,
    //                ArcInputMode.PT3 => ArcInputStep.EndPoint,
    //                ArcInputMode.Last => ArcInputStep.EndPoint,
    //                _ => throw new NotImplementedException()
    //            };

    //            if (_step == ArcInputStep.Radius)
    //            {
    //                // For SER mode, calculate center from radius
    //                var result = await GetDistance(OpenCADStrings.ArcRadiusPrompt);
    //                if (double.IsNaN(result.DoubleValue))
    //                    return null;
    //                return GetCenterFromStartEndRadius(result.DoubleValue);
    //            }

    //            // For EndPoint mode we want live arc preview and also allow angle input.
    //            // Set base as center and start previewing.
    //            BasePoint = _center;
    //            BeginPreview();

    //            // preview handler: build temporary arc from current preview point
    //            _previewArc = null;
    //            _arcPreviewHandler = (sender, e) =>
    //            {
    //                if (e.PropertyName != nameof(ViewportViewModel.PreviewPoint))
    //                    return;

    //                var previewPoint = CachedViewModel?.PreviewPoint;
    //                try
    //                {
    //                    // remove previous preview arc
    //                    if (_previewArc != null && viewport != null)
    //                    {
    //                        try { viewport.RemoveObject(_previewArc); } catch { }
    //                        _previewArc = null;
    //                    }

    //                    if (previewPoint.HasValue && viewport != null && Context != null)
    //                    {
    //                        // Two possible preview computations:
    //                        // - non-PT3: center & start already known -> radius from start, angles from center
    //                        // - PT3: start, second are known and previewPoint is the end -> compute circle through three points
    //                        OpenCAD.Geometry.Point3D previewCenter = default;
    //                        double radius = 0.0;
    //                        double startAngle = 0.0;
    //                        double endAngle = 0.0;
    //                        bool haveCircle = false;

    //                        if (_arcInputMode == ArcInputMode.PT3)
    //                        {
    //                            // compute circle from three points: _start, _second, previewPoint
    //                            if (GeometricCalculator.TryGetCircleThroughThreePoints(_start, _second, previewPoint.Value, out var c, out var r))
    //                            {
    //                                previewCenter = c;
    //                                radius = r;
    //                                startAngle = previewCenter.AngleTo(_start);
    //                                endAngle = previewCenter.AngleTo(previewPoint.Value);

    //                                // Ensure the CCW arc from startAngle to endAngle includes _second.
    //                                // If it doesn't, swap start/end so the arc contains the second point.
    //                                double aStart = NormalizeAngle(startAngle);
    //                                double aEnd = NormalizeAngle(endAngle);
    //                                double aSecond = NormalizeAngle(previewCenter.AngleTo(_second));
    //                                if (!IsAngleBetweenCCW(aStart, aSecond, aEnd))
    //                                {
    //                                    // swap so that arc chosen CCW passes through second
    //                                    double tmp = aStart;
    //                                    aStart = aEnd;
    //                                    aEnd = tmp;
    //                                    startAngle = aStart;
    //                                    endAngle = aEnd;
    //                                }

    //                                haveCircle = true;
    //                            }
    //                            else
    //                            {
    //                                // cannot form circle (colinear) -> skip preview arc
    //                                haveCircle = false;
    //                            }
    //                        }
    //                        else
    //                        {
    //                            // existing behavior (covers Last as well since center is precomputed)
    //                            previewCenter = _center;
    //                            radius = _center.DistanceTo(_start);
    //                            startAngle = _center.AngleTo(_start);
    //                            endAngle = _center.AngleTo(previewPoint.Value);
    //                            haveCircle = true;
    //                        }

    //                        if (haveCircle)
    //                        {
    //                            var doc = Context.GetDocument();
    //                            if (doc != null)
    //                            {
    //                                var arc = new Arc(previewCenter, radius, startAngle, endAngle, doc);
    //                                viewport.AddObject(arc);
    //                                _previewArc = arc;
    //                                viewport.Refresh();
    //                            }
    //                        }
    //                        else
    //                        {
    //                            if (viewport != null)
    //                                viewport.Refresh();
    //                        }
    //                    }
    //                    else
    //                    {
    //                        if (viewport != null)
    //                            viewport.Refresh();
    //                    }
    //                }
    //                catch
    //                {
    //                    // ignore preview errors
    //                }
    //            };

    //            // Attach the arc preview handler on the UI thread after BeginPreview has captured cached providers
    //            if (Context != null && _arcPreviewHandler != null)
    //            {
    //                Context.PostToUI(() =>
    //                {
    //                    if (CachedViewModel != null)
    //                        CachedViewModel.PropertyChanged += _arcPreviewHandler;
    //                });
    //            }

    //            try
    //            {
    //                // Allow the user to either type an angle or pick a point (angle computed from center to picked point)
    //                var result = await GetAngle(OpenCADStrings.ArcEndAnglePrompt);

    //                if (result == null || double.IsNaN(result.DoubleValue))
    //                {
    //                    // user cancelled or invalid -> treat as cancel
    //                    return null;
    //                }
    //                var angle = result.DoubleValue;

    //                // compute endpoint from center, radius and angle
    //                double radiusVal = _center.DistanceTo(_start);
    //                double x = _center.X + radiusVal * Math.Cos(angle);
    //                double y = _center.Y + radiusVal * Math.Sin(angle);
    //                var endPoint = new OpenCAD.Geometry.Point3D(x, y, _center.Z);

    //                return endPoint;
    //            }
    //            finally
    //            {
    //                // cleanup preview handler and preview arc
    //                try
    //                {
    //                    if (Context != null && _arcPreviewHandler != null)
    //                    {
    //                        Context.PostToUI(() =>
    //                        {
    //                            if (CachedViewModel != null)
    //                                CachedViewModel.PropertyChanged -= _arcPreviewHandler;
    //                        });
    //                    }
    //                }
    //                catch { }

    //                try
    //                {
    //                    if (_previewArc != null && Context != null)
    //                    {
    //                        // Remove preview arc on UI thread via Context to avoid cross-thread access
    //                        Context.PostToUI(() =>
    //                        {
    //                            try
    //                            {
    //                                if (_previewArc != null && viewport != null)
    //                                {
    //                                    viewport.RemoveObject(_previewArc);
    //                                    _previewArc = null;
    //                                }
    //                            }
    //                            catch { }
    //                        });
    //                    }
    //                }
    //                catch { }

    //                CommitPreview();
    //            }
    //        }

    //        private Point3D GetCenterFromStartEndRadius(double radius)
    //        {
    //            // chord vector from start to end
    //            var chord = _end - _start; // Vector3D
    //            double d = chord.Length;

    //            // Guard: identical points -> cannot determine a unique center
    //            if (d < 1e-12)
    //            {
    //                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
    //                return _start;
    //            }

    //            double absRadius = Math.Abs(radius);

    //            // radius must be at least half the chord length
    //            if (absRadius < d * 0.5)
    //            {
    //                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
    //                // return midpoint as a safe fallback
    //                return GeometricCalculator.MidPoint(_start, _end);
    //            }

    //            // midpoint of the chord
    //            var midpoint = GeometricCalculator.MidPoint(_start, _end);

    //            // distance from midpoint to center along the perpendicular bisector
    //            double halfChord = d * 0.5;
    //            double h = Math.Sqrt(Math.Max(0.0, absRadius * absRadius - halfChord * halfChord));

    //            // perpendicular direction in XY plane
    //            var perp = new Vector3D(-chord.Y, chord.X, 0.0);
    //            var perpUnit = perp.Normalized;

    //            // sign chosen by radius sign: positive -> one side, negative -> opposite side
    //            double sign = radius >= 0.0 ? 1.0 : -1.0;

    //            var offset = perpUnit * (h * sign);

    //            // Keep Z consistent with midpoint (offset.Z is zero since perp.Z == 0)
    //            return midpoint + offset;
    //        }

    //        /// <summary>
    //        /// Normalize angle to [0, 2*PI)
    //        /// </summary>
    //        private static double NormalizeAngle(double angle)
    //        {
    //            double twoPi = Math.PI * 2.0;
    //            double a = angle % twoPi;
    //            if (a < 0) a += twoPi;
    //            return a;
    //        }

    //        /// <summary>
    //        /// Returns true if moving CCW from start to end (inclusive) the test angle is encountered.
    //        /// Angles must be normalized to [0,2PI) or the function will normalize them.
    //        /// </summary>
    //        private static bool IsAngleBetweenCCW(double start, double test, double end)
    //        {
    //            start = NormalizeAngle(start);
    //            test = NormalizeAngle(test);
    //            end = NormalizeAngle(end);

    //            if (start <= end)
    //                return test >= start && test <= end;

    //            // wrapped case: e.g., start=300deg, end=60deg -> true if test >= start || test <= end
    //            return test >= start || test <= end;
    //        }

    //        private async Task<Point3D?> GetArcPoint(Point3D? basePoint = null)
    //        {
    //            var step = _step switch
    //            {
    //                ArcInputStep.CenterPoint => OpenCADStrings.Center,
    //                ArcInputStep.StartPoint => OpenCADStrings.StartPoint,
    //                ArcInputStep.EndPoint => OpenCADStrings.EndPoint,
    //                ArcInputStep.Radius => OpenCADStrings.Radius,
    //                ArcInputStep.SecondPoint => OpenCADStrings.SecondPoint,
    //                _ => throw new NotImplementedException(),
    //            };
    //            if (basePoint.HasValue)
    //            {
    //                BasePoint = basePoint.Value;
    //            }
    //            var prompt = string.Format(OpenCADStrings.ArcPointPrompt, step);
    //            var result = await GetPoint(prompt);
    //            if (result != null && result.Point is OpenCAD.Geometry.Point3D point)
    //                return point;

    //            return null;
    //        }

    //        public override bool ProcessInput(string input)
    //        {
    //            // Route keyboard input to shared helper in base (created during Initialize)
    //            if (_inputHelper != null)
    //            {
    //                return _inputHelper.ProcessKeyboardInput(input);
    //            }

    //            return false;
    //        }

    //        private void CreateArc()
    //        {
    //            Arc? arc = null;

    //            var startAngle = _center.AngleTo(_start);
    //            var endAngle = _center.AngleTo(_end);
    //            var radius = _center.DistanceTo(_start);

    //            // Get the document to apply current properties
    //            var document = Context?.GetDocument();
    //            arc = new Arc(_center, radius, startAngle, endAngle, document);
    //            CreateObject(arc);
    //        }

    //        protected override string GetUndoCreateString(OpenCADObject obj)
    //        {
    //            if (obj is not Arc arc)
    //                return base.GetUndoCreateString(obj);

    //            return string.Format(
    //                        $"Create Arc at ({_center.X:F3}, {_center.Y:F3}, {_center.Z:F3}), " +
    //                        $"Radius: {arc.Radius:F3}, " +
    //                        $"Angles: {arc.StartAngle * 180 / Math.PI:F1}° to {arc.EndAngle * 180 / Math.PI:F1}°");
    //;
    //        }

    //        public override void Cancel()
    //        {
    //            base.Cancel();
    //            _cancellationTokenSource?.Cancel();
    //            CurrentPrompt = string.Empty;
    //        }
    //    }
}