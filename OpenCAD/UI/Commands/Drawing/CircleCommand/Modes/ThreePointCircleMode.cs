using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.CircleCommand.Modes
{
    public sealed class ThreePointCircleMode : CircleCreationModeBase, ICircleCreationMode
    {
        private Point3D? _p1;
        private Point3D? _p2;
        private Point3D? _p3;

        public ThreePointCircleMode(OpenCADDocument ctx)
            : base(ctx)
        {
        }

        public Point3D GetBasePoint() => Point3D.NotAPoint;


        // --- PROMPTS ----------------------------------------------------------

        public override string Prompt =>
            _p1 == null ? "Specify first point of circle" :
            _p2 == null ? "Specify second point of circle" :
                          "Specify third point of circle";

        public override string[] Keywords => _p1 == null ? new[] { "RAD", "DIA", "2PT" } : Array.Empty<string>();

        public override UserInputType UserInputType => UserInputType.Point;


        // --- INPUT HANDLING ---------------------------------------------------

        public override void SetPoint(Point3D p)
        {
            if (_p1 == null)
                _p1 = p;
            else if (_p2 == null)
                _p2 = p;
            else
                _p3 = p;
        }

        public override void SetDistance(double distance)
        {
            throw new NotImplementedException();
        }

        // --- STATE -------------------------------------------------------------

        public override bool IsComplete =>
            _p1 != null && _p2 != null && _p3 != null;


        // --- PREVIEW -----------------------------------------------------------

        public override void UpdateDynamicInput(Point3D cursor)
        {
            if (_p1 != null && _p2 != null)
            {
                // Update third point dynamically
                _p3 = cursor;
            }
        }

        public override Circle? GetPreview(Point3D dragPoint)
        {
            if (_p1 == null || _p2 == null)
                return null;

            if (!GeometricCalculator.TryGetCircleThroughThreePoints(
                    _p1.Value, _p2.Value, dragPoint,
                    out var center, out var radius))
            {
                return null; // collinear or invalid
            }

            return new Circle(center, radius);
        }


        // --- FINAL RESULT ------------------------------------------------------

        public override Circle CreateCircle()
        {
            if (_p1 == null || _p2 == null || _p3 == null)
                throw new InvalidOperationException("Circle creation not complete.");

            if (!GeometricCalculator.TryGetCircleThroughThreePoints(
                    _p1.Value, _p2.Value, _p3.Value,
                    out var center, out var radius))
            {
                throw new InvalidOperationException("The three points are collinear.");
            }

            return new Circle(center, radius);
        }
    }
}
