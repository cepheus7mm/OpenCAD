using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using Poly2Tri;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.ArcCommand.ArcModes
{
    public abstract class BaseArcMode : IArcCreationMode
    {
        protected readonly OpenCADDocument Document;

        protected Point3D Start;
        protected Point3D Second;
        protected Point3D Third;
        protected double Number = double.NaN;

        public abstract string Prompt { get; }
        public virtual string[] Keywords => Array.Empty<string>();
        public abstract bool IsComplete { get; }

        public virtual Point3D GetBasePoint() => Start;

        public virtual void SetPoint(Point3D p)
        {
            if (Start.IsNotAPoint)
                Start = p;
            else if (Second.IsNotAPoint)
                Second = p;
            else
                Third = p;
        }

        public virtual void SetDouble(double value)
        {
            Number = value;
        }

        public abstract UserInputType UserInputType { get; }

        public abstract Arc GetPreview(Point3D cursor);
        public abstract Arc CreateArc();

        protected BaseArcMode(OpenCADDocument doc)
        {
            Document = doc;
            Start = Point3D.NotAPoint;
            Second = Point3D.NotAPoint;
            Third = Point3D.NotAPoint;
            Number = double.NaN;
        }

        protected Arc FromThreePoints(Point3D pt1, Point3D pt2, Point3D pt3)
        {
            if (pt1.IsNotAPoint || pt2.IsNotAPoint || pt3.IsNotAPoint)
                return Arc.Empty;

            var solution = ArcSolver.SolveArc(pt1, second: pt2, end: pt3);
            return new Arc(solution.Center, solution.Radius, solution.StartAngle, solution.EndAngle, Document);
        }

        protected Arc FromThreePoints(Point3D pt3)
        {
            if (Start.IsNotAPoint || Second.IsNotAPoint)
                return Arc.Empty;

            var solution = ArcSolver.SolveArc(Start, pt3, Second);
            return new Arc(solution.Center, solution.Radius, solution.StartAngle, solution.EndAngle, Document);
        }

        protected Arc FromStartCenterIncludedAngle(Point3D? pt3)
        {
            if (Start.IsNotAPoint || Second.IsNotAPoint)
                return Arc.Empty;

            try
            {
                double angle = pt3.HasValue ? Second.AngleTo(pt3.Value) : Number;
                var solution = ArcSolver.SolveArc(Start, center: Second, includedAngle: angle);

                return new Arc(solution.Center, solution.Radius, solution.StartAngle, solution.EndAngle, Document);
            }
            catch (Exception)
            {
                return Arc.Empty;
            }
        }

        protected Arc FromStartEndDirection(Point3D pt3)
        {
            return Arc.Empty;
        }

        protected Arc FromStartEndDirection()
        {
            return Arc.Empty;
        }

        protected Arc FromStartEndLength(Point3D? pt3)
        {
            if (Start.IsNotAPoint || Second.IsNotAPoint)
                return Arc.Empty;

            double len = pt3.HasValue ? Second.DistanceTo(pt3.Value) : Number;
            var solution = ArcSolver.SolveArc(Start, Second, arcLength: len);

            return new Arc(solution.Center, solution.Radius, solution.StartAngle, solution.EndAngle, Document);
        }

        protected Arc FromStartEndRadius(Point3D? pt3)
        {
            if (Start.IsNotAPoint || Second.IsNotAPoint)
                return Arc.Empty;

            try
            {
                var radius = pt3.HasValue ? Second.DistanceTo(pt3.Value) : Number;
                var solution = ArcSolver.SolveArc(Start, Second, radius: radius);

                return new Arc(solution.Center, radius, solution.StartAngle, solution.EndAngle, Document);
            }
            catch (Exception)
            {
                return Arc.Empty;
            }

        }

    }
}
