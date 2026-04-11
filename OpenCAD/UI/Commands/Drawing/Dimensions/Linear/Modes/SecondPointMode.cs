using OpenCAD.Geometry;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.Dimensions.Linear.Modes
{
    public class SecondPointMode : LinearDimensionModeBase
    {
        private readonly Point3D _firstPoint;

        public SecondPointMode(Point3D firstPoint)
        {
            _firstPoint = firstPoint;
        }

        public override string Prompt => "Specify second extension line origin";

        public override string[] Keywords { get; }
            = Array.Empty<string>();

        public override bool AllowLastPoint => false;

        public override Point3D? GetBasePoint() => _firstPoint;

        public override bool IsComplete => InputPoint.HasValue;

        public override ILinearDimensionMode Apply(LinearDimensionCommand command)
        {
            command.SetSecondPoint(InputPoint!.Value);
            command.SetDirection(InputPoint.Value - _firstPoint);

            return new DirectionMode(_firstPoint, InputPoint!.Value);
        }
    }
}