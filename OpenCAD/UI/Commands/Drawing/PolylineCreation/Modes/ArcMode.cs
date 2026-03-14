using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.PolylineCreation.Modes
{
    public class ArcMode : PolylineCreationModeBase
    {
        private readonly Point3D? _startPoint;
        private Point3D? _secondPoint;

        public ArcMode(Point3D? startPoint)
        {
            _startPoint = startPoint;
        }

        public override string Prompt =>
            _secondPoint is null
                ? "Specify second point of arc"
                : "Specify end point of arc";

        public override string[] Keywords =>
            _secondPoint is null
                ? new[] { "Direction", "Radius", "Angle", "Length", "Second", "Continue" }
                : Array.Empty<string>();

        public override bool AllowLastPoint => true;

        public override bool IsComplete =>
            _startPoint.HasValue &&
            _secondPoint.HasValue &&
            InputPoint.HasValue;

        public override void SetPoint(Point3D point)
        {
            if (_secondPoint is null)
            {
                _secondPoint = point;
            }
            else
            {
                base.SetPoint(point);
            }
        }

        public override Point3D? GetBasePoint()
        {
            if (_secondPoint is null)
                return _startPoint;

            return _secondPoint;
        }

        public override IPolylineCreationMode Apply(PolylineCommand command)
        {
            var start = _startPoint!.Value;
            var second = _secondPoint!.Value;
            var end = InputPoint!.Value;

            // Compute bulge from 3 points
            var bulge = GeometricCalculator.GetBulgeFromThreePoints(start, second, end);
            command.AddVertex(end, bulge);

            // Return to NextPointMode
            return new NextPointMode(end);
        }

        public override IEnumerable<IDrawable> GetPreview(PolylineCommand command)
        {
            if (_startPoint is null)
                return Enumerable.Empty<IDrawable>();

            if (_secondPoint is null)
            {
                return Enumerable.Empty<IDrawable>();
            }

            double bulge = 0;

            if (InputPoint is null)
            {
                bulge = GeometricCalculator.GetBulgeFromThreePoints(_startPoint.Value, _secondPoint.Value, command.GetTarget());
                command.SetPreviewVertex(command.GetTarget(), bulge);
                return Enumerable.Empty<IDrawable>();
            }
            bulge = GeometricCalculator.GetBulgeFromThreePoints(_startPoint.Value, _secondPoint.Value, InputPoint.Value);
            command.SetPreviewVertex(InputPoint.Value, bulge);
            return Enumerable.Empty<IDrawable>();
        }
    }
}
