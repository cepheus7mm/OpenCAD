using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using System;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Drawing.CircleCommand.Modes;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.Drawing.CircleCommand
{
    /// <summary>
    /// Command to create an Circle by specifying center, start point (defines radius and start angle),
    /// and end point (defines end angle)
    /// </summary>
    [InputCommand("circle", "Create a circle", "ci")]
    public class CircleCommand : CommandBase
    {
        private ICircleCreationMode _mode;

        public override async Task Execute()
        {
            var document = Context?.GetDocument();
            _mode = new CenterRadiusMode(document);
            BasePoint = Point3D.NotAPoint;

            while (!_mode.IsComplete)
            {
                BasePoint = _mode.GetBasePoint();
                var input = await GetPoint(_mode.Prompt, null, _mode.Keywords);

                if (input.Keyword != null)
                {
                    _mode = SwitchMode(_mode, input.Keyword, document);
                    continue;
                }

                if (input.Point != null)
                {
                    _mode.SetPoint(input.Point.Value);
                    UpdatePreview();
                }
            }

            var circle = _mode.CreateCircle();
            CreateObject(circle);
            RaiseCommandCompleted();
        }

        private ICircleCreationMode SwitchMode(ICircleCreationMode currentMode, string keyword, OpenCADDocument document)
        {
            return keyword.ToUpperInvariant() switch
            {
                "RAD" => new CenterRadiusMode(document),
                "DIA" => new CenterDiameterMode(document),
                "2PT" => new TwoPointCircleMode(document),
                "3PT" => new ThreePointCircleMode(document),
                _ => currentMode
            };
        }

        //private void UpdatePreview()
        //{
        //    var preview = _mode.GetPreview(TargetPoint);
        //    PreviewManager.ShowPreview(preview);
        //}

        protected override IEnumerable<OpenCADObject> ComputePreviewObjects()
        {
            yield return _mode.GetPreview(TargetPoint);
        }

        //private enum CircleInputStep
        //{
        //    CenterPoint,
        //    FirstPoint,
        //    Radius,
        //    Diameter,
        //    SecondPoint,
        //    ThirdPoint,
        //}

        //private enum CircleInputMode
        //{
        //    RAD,
        //    DIA,
        //    PT3,
        //}

        //private CircleInputStep _step;
        //private CircleInputMode _circleInputMode = CircleInputMode.RAD;
        //private Point3D _center = Point3D.NotAPoint;
        //private double _radius = double.NaN;

        //// preview Circle instance (removed/added during preview)
        //private Circle? _previewCircle;
        //private PropertyChangedEventHandler? _circlePreviewHandler;

        //// points for PT3 mode
        //private Point3D _start = Point3D.NotAPoint;
        //private Point3D _second = Point3D.NotAPoint;
        //private Point3D _end = Point3D.NotAPoint;

        //public override bool IsMultiStep => true;

        //public override async Task Initialize(ICommandContext context)
        //{
        //    await base.Initialize(context);
        //    _circlePreviewHandler = OnCirclePreview;
        //}

        //public override async Task Execute()
        //{
        //    _cancellationTokenSource = new CancellationTokenSource();

        //    try
        //    {
        //        _step = CircleInputStep.CenterPoint;
        //        var result = await GetInitialInput();
        //        if (!result.HasValue)  // Changed: should cancel if NO result
        //        {
        //            Cancel();
        //            return;
        //        }
        //        SetCirclePoint(result.Value);

        //        // Normal flow for non-Last modes
        //        result = await GetSecondInput();
        //        if (!result.HasValue)  // Changed: should cancel if NO result
        //        {
        //            Cancel();
        //            return;
        //        }
        //        SetCirclePoint(result.Value);

        //        if (_circleInputMode == CircleInputMode.PT3)
        //        {
        //            result = await GetLastInput();
        //            if (!result.HasValue)  // Changed: should cancel if NO result
        //            {
        //                Cancel();
        //                return;
        //            }
        //            SetCirclePoint(result.Value);

        //            if (!GeometricCalculator.TryGetCircleThroughThreePoints(_start, _second, _end, out var c, out var r))
        //            {
        //                Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
        //                Cancel();
        //                return;
        //            }
        //            _center = c;
        //            _radius = r;
        //        }

        //        // Create the Circle
        //        CreateCircle();

        //        // Command completed successfully
        //        Context?.OutputMessage("Circle command completed.");
        //        RaiseCommandCompleted();
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        Cancel();
        //    }
        //}

        //private async Task<Point3D?> GetSecondInput()
        //{
        //    // Determine next step based on input mode
        //    _step = _circleInputMode switch
        //    {
        //        CircleInputMode.RAD => CircleInputStep.Radius,
        //        CircleInputMode.DIA => CircleInputStep.Diameter,
        //        CircleInputMode.PT3 => CircleInputStep.SecondPoint,
        //        _ => throw new NotImplementedException()
        //    };

        //    bool previewNeeded = _circleInputMode == CircleInputMode.RAD || _circleInputMode == CircleInputMode.DIA;
        //    if (previewNeeded)
        //    {
        //        BeginPreview();

        //        // Attach the Circle preview handler on the UI thread after BeginPreview has captured cached providers
        //        if (Context != null && _circlePreviewHandler != null)
        //        {
        //            Context.PostToUI(() =>
        //            {
        //                if (CachedViewModel != null)
        //                    CachedViewModel.PropertyChanged += _circlePreviewHandler;
        //            });
        //        } 
        //    }
        //    try
        //    {
        //        return await GetCirclePoint();
        //    }
        //    finally
        //    {
        //        if (previewNeeded)
        //        {
        //            // cleanup preview handler and preview Circle
        //            CleanUpPreview();
        //        }
        //    }
        //}

        //private void CleanUpPreview()
        //{
        //    try
        //    {
        //        if (Context != null && _circlePreviewHandler != null)
        //        {
        //            Context.PostToUI(() =>
        //            {
        //                if (CachedViewModel != null)
        //                    CachedViewModel.PropertyChanged -= _circlePreviewHandler;
        //            });
        //        }
        //    }
        //    catch { }
        //    try
        //    {
        //        if (_previewCircle != null && Context != null)
        //        {
        //            // Remove preview Circle on UI thread via Context to avoid cross-thread access
        //            Context.PostToUI(() =>
        //            {
        //                try
        //                {
        //                    if (_previewCircle != null && viewport != null)
        //                    {
        //                        viewport.RemoveObject(_previewCircle);
        //                        _previewCircle = null;
        //                    }
        //                }
        //                catch { }
        //            });
        //        }
        //    }
        //    catch { }
        //    CommitPreview();
        //}

        //private void SetCirclePoint(Point3D result)
        //{
        //    if (_step == CircleInputStep.Radius)
        //    {
        //        _radius = result.X;
        //        return;
        //    }
        //    else if (_step == CircleInputStep.Diameter)
        //    {
        //        _radius = result.X / 2.0;
        //        return;
        //    }

        //    BasePoint = result;

        //    _ = _step switch
        //    {
        //        CircleInputStep.CenterPoint => _center = result,
        //        CircleInputStep.FirstPoint => _start = result,
        //        CircleInputStep.ThirdPoint => _end = result,
        //        CircleInputStep.SecondPoint => _second = result,
        //        _ => throw new NotImplementedException()
        //    };
        //}

        //private async Task<Point3D?> GetInitialInput()
        //{
        //    BasePoint = Point3D.NotAPoint;
        //    var step = _step switch
        //    {
        //        CircleInputStep.CenterPoint => OpenCADStrings.Center,
        //        CircleInputStep.FirstPoint => OpenCADStrings.FirstPointPrompt,
        //        _ => throw new NotImplementedException(),
        //    };
        //    var keyWords = new string[] { "Rad", "Dia", "3PT" };
        //    var result = await GetPoint(string.Format(OpenCADStrings.CirclePointPrompt, step), null, keyWords);

        //    if (result == null || result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
        //        throw new OperationCanceledException();

        //    if (result.Keyword is string keyWord)
        //    {
        //        KeyWordInput(keyWord);

        //        return await GetCirclePoint();
        //    }

        //    else if (result.Point is Point3D point)
        //        return point;

        //    return null;
        //}

        //private void KeyWordInput(string? keyWord)
        //{
        //    if (string.IsNullOrWhiteSpace(keyWord))
        //        return;

        //    // Handle specific keywords
        //    switch (keyWord.ToUpperInvariant())
        //    {
        //        case "RAD":
        //            _circleInputMode = CircleInputMode.RAD;
        //            _step = CircleInputStep.CenterPoint;
        //            break;
        //        case "DIA":
        //            _circleInputMode = CircleInputMode.DIA;
        //            _step = CircleInputStep.CenterPoint;
        //            break;
        //        case "3PT":
        //            _circleInputMode = CircleInputMode.PT3;
        //            _step = CircleInputStep.FirstPoint;
        //            break;
        //        default:
        //            return;
        //    }
        //}

        //private async Task<Point3D?> GetLastInput()
        //{
        //    // Determine next step based on input mode
        //    _step = _circleInputMode switch
        //    {
        //        CircleInputMode.PT3 => CircleInputStep.ThirdPoint,
        //        _ => throw new NotImplementedException()
        //    };

        //    BeginPreview();

        //    // Attach the Circle preview handler on the UI thread after BeginPreview has captured cached providers
        //    if (Context != null && _circlePreviewHandler != null)
        //    {
        //        Context.PostToUI(() =>
        //        {
        //            if (CachedViewModel != null)
        //                CachedViewModel.PropertyChanged += _circlePreviewHandler;
        //        });
        //    }

        //    try
        //    {
        //        var result = await GetCirclePoint();
        //        return result;
        //    }
        //    finally
        //    {
        //        // cleanup preview handler and preview Circle
        //        CleanUpPreview();
        //    }
        //}

        //private Point3D GetCenterFromStartEndRadius(double radius)
        //{
        //    // chord vector from start to end
        //    var chord = _end - _start; // Vector3D
        //    double d = chord.Length;

        //    // Guard: identical points -> cannot determine a unique center
        //    if (d < 1e-12)
        //    {
        //        Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
        //        return _start;
        //    }

        //    double absRadius = Math.Abs(radius);

        //    // radius must be at least half the chord length
        //    if (absRadius < d * 0.5)
        //    {
        //        Context?.OutputMessage(OpenCADStrings.InvalidPointInput);
        //        // return midpoint as a safe fallback
        //        return new Point3D(
        //            (_start.X + _end.X) * 0.5,
        //            (_start.Y + _end.Y) * 0.5,
        //            (_start.Z + _end.Z) * 0.5);
        //    }

        //    // midpoint of the chord
        //    var midpoint = new Point3D(
        //        (_start.X + _end.X) * 0.5,
        //        (_start.Y + _end.Y) * 0.5,
        //        (_start.Z + _end.Z) * 0.5);

        //    // distance from midpoint to center along the perpendicular bisector
        //    double halfChord = d * 0.5;
        //    double h = Math.Sqrt(Math.Max(0.0, absRadius * absRadius - halfChord * halfChord));

        //    // perpendicular direction in XY plane
        //    var perp = new Vector3D(-chord.Y, chord.X, 0.0);
        //    var perpUnit = perp.Normalized;

        //    // sign chosen by radius sign: positive -> one side, negative -> opposite side
        //    double sign = radius >= 0.0 ? 1.0 : -1.0;

        //    var offset = perpUnit * (h * sign);

        //    // Keep Z consistent with midpoint (offset.Z is zero since perp.Z == 0)
        //    return midpoint + offset;
        //}

        //private async Task<Point3D?> GetCirclePoint(Point3D? basePoint = null)
        //{
        //    if (_step == CircleInputStep.Radius || _step == CircleInputStep.Diameter)
        //    {
        //        return await GetDistanceAsPoint();
        //    }


        //    var step = _step switch
        //    {
        //        CircleInputStep.CenterPoint => OpenCADStrings.Center,
        //        CircleInputStep.FirstPoint => OpenCADStrings.StartPoint,
        //        CircleInputStep.SecondPoint => OpenCADStrings.SecondPoint,
        //        CircleInputStep.ThirdPoint => OpenCADStrings.EndPoint,
        //        _ => throw new NotImplementedException(),
        //    };
        //    if (basePoint != null)
        //    {
        //        BasePoint = Point3D.NotAPoint;
        //    }
        //    var prompt = string.Format(OpenCADStrings.CirclePointPrompt, step);
        //    var result = await GetPoint(prompt);
        //    if (result != null && result.Point is Point3D point)
        //        return point;

        //    return null;
        //}

        //private async Task<Point3D?> GetDistanceAsPoint()
        //{
        //    BasePoint = _center;
        //    var prompt = string.Format(OpenCADStrings.CirclePointPrompt, _step);
        //    var result = await GetDistance(prompt);
        //    if (result == null || result.ResultType == InputHelpers.InputResult.InputResultType.Cancel)
        //        return null;
        //    double distance = result.DoubleValue;

        //    // return the distance as a point along the X axis from the base point
        //    return new Point3D(distance, 0, 0);
        //}

        //public override bool ProcessInput(string input)
        //{
        //    // Route keyboard input to shared helper in base (created during Initialize)
        //    if (_inputHelper != null)
        //    {
        //        return _inputHelper.ProcessKeyboardInput(input);
        //    }

        //    return false;
        //}

        ///// <summary>
        ///// Calculate the distance between two points (used for radius)
        ///// </summary>
        //private double CalculateDistance(Point3D center, Point3D point)
        //{
        //    double dx = point.X - center.X;
        //    double dy = point.Y - center.Y;
        //    return Math.Sqrt(dx * dx + dy * dy);
        //}

        //private void CreateCircle()
        //{
        //    Circle Circle = null;

        //    // Get the document to apply current properties
        //    var document = Context?.GetDocument();
        //    Circle = new Circle(_center, _radius, document);
        //    CreateObject(Circle);
        //}

        //protected override string GetUndoCreateString(OpenCADObject obj)
        //{
        //    if (obj is Circle)
        //    {
        //        return string.Format($"Create Circle at ({_center.X:F3}, {_center.Y:F3}, {_center.Z:F3}), Radius: {_radius:F3}");
        //    }
        //    return base.GetUndoCreateString(obj);
        //}

        //public override void Cancel()
        //{
        //    base.Cancel();
        //    _cancellationTokenSource?.Cancel();
        //    CurrentPrompt = string.Empty;
        //}

        //private void OnCirclePreview(object? sender, PropertyChangedEventArgs e)
        //{
        //    if (e.PropertyName != nameof(ViewportViewModel.PreviewPoint))
        //        return;

        //    var previewPoint = CachedViewModel?.PreviewPoint;

        //    // Marshal all viewport access to the UI thread
        //    Context?.PostToUI(() =>
        //    {
        //        try
        //        {
        //            // remove previous preview Circle
        //            if (_previewCircle != null && viewport != null)
        //            {
        //                try { viewport.RemoveObject(_previewCircle); } 
        //                catch { }
        //                _previewCircle = null;
        //            }

        //            if (previewPoint.HasValue && viewport != null && Context != null)
        //            {
        //                // Two possible preview computations:
        //                // - non-PT3: center & start already known -> radius from start, angles from center
        //                // - PT3: start, second are known and previewPoint is the end -> compute circle through three points
        //                Point3D previewCenter = Point3D.NotAPoint;
        //                double radius = 0.0;
        //                bool haveCircle = false;

        //                if (_circleInputMode == CircleInputMode.PT3)
        //                {
        //                    // compute circle from three points: _start, _second, previewPoint
        //                    if (GeometricCalculator.TryGetCircleThroughThreePoints(_start, _second, previewPoint.Value, out var c, out var r))
        //                    {
        //                        previewCenter = c;
        //                        radius = r;
        //                        haveCircle = true;
        //                    }
        //                    else
        //                    {
        //                        // cannot form circle (colinear) -> skip preview Circle
        //                        haveCircle = false;
        //                    }
        //                }
        //                else
        //                {
        //                    // existing behavior (covers Last as well since center is precomputed)
        //                    previewCenter = _center;
        //                    radius = CalculateDistance(_center, previewPoint.Value);
        //                    if (_circleInputMode == CircleInputMode.DIA)
        //                    {
        //                        radius /= 2.0;
        //                    }
        //                    haveCircle = true;
        //                }

        //                if (haveCircle)
        //                {
        //                    var doc = Context.GetDocument();
        //                    if (doc != null)
        //                    {
        //                        var circle = new Circle(previewCenter, radius, doc);
        //                        circle.IsPreviewGeometry = true;
        //                        viewport.AddObject(circle);
        //                        _previewCircle = circle;
        //                        viewport.Refresh();
        //                    }
        //                }
        //                else
        //                {
        //                    if (viewport != null)
        //                        viewport.Refresh();
        //                }
        //            }
        //            else
        //            {
        //                if (viewport != null)
        //                    viewport.Refresh();
        //            }
        //        }
        //        catch
        //        {
        //            // ignore preview errors
        //        }
        //    });
        //}
    }
}