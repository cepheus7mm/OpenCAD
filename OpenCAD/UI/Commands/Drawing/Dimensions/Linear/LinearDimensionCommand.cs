using OpenCAD;
using OpenCAD.Dimensions;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using OpenCAD.Settings;
using OpenCAD.Styles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Drawing.Dimensions.Linear.Modes;
using UI.Commands.InputHelpers;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.Dimensions.Linear
{
    [InputCommand("dimlinear", "Create a linear dimension", "dli")]
    public class LinearDimensionCommand : CommandBase
    {
        private CancellationTokenSource? _cts;
        private ILinearDimensionMode? _mode;

        // -------------------------
        // Definition state
        // -------------------------
        private Point3D? _firstPoint;
        private Point3D? _secondPoint;
        private Vector3D? _direction;
        private double _offset;
        private OpenCADDimensionStyle? _dimensionStyle;
        private DimensionUnits? _dimensionUnits;

        public LinearDimensionCommand()
        {
        }

        public override async Task Initialize(ICommandContext context)
        {
            await base.Initialize(context);
            CreateDimensionStyle();
            CreateDimensionUnits();
        }

        public override bool IsMultiStep => true;

        public Point3D? FirstPoint => _firstPoint;
        public Point3D? SecondPoint => _secondPoint;

        public OpenCADDocument? GetDocument() => Document;
        public Point3D GetTarget() => TargetPoint;

        // -------------------------
        // Execute loop
        // -------------------------
        public override async Task Execute()
        {
            _cts = new CancellationTokenSource();
            _firstPoint = null;
            _secondPoint = null;
            _direction = null;
            _offset = 0;

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
                    DistanceProjection = _mode.DistanceProjection,
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

            if (_firstPoint.HasValue && _secondPoint.HasValue && _direction.HasValue)
                CreateDimension();
        }

        private void CreateDimensionStyle()
        {
            if (Document != null)
            {
                _dimensionStyle = Document.CurrentDimensionStyle ??
                    new OpenCADDimensionStyle()
                    {
                        ArrowType = ArrowType.Open,
                        ArrowSize = 0.1f,
                        TextStyle = Document?.CurrentTextStyle ?? throw new InvalidOperationException("No default text style available."),
                        TextHeight = 0.2f,
                        Offset = 0.1f,
                        ExtensionLength = 0.1f,
                        ExtensionOffset = 0.05f,
                        ExtensionBeyond = 0.05f
                    };
            }
        }

        private void CreateDimensionUnits()
        {
            _dimensionUnits = new DimensionUnits(LinearType.DecimalFeet, AngularType.Radians, 2);
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
        // Mode setters (called by modes)
        // -------------------------

        public void SetFirstPoint(Point3D point) => _firstPoint = point;

        public void SetSecondPoint(Point3D point) => _secondPoint = point;

        public void SetDirection(Vector3D direction) => _direction = direction;

        public void SetOffset(double offset) => _offset = offset;

        // -------------------------
        // Dimension creation
        // -------------------------

        private void CreateDimension()
        {
            var definition = GetDimensionDefinition();

            var dimension = new LinearDimension(definition);
            CreateObject(dimension);
        }

        internal DimensionDefinition GetDimensionDefinition()
        {
            var dir2 = new Vector2((float)_direction!.Value.X, (float)_direction!.Value.Y);
            var normal = new Vector2(-dir2.Y, dir2.X);

            var definition = new DimensionDefinition
            {
                Ref1 = new EntityReference(Guid.Empty, null, _firstPoint),
                Ref2 = new EntityReference(Guid.Empty, null, _secondPoint),
                Direction = dir2,
                Normal = normal,
                Offset = (float)_offset,
                Style = _dimensionStyle,
                Units = _dimensionUnits,
                Document = Document
            };
            return definition;
        }

        // -------------------------
        // Keyword routing
        // -------------------------

        private ILinearDimensionMode HandleKeyword(ILinearDimensionMode mode, string keyword)
        {
            keyword = keyword.ToUpperInvariant();

            switch (keyword)
            {
                case "H":
                case "HORIZONTAL":
                    SetDirection(Vector3D.UnitX);
                    if (_firstPoint.HasValue && _secondPoint.HasValue)
                        return new OffsetMode(_firstPoint.Value, _secondPoint.Value, Vector3D.UnitX);
                    break;

                case "V":
                case "VERTICAL":
                    SetDirection(Vector3D.UnitY);
                    if (_firstPoint.HasValue && _secondPoint.HasValue)
                        return new OffsetMode(_firstPoint.Value, _secondPoint.Value, Vector3D.UnitY);
                    break;

                case "A":
                case "ALIGNED":
                    if (_firstPoint.HasValue && _secondPoint.HasValue)
                    {
                        var aligned = (_secondPoint.Value - _firstPoint.Value).Normalized;
                        SetDirection(aligned);
                        return new OffsetMode(_firstPoint.Value, _secondPoint.Value, aligned);
                    }
                    break;
            }

            return mode;
        }

        // -------------------------
        // Preview
        // -------------------------

        protected override IEnumerable<OpenCADObject> ComputePreviewObjects()
        {
            var preview = new List<OpenCADObject>();

            if (_mode == null || !_mode.CanPreview)
                return preview;

            preview = _mode.GetPreview(this).ToList();

            // TODO: Build preview dimension geometry from current state + TargetPoint

            return preview;
        }
    }
}