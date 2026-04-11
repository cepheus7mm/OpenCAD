using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.Dimensions.Linear.Modes
{
    public class DirectionMode : LinearDimensionModeBase
    {
        private readonly Point3D _firstPoint;
        private readonly Point3D _secondPoint;

        public DirectionMode(Point3D firstPoint, Point3D secondPoint)
        {
            _firstPoint = firstPoint;
            _secondPoint = secondPoint;

        }

        public override string Prompt => "Specify dimension line direction [Horizontal/Vertical/Aligned]";

        public override string[] Keywords { get; }
            = new[] { "HORIZONTAL", "VERTICAL", "ALIGNED" };

        public override Point3D? GetBasePoint() => _firstPoint;

        public override bool IsComplete => InputDouble.HasValue;

        public override UserInputType UserInputType => UserInputType.Angle;

        public override ILinearDimensionMode Apply(LinearDimensionCommand command)
        {
            var x = Math.Cos(InputDouble!.Value);
            var y = Math.Sin(InputDouble!.Value);
            var direction = new Vector3D(x,y,0);
            command.SetDirection(direction);

            return new OffsetMode(_firstPoint, _secondPoint, direction);
        }

        private Vector3D ComputeDirection(Point3D directionPoint)
        {
            var delta = _secondPoint - _firstPoint;
            var toPoint = directionPoint - _firstPoint;

            // If the user picked closer to horizontal displacement, use horizontal
            if (Math.Abs(toPoint.Y) > Math.Abs(toPoint.X))
                return Vector3D.UnitY;

            return Vector3D.UnitX;
        }
    }
}