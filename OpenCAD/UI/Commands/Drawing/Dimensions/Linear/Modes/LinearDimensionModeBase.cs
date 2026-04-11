using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.Dimensions.Linear.Modes
{
    public abstract class LinearDimensionModeBase : ILinearDimensionMode
    {
        public abstract string Prompt { get; }

        public virtual string[] Keywords { get; }
            = Array.Empty<string>();

        public virtual bool AllowLastPoint => false;

        public virtual bool CanPreview => false;

        protected Point3D? InputPoint { get; private set; }

        protected double? InputDouble { get; private set; }

        public virtual UserInputType UserInputType => UserInputType.Point;

        public virtual object? GetDefaultValue() => null;

        public virtual bool AllowArbitraryInput => false;

        public virtual Func<Point3D, Point3D, double>? DistanceProjection => null;

        public virtual void SetPoint(Point3D point)
        {
            InputPoint = point;
        }

        public virtual void SetDouble(double value)
        {
            if (double.IsNaN(value) || double.IsPositiveInfinity(value) || double.IsNegativeInfinity(value))
            {
                InputDouble = null;
                return;
            }

            InputDouble = value;
        }

        public abstract bool IsComplete { get; }

        public abstract ILinearDimensionMode Apply(LinearDimensionCommand command);

        public virtual Point3D? GetBasePoint() => null;

        public virtual IEnumerable<OpenCADObject> GetPreview(LinearDimensionCommand command)
            => Enumerable.Empty<OpenCADObject>();
    }
}