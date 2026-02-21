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
    public sealed class CenterDiameterMode : CircleCreationModeBase, ICircleCreationMode
    {
        private Point3D? _center;
        private Point3D? _diameterPoint;
        private double? _radius;

        public CenterDiameterMode(OpenCADDocument ctx)
            : base(ctx)
        {
        }

        public Point3D GetBasePoint() => _center ?? Point3D.NotAPoint;

        // --- PROMPTS ----------------------------------------------------------

        public override string Prompt =>
            _center == null
                ? "Specify center point"
                : "Specify diameter";


        public override string[] Keywords => _center == null ? new[] { "RAD", "2PT", "3PT" } : Array.Empty<string>();

        public override UserInputType UserInputType => _center == null ? UserInputType.Point : UserInputType.Distance;


        // --- INPUT HANDLING ---------------------------------------------------

        public override void SetPoint(Point3D p)
        {
            if (_center == null)
            {
                _center = p;
            }
            else
            {
                _diameterPoint = p;
            }
        }

        public override void SetKeyword(string keyword)
        {
            // CircleCommand will handle switching modes.
            // This mode does not interpret keywords internally.
        }

        public override void SetDistance(double distance)
        {
            _radius = distance / 2;
        }


        // --- STATE -------------------------------------------------------------

        public override bool IsComplete =>
            _center != null && (_diameterPoint != null || _radius != null);


        // --- PREVIEW -----------------------------------------------------------

        public override Circle? GetPreview(Point3D dragPoint)
        {
            if (_center == null)
                return null;

            //if (_diameterPoint == null)
            //    return null;

            double diameter = (dragPoint - _center.Value).Length;

            if (diameter <= 0)
                return null;

            return new Circle(_center.Value, diameter / 2.0, Document);
        }

        public override void UpdateDynamicInput(Point3D cursor)
        {
            if (_center == null)
                return;

            // Update preview diameter point dynamically
            _diameterPoint = cursor;
        }


        // --- FINAL RESULT ------------------------------------------------------

        public override Circle CreateCircle()
        {
            if (_center == null || (_diameterPoint == null && _radius == null))
                throw new InvalidOperationException("Circle creation not complete.");

            if (_diameterPoint.HasValue)
            {
                double diameter = (_diameterPoint.Value - _center.Value).Length;
                _radius = diameter / 2.0; 
            }
            else if (!_radius.HasValue)
            {
                throw new InvalidOperationException("Either diameter point or radius must be specified.");
            }

            return new Circle(_center.Value, _radius.Value, Document);
        }
    }
}
