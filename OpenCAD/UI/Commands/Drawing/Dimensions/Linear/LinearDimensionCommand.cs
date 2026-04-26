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
        private DimensionTypeLinear _dimensionType = DimensionTypeLinear.Aligned;

        // -------------------------
        // Associative state
        // -------------------------
        private Guid _associatedEntityId = Guid.Empty;

        public LinearDimensionCommand()
        {
        }

        public override async Task Initialize(ICommandContext context, CommandArgs? args = null)
        {
            await base.Initialize(context, args);
            CreateDimensionStyle();
            CreateDimensionUnits();
        }

        public override bool IsMultiStep => true;

        public Point3D? FirstPoint => _firstPoint;
        public Point3D? SecondPoint => _secondPoint;

        public OpenCADDocument? GetDocument() => Document;
        public Point3D GetTarget() => TargetPoint;

        // -------------------------
        // Entity filter for AssociateMode
        // -------------------------
        private static readonly Func<OpenCADObject, (bool, string?)> _lineFilter =
            obj => obj is Line
                ? (true, null)
                : (false, "Object is not a line.");

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
            _associatedEntityId = Guid.Empty;

            _mode = new AssociateMode();

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
                    UserInputType.Entity => await GetEntity(inputParams, filter: _lineFilter),
                    UserInputType.Enum => await GetEnum<DimensionTypeLinear>(inputParams),
                    _ => throw new InvalidOperationException("Unsupported input type")
                };

                if (result.IsCancel)
                    break;

                if (result.IsKeyword)
                {
                    if (_mode is TypeMode typeMode && Enum.TryParse<DimensionTypeLinear>(result.Keyword, true, out var dimType))
                    {
                        typeMode.SetType(dimType);
                        if (_mode.IsComplete)
                            _mode = _mode.Apply(this);
                        continue;
                    }

                    _mode = HandleKeyword(_mode, result.Keyword);
                    continue;
                }

                if (result.IsObject && _mode is AssociateMode associateMode)
                {
                    associateMode.SetEntity(result.Object!);

                    if (_mode.IsComplete)
                        _mode = _mode.Apply(this);

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

            if (_mode is FinishedMode && _firstPoint.HasValue && _secondPoint.HasValue && _direction.HasValue)
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

        public void SetDimensionType(DimensionTypeLinear type) => _dimensionType = type;

        /// <summary>
        /// Sets both extension line origins and the associated entity ID from a selected line.
        /// Called by AssociateMode when the user selects a line for associative dimensioning.
        /// </summary>
        public void SetAssociatedEntity(ICurve curve)
        {
            _firstPoint = curve.StartPoint;
            _secondPoint = curve.EndPoint;
            _associatedEntityId = ((OpenCADObject)curve).ID;
        }

        // -------------------------
        // Dimension creation
        // -------------------------

        private void CreateDimension()
        {
            var definition = GetDimensionDefinition();

            var dimension = new LinearDimension(definition);
            CreateObject(dimension);
        }

        internal DimensionDefinition GetDimensionDefinition(float? offsetOverride = null)
        {
            var dir2 = new Vector2((float)_direction!.Value.X, (float)_direction!.Value.Y);
            var normal = new Vector2(-dir2.Y, dir2.X);

            var definition = new DimensionDefinition
            {
                Ref1 = new EntityReference(_associatedEntityId, "Start", _firstPoint),
                Ref2 = new EntityReference(_associatedEntityId, "End", _secondPoint),
                Direction = dir2,
                Normal = normal,
                Offset = offsetOverride ?? (float)_offset,
                Style = _dimensionStyle,
                Units = _dimensionUnits,
                Document = Document,
                DimensionType = _dimensionType
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
                case "P":
                case "POINTS":
                    return new FirstPointMode();
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