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
    public sealed class TwoPointCircleMode : CircleCreationModeBase, ICircleCreationMode
    {
        private Point3D? _p1;
        private Point3D? _p2;

        public TwoPointCircleMode(OpenCADDocument ctx)
            : base(ctx)
        {
        }

        public Point3D GetBasePoint() => _p1 ?? Point3D.NotAPoint;

        // --- PROMPTS ----------------------------------------------------------

        public override string Prompt =>
            _p1 == null
                ? "Specify first point of diameter"
                : "Specify second point of diameter";

        public override string[] Keywords => new[] { "RAD", "DIA", "3PT" };


        // --- INPUT HANDLING ---------------------------------------------------

        public override void SetPoint(Point3D p)
        {
            if (_p1 == null)
                _p1 = p;
            else
                _p2 = p;
        }


        // --- STATE -------------------------------------------------------------

        public override bool IsComplete =>
            _p1 != null && _p2 != null;


        // --- PREVIEW -----------------------------------------------------------

        public override void UpdateDynamicInput(Point3D cursor)
        {
            if (_p1 != null)
            {
                // Update second point dynamically
                _p2 = cursor;
            }
        }

        public override Circle? GetPreview(Point3D dragPoint)
        {
            if (_p1 == null)
                return null;

            double diameter = (dragPoint - _p1.Value).Length;
            if (diameter <= 0)
                return null;

            var center = Point3D.MidPoint(_p1.Value, dragPoint);
            double radius = diameter / 2.0;

            return new Circle(center, radius);
        }


        // --- FINAL RESULT ------------------------------------------------------

        public override Circle CreateCircle()
        {
            if (_p1 == null || _p2 == null)
                throw new InvalidOperationException("Circle creation not complete.");

            double diameter = (_p2.Value - _p1.Value).Length;
            if (diameter <= 0)
                throw new InvalidOperationException("Diameter points must not be identical.");

            var center = Point3D.MidPoint(_p1.Value, _p2.Value);
            double radius = diameter / 2.0;

            return new Circle(center, radius);
        }
    }
}
