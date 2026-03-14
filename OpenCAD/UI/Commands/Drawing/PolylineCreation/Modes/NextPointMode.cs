using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.PolylineCreation.Modes
{
    public class NextPointMode : PolylineCreationModeBase
    {
        public override string Prompt =>
            "Specify next point";

        public override string[] Keywords { get; }
            = new[] { "ARC", "CLOSE", "WIDTH", "UNDO" };

        // After the first point, Enter should reuse the last point
        public override bool AllowLastPoint => true;

        private Point3D? _basePoint;

        public NextPointMode()
        {
        }

        public NextPointMode(Point3D basePoint)
        {
            _basePoint = basePoint;
        }

        public override Point3D? GetBasePoint() => _basePoint;

        public override bool IsComplete => InputPoint.HasValue;

        public override void SetPoint(Point3D point)
        {
            base.SetPoint(point);
        }

        public override IPolylineCreationMode Apply(PolylineCommand command)
        {
            // Add the new vertex
            command.AddVertex(InputPoint!.Value);

            // Update base point for next iteration
            _basePoint = InputPoint;

            // Stay in NextPointMode for continuous polyline creation
            return new NextPointMode(_basePoint.Value);
        }

        public override IEnumerable<IDrawable> GetPreview(PolylineCommand command)
        {
            if (!command.GetTarget().IsValid)
                return Enumerable.Empty<IDrawable>();

            //var last = command.LastPoint;
            //if (last is null)
            //    return Enumerable.Empty<IDrawable>();

            command.SetPreviewVertex(command.GetTarget());

            return Enumerable.Empty<IDrawable>();
        }
    }
}
