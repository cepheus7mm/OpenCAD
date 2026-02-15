using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Editing.Fillet
{
    public sealed class FilletSolution
    {
        public Arc FilletArc { get; init; }
        public ICurve TrimmedA { get; init; }
        public ICurve TrimmedB { get; init; }
    }

    public sealed class FilletSolver
    {
        public OpenCADDocument? Document { get; set; }

        public bool TrySolve(
            ICurve curveA,
            ICurve curveB,
            Point3D pickA,
            Point3D pickB,
            double radius,
            out FilletSolution? solution)
        {
            solution = null;

            if (radius <= 0)
                return false;

            // 1) Build offset curves at +radius on both curves
            //    (sign convention: +radius = "left" of curve direction)
            var offsetsA = curveA.GetOffsetCurves(radius);
            var offsetsB = curveB.GetOffsetCurves(radius);

            if (offsetsA.Length == 0 || offsetsB.Length == 0)
                return false;

            // 2) Collect all intersection candidates between offset curves
            var centers = new List<Point3D>();

            foreach (var oa in offsetsA)
                foreach (var ob in offsetsB)
                {
                    var pts = GeometricCalculator.Intersection(oa, ob);
                    if (pts != null && pts.Count() > 0)
                        centers.AddRange(pts);
                }

            if (centers.Count < 1)
                return false;

            // 3) Choose the "best" center based on pick points
            //    Heuristic: center closest to the corner implied by picks
            var cornerGuess = GeometricCalculator.TryIntersectInfinite(
                curveA, curveB, pickA, pickB, out var corner)
                ? corner
                : pickA; // fallback

            Point3D center = centers
                .OrderBy(c => c.DistanceTo(cornerGuess))
                .First();

            // 4) Project center back onto original curves to get true tangent points
            double tA = curveA.GetClosestParameter(center, extend: true);
            double tB = curveB.GetClosestParameter(center, extend: true);

            Point3D pA = curveA.GetPointAtParameter(tA);
            Point3D pB = curveB.GetPointAtParameter(tB);

            // 5) Build fillet arc from center + tangent points
            double startAngle = center.AngleTo(pA);
            double endAngle = center.AngleTo(pB);
            double sweep = GeometricCalculator.NormalizeUnsigned(endAngle - startAngle);

            var filletArc = new Arc(center, radius, startAngle, sweep, Document, true);

            // 6) Trim original curves at tangent parameters
            bool keepStartA = ShouldKeepStart(curveA, pickA, tA);
            bool keepStartB = ShouldKeepStart(curveB, pickB, tB);

            ICurve trimmedA = TrimCurveAt(curveA, tA, keepStartA);
            ICurve trimmedB = TrimCurveAt(curveB, tB, keepStartB);

            // 7) Package solution
            solution = new FilletSolution
            {
                FilletArc = filletArc,
                TrimmedA = trimmedA,
                TrimmedB = trimmedB
            };

            return true;
        }

        // ------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------

        private static ICurve TrimCurveAt(ICurve curve, double t, bool keepStart)
        {
            double t0 = curve.DomainStart;
            double t1 = curve.DomainEnd;

            return keepStart
                ? curve.Trim(t0, t)
                : curve.Trim(t, t1);
        }

        private static bool ShouldKeepStart(ICurve curve, Point3D pick, double tTangent)
        {
            // Compare pick parameter vs tangent parameter
            double tPick = curve.GetClosestParameter(pick, extend: true);
            return tPick < tTangent;
        }
    }
}
