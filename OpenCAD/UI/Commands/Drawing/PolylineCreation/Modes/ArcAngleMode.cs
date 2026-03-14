using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Drawing.PolylineCreation.Modes
{
    public class ArcAngleMode : ArcModeBase
    {
        private Point3D? _center;   // computed center
        private double? _radius;    // computed radius

        public ArcAngleMode(Point3D start, Vector3D tangent)
            : base(start, tangent)
        {
            SetDouble(double.NaN);
        }

        public override bool CanPreview => InputDouble.HasValue;

        public override bool AllowArbitraryInput => !InputDouble.HasValue;

        public override UserInputType UserInputType => !InputDouble.HasValue ? UserInputType.Angle : UserInputType.Point;

        public override string Prompt => !InputDouble.HasValue ? "Specify included angle" : "Specify end point of arc";

        public override bool IsComplete => InputDouble.HasValue && InputPoint.HasValue;

        public override void SetDouble(double value)
        {
            base.SetDouble(value);
        }

        protected override double ComputeBulge(Point3D rawEndPoint)
        {
            if (!InputDouble.HasValue)
                return 0;

            double theta = InputDouble.Value;

            // 1. Cursor direction
            Vector3D d = (rawEndPoint - StartPoint).Normalized;

            // 2. Determine CW/CCW from cursor side
            double side = GetCursorSideOfTangent(StartPoint, _tangent, rawEndPoint);
            bool ccw = side > 0;

            // 3. Center direction (perpendicular to tangent)
            Vector3D normal = _tangent.Normalized.Rotate(
                ccw ? Math.PI / 2 : -Math.PI / 2,
                Vector3D.UnitZ);

            // 4. Compute chord length (cursor distance)
            double c = (rawEndPoint - StartPoint).Length;

            // Avoid division by zero
            if (Math.Abs(Math.Sin(theta)) < 1e-12)
                return 0;

            // 5. Compute radius
            double R = c / (1.0 * Math.Sin(theta));
            _radius = Math.Abs(R);

            // 6. Compute center
            _center = StartPoint + normal * R;

            // 7. Constrain end point to cursor ray
            d = _tangent.Rotate(theta / 2.0 * (ccw ? 1 : -1), Vector3D.UnitZ);
            _endPoint = StartPoint + d * c;

            // 8. Major arc correction
            // Compute signed angle from center (skip to step 9)

            // 9. Compute bulge
            var bulge = GeometricCalculator.GetBulgeFromCenterFull(StartPoint, _endPoint!.Value, _center.Value, ccw);

            return bulge;
        }
    }
}
