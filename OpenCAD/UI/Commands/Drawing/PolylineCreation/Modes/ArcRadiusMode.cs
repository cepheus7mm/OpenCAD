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
    public class ArcRadiusMode : ArcModeBase
    {
        public ArcRadiusMode(Point3D start, Vector3D tangent) : base(start, tangent) 
        { 
            _center1 = null;
            _center2 = null;
            SetDouble(double.NaN);
        }

        public override bool CanPreview => InputDouble.HasValue && _center1.HasValue && _center2.HasValue;

        public override bool AllowArbitraryInput => !(InputDouble.HasValue && _center1.HasValue && _center2.HasValue);

        public override UserInputType UserInputType =>
            !InputDouble.HasValue ? UserInputType.Distance : UserInputType.Point;

        public override string Prompt =>
            !InputDouble.HasValue
                ? "Specify radius"
                : "Specify end point of arc";

        public override bool IsComplete =>
            InputDouble.HasValue && InputPoint.HasValue;

        public override void SetDouble(double value)
        {
            base.SetDouble(value);
            var vec = _tangent.Normalized.Rotate(Math.PI / 2, Vector3D.UnitZ) * value;
            _center1 = StartPoint + vec;
            _center2 = StartPoint - vec;
        }

        protected override double ComputeBulge(Point3D endPoint)
        {
            var side = GetCursorSideOfTangent(StartPoint, _tangent, endPoint);
            var center = side >= 0 ? _center1 : _center2;
            var vec = (endPoint - center).Value.Normalized * InputDouble;
            _endPoint = center + vec;
            var bulge = GeometricCalculator.GetBulgeFromCenterFull(StartPoint, _endPoint!.Value, center.Value, side > 0);

            return bulge;
        }
    }
}
