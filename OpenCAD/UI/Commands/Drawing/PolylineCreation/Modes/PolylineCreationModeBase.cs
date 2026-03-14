using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using UI.Commands.Drawing.PolylineCreation;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.Drawing.PolylineCreation.Modes
{
    public abstract class PolylineCreationModeBase : IPolylineCreationMode
    {
        public abstract string Prompt { get; }

        public virtual string[] Keywords { get; }
            = Array.Empty<string>();

        public virtual bool AllowLastPoint => false;

        protected Point3D? InputPoint { get; private set; }

        protected double? InputDouble { get; private set; }

        public virtual UserInputType UserInputType => UserInputType.Point;

        public virtual object? GetDefaultValue() => null;

        public virtual bool AllowArbitraryInput => false;

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

        public abstract IPolylineCreationMode Apply(PolylineCommand command);

        public virtual Point3D? GetBasePoint() => null;

        public virtual IEnumerable<IDrawable> GetPreview(PolylineCommand command)
            => Enumerable.Empty<IDrawable>();
    }
}