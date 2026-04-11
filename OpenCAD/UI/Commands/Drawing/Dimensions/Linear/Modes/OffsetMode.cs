using OpenCAD;
using OpenCAD.Dimensions;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.Dimensions.Linear.Modes
{
    public class OffsetMode : LinearDimensionModeBase
    {
        private readonly Point3D _firstPoint;
        private readonly Point3D _secondPoint;
        private readonly Vector3D _direction;

        public OffsetMode(Point3D firstPoint, Point3D secondPoint, Vector3D direction)
        {
            _firstPoint = firstPoint;
            _secondPoint = secondPoint;
            _direction = direction;
        }

        public override string Prompt => "Specify dimension line location";

        public override string[] Keywords { get; }
            = Array.Empty<string>();

        public override Point3D? GetBasePoint() => _firstPoint;

        public override bool IsComplete => InputDouble.HasValue;

        public override bool CanPreview => true;

        public override UserInputType UserInputType => UserInputType.Distance;

        public override Func<Point3D, Point3D, double>? DistanceProjection =>
            (basePoint, pickedPoint) => ComputeOffset(pickedPoint);

        public override ILinearDimensionMode Apply(LinearDimensionCommand command)
        {
            command.SetOffset(InputDouble!.Value);

            return new FinishedMode();
        }

        public override IEnumerable<OpenCADObject> GetPreview(LinearDimensionCommand command)
        {
            var offset = ComputeOffset(command.GetTarget());
            command.SetOffset(offset);
            var dimDef = command.GetDimensionDefinition();
            var dimension = new LinearDimension(dimDef);
            foreach (var item in dimension.GenerateGeometry())
            {
                yield return (OpenCADObject)item;
            }
        }

        private double ComputeOffset(Point3D locationPoint)
        {
            // The normal is perpendicular to the dimension direction in the XY plane
            var normal = new Vector3D(-_direction.Y, _direction.X, 0);

            // Project the vector from the first point to the picked location onto the normal
            var delta = locationPoint - _firstPoint;
            return delta.X * normal.X + delta.Y * normal.Y;
        }
    }
}