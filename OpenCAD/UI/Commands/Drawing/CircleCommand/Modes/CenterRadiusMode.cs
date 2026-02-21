using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.CircleCommand.Modes
{
    public class CenterRadiusMode : CircleCreationModeBase, ICircleCreationMode
    {
        private Point3D? _center;
        private double? _radius;

        public CenterRadiusMode(OpenCADDocument ctx) : base(ctx) { }

        public override string Prompt =>
            _center == null ? "Specify center point" : "Specify radius";

        public override string[] Keywords => _center == null ? new[] { "DIA", "2PT", "3PT" } : Array.Empty<string>();

        public override UserInputType UserInputType => _center == null ? UserInputType.Point : UserInputType.Distance;


        public override void SetPoint(Point3D p)
        {
            if (_center == null)
                _center = p;
            else
                _radius = (_center.Value - p).Length;
        }

        public override void SetKeyword(string keyword)
        {
            // CircleCommand will handle switching modes.
            // This mode does not interpret keywords internally.
        }

        public override void SetDistance(double distance)
        {
            _radius = distance;
        }

        public override bool IsComplete => _center != null && _radius != null;

        public override Circle? GetPreview(Point3D dragPoint)
        {
            if (_center != null)
                return new Circle(_center.Value, _center.Value.DistanceTo(dragPoint), Document);

            return null;
        }

        public override Circle CreateCircle()
            => new Circle(_center!.Value, _radius!.Value, Document);

        public Point3D GetBasePoint() => _center ?? Point3D.NotAPoint;
    }
}
