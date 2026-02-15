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

        public CenterDiameterMode(OpenCADDocument ctx)
            : base(ctx)
        {
        }

        public Point3D GetBasePoint() => _center ?? Point3D.NotAPoint;

        // --- PROMPTS ----------------------------------------------------------

        public override string Prompt =>
            _center == null
                ? "Specify center point"
                : "Specify diameter point";

        public override string[] Keywords => new[] { "RAD", "2PT", "3PT" };


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


        // --- STATE -------------------------------------------------------------

        public override bool IsComplete =>
            _center != null && _diameterPoint != null;


        // --- PREVIEW -----------------------------------------------------------

        public override Circle? GetPreview(Point3D dragPoint)
        {
            if (_center == null)
                return null;

            //if (_diameterPoint == null)
            //    return null;

            double diameter = (dragPoint - _center.Value).Length;
            double radius = diameter / 2.0;

            if (radius <= 0)
                return null;

            return new Circle(_center.Value, radius, Document);
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
            if (_center == null || _diameterPoint == null)
                throw new InvalidOperationException("Circle creation not complete.");

            double diameter = (_diameterPoint.Value - _center.Value).Length;
            double radius = diameter / 2.0;

            return new Circle(_center.Value, radius, Document);
        }
    }
}
