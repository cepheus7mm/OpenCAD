using OpenCAD;
using OpenCAD.Dimensions;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.Dimensions.Linear.Modes
{
    /// <summary>
    /// Prompts the user to select the dimension type (Aligned, Horizontal, Vertical, Rotated).
    /// Default is Aligned. Follows AssociateMode/SecondPointMode.
    /// </summary>
    public class TypeMode : LinearDimensionModeBase
    {
        private readonly Point3D _firstPoint;
        private readonly Point3D _secondPoint;

        private DimensionTypeLinear? _selectedType;

        public TypeMode(Point3D firstPoint, Point3D secondPoint)
        {
            _firstPoint = firstPoint;
            _secondPoint = secondPoint;
        }

        public override string Prompt => "Specify dimension type";

        public override UserInputType UserInputType => UserInputType.Enum;

        public override object? GetDefaultValue() => DimensionTypeLinear.Aligned;

        public override bool IsComplete => _selectedType.HasValue;

        public void SetType(DimensionTypeLinear type) => _selectedType = type;

        public override ILinearDimensionMode Apply(LinearDimensionCommand command)
        {
            var type = _selectedType!.Value;
            command.SetDimensionType(type);

            switch (type)
            {
                case DimensionTypeLinear.Horizontal:
                    command.SetDirection(Vector3D.UnitX);
                    return new OffsetMode(_firstPoint, _secondPoint, Vector3D.UnitX);

                case DimensionTypeLinear.Vertical:
                    command.SetDirection(Vector3D.UnitY);
                    return new OffsetMode(_firstPoint, _secondPoint, Vector3D.UnitY);

                case DimensionTypeLinear.Aligned:
                    var aligned = (_secondPoint - _firstPoint).Normalized;
                    command.SetDirection(aligned);
                    return new OffsetMode(_firstPoint, _secondPoint, aligned);

                case DimensionTypeLinear.Rotated:
                    return new DirectionMode(_firstPoint, _secondPoint);

                default:
                    return this;
            }
        }
    }
}