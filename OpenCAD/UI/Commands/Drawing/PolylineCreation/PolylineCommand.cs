using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UI.Commands.Drawing.PolylineCreation.Modes;
using UI.Commands.InputHelpers;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.PolylineCreation
{
    [InputCommand("polyline", "Create a polyline", "pl")]
    public class PolylineCommand : CommandBase
    {
        private readonly List<PolylineVertex> _vertices = new();

        private bool _isClosed = false;
        private CancellationTokenSource? _cts;

        private PolylineVertex? _previewVertex = null;

        private IPolylineCreationMode? _mode;
        public double CurrentStartWidth { get; private set; } = 0.0;
        public double CurrentEndWidth { get; private set; } = 0.0;

        public void SetCurrentWidths(double start, double end)
        {
            CurrentStartWidth = start;
            CurrentEndWidth = end;
        }

        public override bool IsMultiStep => true;

        public Point3D? FirstPoint => _vertices.Count > 0 ? _vertices[0].Position : null;
        public Point3D? LastPoint => _vertices.Count > 0 ? _vertices[^1].Position : null;

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
        }

        public OpenCADDocument? GetDocument() => document;

        public Point3D GetTarget() => TargetPoint;

        public override async Task Execute()
        {
            _cts = new CancellationTokenSource();
            _vertices.Clear();
            _isClosed = false;

            _mode = new FirstPointMode();

            while (_mode is not FinishedMode)
            {
                BasePoint = _mode.GetBasePoint() ?? Point3D.NotAPoint;

                var inputParams = new InputParams
                {
                    Prompt = _mode.Prompt,
                    BasePoint = BasePoint,
                    Keywords = _mode.Keywords,
                    AllowLastPoint = _mode.AllowLastPoint,
                    DefaultValue = _mode.GetDefaultValue(),
                    AllowArbitraryInput = _mode.AllowArbitraryInput,
                    CancellationToken = _cts.Token
                };

                var result = _mode.UserInputType switch
                {
                    UserInputType.Point => await GetPoint(inputParams),
                    UserInputType.Distance => await GetDistance(inputParams),
                    UserInputType.Angle => await GetAngle(inputParams),
                    _ => throw new InvalidOperationException("Unsupported input type")
                };

                if (result.IsCancel)
                    break;

                if (result.IsKeyword)
                {
                    _mode = HandleKeyword(_mode, result.Keyword);
                    continue;
                }

                if (result.IsPoint)
                {
                    _mode.SetPoint(result.Point!.Value);

                    if (_mode.IsComplete)
                        _mode = _mode.Apply(this);
                }

                if (result.IsDefault)
                {
                    _mode.SetDouble(_mode.GetDefaultValue() is double d ? d : 0);
                    if (_mode.IsComplete)
                        _mode = _mode.Apply(this);
                }

                if (result.IsDouble)
                {
                    _mode.SetDouble(result.DoubleValue);
                    if (_mode.IsComplete)
                        _mode = _mode.Apply(this);
                }
            }

            // If we have at least 2 vertices, create the polyline
            if (_vertices.Count >= 2)
                CreatePolyline();
        }

        public override bool ProcessInput(string input)
        {
            if (_inputHelper != null)
            {
                _inputHelper.ProcessKeyboardInput(input);
                return IsCommandCompleted;
            }

            return false;
        }

        // -------------------------
        // Polyline construction API
        // -------------------------

        public void AddVertex(Point3D point, double bulge = 0)
        {
            if (Math.Abs(bulge) > 1e-12 && _vertices.Count > 0)
            {
                // Bulge belongs to the previous vertex (segment TO this new point)
                _vertices[^1].Bulge = bulge;
            }

            if (_vertices.Any())
                _vertices[^1].StartWidth = CurrentStartWidth;

            _vertices.Add(new PolylineVertex(document, point, 0));

            if (_vertices.Count > 1)
            {
                _vertices[^1].EndWidth = CurrentEndWidth;
                _vertices[^1].StartWidth = CurrentEndWidth;
            }
            CurrentStartWidth = CurrentEndWidth;
        }

        public void RemoveLastVertex()
        {
            if (_vertices.Count <= 1)
                return;

            _vertices.RemoveAt(_vertices.Count - 1);
            _vertices[^1].Bulge = 0; // Clear bulge of new last vertex since it no longer has a segment after it.
        }

        public void SetClosed(bool closed)
        {
            _isClosed = closed;
        }

        private void CreatePolyline()
        {
            var poly = new Polyline(document!);
            foreach (var vertex in _vertices)
                poly.AddVertex(vertex);

            CreateObject(poly);
        }

        // -------------------------
        // Keyword routing
        // -------------------------

        private IPolylineCreationMode HandleKeyword(IPolylineCreationMode mode, string keyword)
        {
            _previewVertex = null;

            keyword = keyword.ToUpperInvariant();

            switch (keyword)
            {
                case "A":
                case "ARC":
                    return new ArcMode(LastPoint);

                case "RADIUS":
                    return new ArcRadiusMode(LastPoint!.Value, GetTangent());

                case "ANGLE":
                    return new ArcAngleMode(LastPoint!.Value, GetTangent());

                case "U":
                case "UNDO":
                    var undoMode = new UndoMode();
                    var nextMode = undoMode.Apply(this);
                    UpdatePreview();
                    return nextMode;

                case "W":
                case "WIDTH":
                    return new WidthMode(this);

                case "C":
                case "CLOSE":
                    if (FirstPoint.HasValue && LastPoint.HasValue)
                        return new CloseMode(LastPoint.Value, FirstPoint.Value);
                    break;
            }

            return mode;
        }

        private Vector3D GetTangent()
        {
            if (_vertices.Count < 2)
                return Vector3D.UnitX;

            var segment = new PolylineSegment(_vertices[^2], _vertices[^1]);
            return segment.GetFirstDerivative(1).Normalized;
        }

        protected override IEnumerable<OpenCADObject> ComputePreviewObjects()
        {
            var preview = new List<OpenCADObject>();

            if (_mode is FirstPointMode)
                return preview;

            if (_mode is null)
                return preview;

            // New preview model: the mode updates command state by adding a transient vertex at the current mouse point.
            // (Arc mode will pass computed bulge.)

            _mode.GetPreview(this);

            if (_vertices.Count > 1)
            {
                var polyline = new Polyline(document!);
                foreach (var vertex in _vertices)
                    polyline.AddVertex(vertex);

                var lastVert = polyline.Vertices.Last();
                lastVert.Bulge = _previewVertex?.Bulge ?? 0;
                lastVert.StartWidth = CurrentStartWidth;
                polyline.AddVertex(_previewVertex ?? new PolylineVertex(document, TargetPoint, 0));

                preview.Add(polyline);
            }



            return preview;
        }

        internal void SetPreviewVertex(Point3D point3D, double bulge = 0)
        {
            _previewVertex = new PolylineVertex(document, point3D, bulge)
            {
                StartWidth = CurrentStartWidth,
                EndWidth = CurrentEndWidth,
                Index = 1
            };
        }
    }
}
