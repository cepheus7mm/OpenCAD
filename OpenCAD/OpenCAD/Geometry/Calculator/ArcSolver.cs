using OpenCAD.Geometry.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Calculator
{
    public struct ArcSolution
    {
        // Basic geometry
        public Point3D Start;
        public Point3D End;

        // Circle
        public Point3D Center;
        public double Radius;

        // Angles (radians)
        public double StartAngle;
        public double EndAngle;
        public double IncludedAngle;    // signed, CCW positive

        // Bulge
        public double Bulge;

        // Linear measures
        public double ChordLength;
        public double Sagitta;          // positive magnitude
        public double OffsetToCenter;   // R - sagitta
        public double ArcLength;

        // Direction
        public bool IsCCW;

        // Tangents at endpoints (unit vectors)
        public Vector3D StartTangent;
        public Vector3D EndTangent;

        public bool IsSolved;
    }

    public static class ArcSolver
    {
        /// <summary>
        /// General arc solver from a start point and a set of optional constraints.
        /// It will:
        /// - accept an optional end point
        /// - accept optional center, radius, includedAngle, arcLength, direction, bulge
        /// - compute the missing values (including end) when possible
        /// - return a fully populated ArcSolution
        ///
        /// Supported combinations (examples):
        /// - start + end + bulge
        /// - start + end + center
        /// - start + end + radius
        /// - start + center + includedAngle
        /// - start + center + arcLength
        /// - start + radius + includedAngle
        /// - start + radius + arcLength
        /// - start + end + includedAngle
        /// (Others can be added as needed.)
        /// </summary>
        public static ArcSolution SolveArc(
            Point3D start,
            Point3D? end = null,
            Point3D? center = null,
            Point3D? second = null,
            double? radius = null,
            double? includedAngle = null, // radians
            double? arcLength = null,
            Vector3D? direction = null,   // tangent at start
            double? bulge = null)
        {
            ArcSolution sol = new ArcSolution();
            sol.IsSolved = false;
            sol.Start = start;

            // ---------------------------
            // 0. Handle trivial/straight
            // ---------------------------

            if (end.HasValue)
            {
                sol.End = end.Value;
                sol.ChordLength = GeometricCalculator.GetChordLength(start, sol.End);

                if (sol.ChordLength < 1e-12 && !center.HasValue && !radius.HasValue &&
                    !includedAngle.HasValue && !arcLength.HasValue && !bulge.HasValue)
                {
                    // Degenerate: start == end and no curvature info
                    return MakeStraightSolution(sol);
                }
            }

            // ---------------------------
            // 1. Case: bulge + end → classic PL arc
            // ---------------------------
            if (bulge.HasValue && end.HasValue && Math.Abs(bulge.Value) > 1e-12)
            {
                sol.End = end.Value;
                sol.ChordLength = GeometricCalculator.GetChordLength(start, sol.End);
                sol.Bulge = bulge.Value;

                FillFromStartEndBulge(ref sol);
                return sol;
            }

            // ---------------------------
            // 1a. Case: second point + end → solve center from three points
            // ---------------------------
            if (second.HasValue && end.HasValue)
            {
                if (TryComputeCenterFromThreePoints(start, second.Value, end.Value, out Point3D computedCenter))
                {
                    sol.Center = computedCenter;
                    return SolveArc(
                        start,
                        end: end,
                        center: sol.Center,
                        radius: null,
                        includedAngle: null,
                        arcLength: null,
                        direction: null,
                        bulge: null);
                }
                 else
                {
                    // Points are collinear; treat as straight segment
                    sol.End = end.Value;
                    sol.ChordLength = GeometricCalculator.GetChordLength(start, sol.End);
                    return MakeStraightSolution(sol);
                }
            }

            // ---------------------------
            // 2. Case: center + end
            // center + start + end fully define the circle and arc
            // ---------------------------
            if (center.HasValue && end.HasValue)
            {
                sol.End = end.Value;
                sol.ChordLength = GeometricCalculator.GetChordLength(start, sol.End);
                sol.Center = center.Value;
                sol.Radius = GeometricCalculator.GetChordLength(start, sol.Center);

                // basic angles
                GeometricCalculator.GetRawArcAnglesFromCenter(sol.Center, sol.Start, sol.End,
                                                 out sol.StartAngle, out sol.EndAngle);

                // raw included angle (minor arc by default)
                double theta = GeometricCalculator.NormalizeUnsigned(sol.EndAngle - sol.StartAngle);

                if (theta > Math.PI) theta -= 2.0 * Math.PI;
                if (theta < -Math.PI) theta += 2.0 * Math.PI;

                // if user specified includedAngle, we try to respect its sign/magnitude
                if (includedAngle.HasValue && Math.Abs(includedAngle.Value) > 1e-12)
                {
                    double target = includedAngle.Value;
                    // Use sign of target; magnitude may differ if they want major arc
                    if (Math.Sign(target) != Math.Sign(theta))
                    {
                        if (target > 0 && theta < 0) theta += 2.0 * Math.PI;
                        if (target < 0 && theta > 0) theta -= 2.0 * Math.PI;
                    }
                    sol.IncludedAngle = target;
                }
                else
                {
                    sol.IncludedAngle = theta;
                }

                FillDerived(ref sol);
                return sol;
            }

            // ---------------------------
            // 3. Case: center + includedAngle (no end)
            // ---------------------------
            if (center.HasValue && includedAngle.HasValue)
            {
                sol.Center = center.Value;
                sol.Radius = GeometricCalculator.GetChordLength(start, sol.Center);
                sol.StartAngle = Math.Atan2(start.Y - sol.Center.Y, start.X - sol.Center.X);
                sol.IncludedAngle = includedAngle.Value;
                sol.EndAngle = sol.StartAngle + sol.IncludedAngle;

                // Compute end from center + radius + endAngle
                double ex = sol.Center.X + sol.Radius * Math.Cos(sol.EndAngle);
                double ey = sol.Center.Y + sol.Radius * Math.Sin(sol.EndAngle);
                sol.End = new Point3D(ex, ey, start.Z);
                sol.ChordLength = GeometricCalculator.GetChordLength(sol.Start, sol.End);

                FillDerived(ref sol);
                return sol;
            }

            // ---------------------------
            // 4. Case: center + arcLength (no end, no angle)
            // ---------------------------
            if (center.HasValue && arcLength.HasValue)
            {
                sol.Center = center.Value;
                sol.Radius = GeometricCalculator.GetChordLength(start, sol.Center);
                sol.StartAngle = Math.Atan2(start.Y - sol.Center.Y, start.X - sol.Center.X);

                double L = arcLength.Value;
                double theta = L / sol.Radius; // sign of L gives direction

                sol.IncludedAngle = theta;
                sol.EndAngle = sol.StartAngle + sol.IncludedAngle;

                double ex = sol.Center.X + sol.Radius * Math.Cos(sol.EndAngle);
                double ey = sol.Center.Y + sol.Radius * Math.Sin(sol.EndAngle);
                sol.End = new Point3D(ex, ey, start.Z);
                sol.ChordLength = GeometricCalculator.GetChordLength(sol.Start, sol.End);

                FillDerived(ref sol);
                return sol;
            }

            // ---------------------------
            // 5. Case: radius + end (no center)
            // Solve one of the two possible centers, optionally using direction to choose side.
            // ---------------------------
            if (radius.HasValue && end.HasValue && Math.Abs(radius.Value) > 1e-12)
            {
                sol.End = end.Value;
                sol.ChordLength = GeometricCalculator.GetChordLength(start, sol.End);

                double R = Math.Abs(radius.Value);
                double c = sol.ChordLength;
                double halfChord = c / 2.0;

                if (c > 2.0 * R + 1e-9)
                    throw new InvalidOperationException("No circle with given radius passes through both points.");

                double h = Math.Sqrt(Math.Max(0.0, R * R - halfChord * halfChord));

                // chord midpoint
                double mx = (start.X + sol.End.X) * 0.5;
                double my = (start.Y + sol.End.Y) * 0.5;

                // unit perpendicular (left)
                double dx = sol.End.X - start.X;
                double dy = sol.End.Y - start.Y;
                double invLen = 1.0 / c;
                double perpX = -dy * invLen;
                double perpY = dx * invLen;

                // Decide side:
                // - if direction given, pick center giving tangent at start
                // - else, default CCW (left side)
                double side = 1.0; // default CCW
                if (direction.HasValue)
                {
                    Vector3D t = direction.Value.Normalized;

                    // Candidate center on left
                    double cxL = mx + perpX * h;
                    double cyL = my + perpY * h;
                    Vector3D radialL = new Vector3D(cxL - start.X, cyL - start.Y, 0).Normalized;
                    double dotL = Math.Abs(Vector3D.Dot(t, radialL));

                    // Candidate center on right
                    double cxR = mx - perpX * h;
                    double cyR = my - perpY * h;
                    Vector3D radialR = new Vector3D(cxR - start.X, cyR - start.Y, 0).Normalized;
                    double dotR = Math.Abs(Vector3D.Dot(t, radialR));

                    side = (dotL < dotR) ? 1.0 : -1.0;
                }

                double cx = mx + perpX * h * side;
                double cy = my + perpY * h * side;

                sol.Center = new Point3D(cx, cy, start.Z);
                sol.Radius = R;

                GeometricCalculator.GetRawArcAnglesFromCenter(sol.Center, sol.Start, sol.End,
                                                 out sol.StartAngle, out sol.EndAngle);
                double theta = GeometricCalculator.NormalizeUnsigned(sol.EndAngle - sol.StartAngle);

                // If includedAngle provided, we can adjust sign for major/minor
                if (includedAngle.HasValue && Math.Abs(includedAngle.Value) > 1e-12)
                {
                    double target = includedAngle.Value;
                    if (Math.Sign(target) != Math.Sign(theta))
                    {
                        if (target > 0 && theta < 0) theta += 2.0 * Math.PI;
                        if (target < 0 && theta > 0) theta -= 2.0 * Math.PI;
                    }
                    sol.IncludedAngle = target;
                }
                else
                {
                    // default: minor arc
                    if (theta > Math.PI) theta -= 2.0 * Math.PI;
                    if (theta < -Math.PI) theta += 2.0 * Math.PI;
                    sol.IncludedAngle = theta;
                }

                FillDerived(ref sol);
                return sol;
            }

            // ---------------------------
            // 6. Case: radius + includedAngle (no end, no center)
            // Pick a center on left/right of start→(start + 1,0) based on sign of angle.
            // This is a bit "free", but useful if you want an arc from start by angle & radius.
            // ---------------------------
            if (radius.HasValue && includedAngle.HasValue && !end.HasValue)
            {
                double R = Math.Abs(radius.Value);
                double theta = includedAngle.Value;
                sol.Radius = R;
                sol.Start = start;
                sol.StartAngle = 0.0; // interpret "direction" as +X by default, or use 'direction' if given

                if (direction.HasValue)
                {
                    Vector3D t = direction.Value.Normalized;
                    var antiCurveture = theta >= 0.0 ? -1.0 : 1.0; // minus for CCW, plus for CW
                    sol.StartAngle = Math.Atan2(t.Y, t.X) + (Math.PI / 2.0 * antiCurveture);
                }

                sol.IncludedAngle = theta;
                sol.Center = new Point3D(
                    start.X - R * Math.Cos(sol.StartAngle),
                    start.Y - R * Math.Sin(sol.StartAngle),
                    start.Z); // rough: center opposite to tangent direction

                sol.EndAngle = sol.StartAngle + sol.IncludedAngle;
                double ex = sol.Center.X + R * Math.Cos(sol.EndAngle);
                double ey = sol.Center.Y + R * Math.Sin(sol.EndAngle);
                sol.End = new Point3D(ex, ey, start.Z);
                sol.ChordLength = GeometricCalculator.GetChordLength(sol.Start, sol.End);

                FillDerived(ref sol);
                return sol;
            }

            // ---------------------------
            // 7. Case: includedAngle + end (no center, no radius)
            // Derive radius from chord + angle.
            // ---------------------------
            if (includedAngle.HasValue && end.HasValue)
            {
                sol.End = end.Value;
                sol.ChordLength = GeometricCalculator.GetChordLength(start, sol.End!);

                double theta = includedAngle.Value;
                double c = sol.ChordLength;
                double half = theta / 2.0;
                double sinHalf = Math.Sin(half);
                if (Math.Abs(sinHalf) < 1e-12)
                    throw new InvalidOperationException("Angle too small to construct arc.");

                double R = c / (2.0 * Math.Abs(sinHalf));
                sol.Radius = R;

                // Sagitta magnitude
                double sMag = R * (1.0 - Math.Cos(half));
                sol.Sagitta = Math.Abs(sMag);
                sol.OffsetToCenter = R - sol.Sagitta;

                // chord midpoint
                double mx = (start.X + sol.End.X) * 0.5;
                double my = (start.Y + sol.End.Y) * 0.5;

                // unit perpendicular
                double dx = sol.End.X - start.X;
                double dy = sol.End.Y - start.Y;
                double invLen = 1.0 / c;
                double perpX = -dy * invLen;
                double perpY = dx * invLen;

                double side = (theta >= 0.0) ? 1.0 : -1.0;
                double cx = mx + perpX * sol.OffsetToCenter * side;
                double cy = my + perpY * sol.OffsetToCenter * side;

                sol.Center = new Point3D(cx, cy, start.Z);

                GeometricCalculator.GetRawArcAnglesFromCenter(sol.Center, sol.Start, sol.End,
                                                 out sol.StartAngle, out sol.EndAngle);
                sol.IncludedAngle = theta;

                FillDerived(ref sol, skipAngleToBulge: true);
                return sol;
            }

            // ---------------------------
            // 7a. Case: arcLength + end (no center, no radius, no angle)
            // Solve radius from chord + arc length.
            // ---------------------------
            if (arcLength.HasValue && end.HasValue)
            {
                sol.End = end.Value;
                sol.ChordLength = GeometricCalculator.GetChordLength(start, sol.End);

                double L = arcLength.Value;
                double c = sol.ChordLength;

                if (c < 1e-12)
                    throw new InvalidOperationException("Start and end points are identical; arc length is meaningless.");

                double absL = Math.Abs(L);

                // Solve for R using bisection:
                // f(R) = 2R * sin(L/(2R)) - c = 0
                double Rmin = c / 2.0;       // minimum possible radius (semi-circle)
                double Rmax = 1e12;          // large upper bound

                Func<double, double> f = R =>
                    2.0 * R * Math.Sin(absL / (2.0 * R)) - c;

                // Bisection
                for (int iter = 0; iter < 100; iter++)
                {
                    double mid = 0.5 * (Rmin + Rmax);
                    double fmid = f(mid);

                    if (Math.Abs(fmid) < 1e-12)
                    {
                        sol.Radius = mid;
                        break;
                    }

                    // f(Rmin) and f(mid) have opposite signs?
                    if (f(Rmin) * fmid < 0)
                        Rmax = mid;
                    else
                        Rmin = mid;

                    sol.Radius = 0.5 * (Rmin + Rmax);
                }

                double R = sol.Radius;

                // Compute included angle
                double theta = L / R;   // sign of L determines CW/CCW
                sol.IncludedAngle = theta;

                // Sagitta
                double half = theta / 2.0;
                sol.Sagitta = Math.Abs(R * (1.0 - Math.Cos(half)));
                sol.OffsetToCenter = R - sol.Sagitta;

                // chord midpoint
                double mx = (start.X + sol.End.X) * 0.5;
                double my = (start.Y + sol.End.Y) * 0.5;

                // unit perpendicular
                double dx = sol.End.X - start.X;
                double dy = sol.End.Y - start.Y;
                double invLen = 1.0 / c;
                double perpX = -dy * invLen;
                double perpY = dx * invLen;

                // side determined by sign of arc length
                double side = (L >= 0.0) ? 1.0 : -1.0;

                double cx = mx + perpX * sol.OffsetToCenter * side;
                double cy = my + perpY * sol.OffsetToCenter * side;

                sol.Center = new Point3D(cx, cy, start.Z);

                GeometricCalculator.GetRawArcAnglesFromCenter(
                    sol.Center, sol.Start, sol.End,
                    out sol.StartAngle, out sol.EndAngle);

                FillDerived(ref sol, skipAngleToBulge: true);
                return sol;
            }

            // ---------------------------
            // 8. Case: radius + arcLength (no end)
            // Derive angle = L / R, then use "radius + includedAngle" path.
            // ---------------------------
            if (radius.HasValue && arcLength.HasValue && !end.HasValue)
            {
                double R = Math.Abs(radius.Value);
                double L = arcLength.Value;
                double theta = L / R;

                return SolveArc(
                    start,
                    end: null,
                    center: null,
                    radius: R,
                    includedAngle: theta,
                    arcLength: L,
                    direction: direction,
                    bulge: null);
            }

            // ---------------------------
            // 8a. Case: arcLength + includedAngle (no end, no radius)
            // Derive radius = L / θ, then use "radius + includedAngle" path.
            // ---------------------------
            if (arcLength.HasValue && includedAngle.HasValue && !end.HasValue && !radius.HasValue)
            {
                double L = arcLength.Value;
                double theta = includedAngle.Value;

                if (Math.Abs(theta) < 1e-12)
                    throw new InvalidOperationException("Included angle too small to derive radius from arc length.");

                double R = Math.Abs(L / theta);

                return SolveArc(
                    start,
                    end: null,
                    center: null,
                    radius: R,
                    includedAngle: theta,
                    arcLength: L,
                    direction: direction,
                    bulge: null);
            }

            // ---------------------------
            // 8b. Case: start + end + direction
            // ---------------------------
            if (end.HasValue && direction.HasValue)
            {
                return SolveFromStartEndDirection(start, end.Value, direction.Value);
            }

            // ---------------------------
            // 9. Fallback: start + end only → straight segment (bulge 0).
            // ---------------------------
            if  (end.HasValue)
            {
                sol.End = end.Value;
                sol.ChordLength = GeometricCalculator.GetChordLength(start, sol.End);
                return MakeStraightSolution(sol);
            }

            throw new InvalidOperationException("Insufficient or unsupported combination of constraints to solve arc.");
        }

        // ---------------------------------------------------
        // Helpers to fill derived fields from core quantities
        // ---------------------------------------------------

        private static ArcSolution MakeStraightSolution(ArcSolution s)
        {
            s.Center = new Point3D(double.NaN, double.NaN, double.NaN);
            s.Radius = double.PositiveInfinity;
            s.StartAngle = s.EndAngle = s.IncludedAngle = 0.0;
            s.Bulge = 0.0;
            s.Sagitta = 0.0;
            s.OffsetToCenter = 0.0;
            s.ArcLength = s.ChordLength;
            s.IsCCW = false;

            if (s.ChordLength > 1e-12)
            {
                double tx = (s.End.X - s.Start.X) / s.ChordLength;
                double ty = (s.End.Y - s.Start.Y) / s.ChordLength;
                s.StartTangent = new Vector3D(tx, ty, 0);
                s.EndTangent = s.StartTangent;
            }
            else
            {
                s.StartTangent = new Vector3D(0, 0, 0);
                s.EndTangent = new Vector3D(0, 0, 0);
            }

            return s;
        }

        /// <summary>
        /// Fill all derived quantities assuming Start, End, Center, Radius, IncludedAngle are known or inferable.
        /// </summary>
        private static void FillDerived(ref ArcSolution s, bool skipAngleToBulge = false)
        {
            // If angles are not yet set, compute them
            if (double.IsNaN(s.StartAngle) || double.IsNaN(s.EndAngle))
            {
                GeometricCalculator.GetRawArcAnglesFromCenter(s.Center, s.Start, s.End,
                                                 out s.StartAngle, out s.EndAngle);
            }

            if (Math.Abs(s.IncludedAngle) < 1e-12)
            {
                // derive included angle from end-start angles if missing
                s.IncludedAngle = GeometricCalculator.NormalizeUnsigned(s.EndAngle - s.StartAngle);
            }

            if (!skipAngleToBulge)
            {
                s.Bulge = GeometricCalculator.GetBulgeFromAngle(s.IncludedAngle);
            }
            else if (Math.Abs(s.Bulge) < 1e-12)
            {
                s.Bulge = GeometricCalculator.GetBulgeFromAngle(s.IncludedAngle);
            }

            s.ChordLength = GeometricCalculator.GetChordLength(s.Start, s.End);
            s.Sagitta = GeometricCalculator.GetSagittaFromBulge(s.Start, s.End, s.Bulge);
            s.OffsetToCenter = s.Radius - s.Sagitta;
            s.ArcLength = Math.Abs(s.Radius * s.IncludedAngle);
            s.IsCCW = s.Bulge > 0.0;

            s.StartTangent = new Vector3D(
                -Math.Sin(s.StartAngle),
                 Math.Cos(s.StartAngle), 0);
            s.EndTangent = new Vector3D(
                -Math.Sin(s.EndAngle),
                 Math.Cos(s.EndAngle), 0);

            s.IsSolved = true;
        }

        private static ArcSolution SolveFromStartEndDirection(Point3D S, Point3D E, Vector3D T)
        {
            // Normalize tangent
            T = T.Normalized;

            // Perpendicular to tangent (CCW normal)
            Vector3D N = new Vector3D(-T.Y, T.X, 0);

            // Solve for d such that |S + N*d - S| = |S + N*d - E|
            // => d^2 = |N*d - (E - S)|^2

            Vector3D D = E - S;

            // Expand:
            // d^2 = (d*N - D)·(d*N - D)
            // d^2 = d^2 + |D|^2 - 2d(N·D)
            // 0 = |D|^2 - 2d(N·D)

            double ND = Vector3D.Dot(N, D);

            if (Math.Abs(ND) < 1e-12)
                throw new InvalidOperationException("Direction points directly at end point; no tangent arc exists.");

            double d = (D.LengthSquared) / (2 * ND);

            Point3D C = S + N * d;

            // Now feed into your existing center-based solver
            return SolveArc(
                S,
                end: E,
                center: C,
                radius: null,
                includedAngle: null,
                arcLength: null,
                direction: null,
                bulge: null);
        }

        /// <summary>
        /// Convenience overload: classic PL segment with known end & bulge only.
        /// </summary>
        public static ArcSolution SolveArcFromBulge(Point3D start, Point3D end, double bulge)
        {
            return SolveArc(start, end: end, bulge: bulge);
        }

        private static void FillFromStartEndBulge(ref ArcSolution s)
        {
            // Radius
            s.Radius = GeometricCalculator.GetRadiusFromBulge(s.Start, s.End, s.Bulge);

            // Center
            s.Center = GeometricCalculator.GetCenterFromBulge(s.Start, s.End, s.Bulge);

            // Angles
            GeometricCalculator.GetRawArcAnglesFromCenter(
                s.Center,
                s.Start,
                s.End,
                out s.StartAngle,
                out s.EndAngle);

            // Included angle (signed)
            s.IncludedAngle = GeometricCalculator.GetIncludedAngleFromBulge(
                s.Center,
                s.Start,
                s.End,
                s.Bulge);

            // Linear measures
            s.ChordLength = GeometricCalculator.GetChordLength(s.Start, s.End);
            s.Sagitta = GeometricCalculator.GetSagittaFromBulge(s.Start, s.End, s.Bulge);
            s.OffsetToCenter = s.Radius - s.Sagitta;
            s.ArcLength = Math.Abs(s.Radius * s.IncludedAngle);

            // Direction
            s.IsCCW = s.Bulge > 0.0;

            // Tangents
            s.StartTangent = new Vector3D(
                -Math.Sin(s.StartAngle),
                 Math.Cos(s.StartAngle), 0);

            s.EndTangent = new Vector3D(
                -Math.Sin(s.EndAngle),
                 Math.Cos(s.EndAngle), 0);
        }

        public static bool TryComputeCenterFromThreePoints(
            Point3D start,
            Point3D second,
            Point3D end,
            out Point3D center)
        {
            center = Point3D.NotAPoint;

            double x1 = start.X, y1 = start.Y;
            double x2 = second.X, y2 = second.Y;
            double x3 = end.X, y3 = end.Y;

            // Determinant (twice the signed area of triangle)
            double d = 2 * (x1 * (y2 - y3) +
                            x2 * (y3 - y1) +
                            x3 * (y1 - y2));

            // Collinear or nearly collinear
            if (Math.Abs(d) < 1e-12)
                return false;

            double x1sq = x1 * x1 + y1 * y1;
            double x2sq = x2 * x2 + y2 * y2;
            double x3sq = x3 * x3 + y3 * y3;

            double ux = (x1sq * (y2 - y3) +
                         x2sq * (y3 - y1) +
                         x3sq * (y1 - y2)) / d;

            double uy = (x1sq * (x3 - x2) +
                         x2sq * (x1 - x3) +
                         x3sq * (x2 - x1)) / d;

            center = new Point3D(ux, uy, start.Z);
            return true;
        }
    }
}
