using OpenCAD.Geometry.Helpers;
using System;
using System.Net;

namespace OpenCAD.Geometry.Calculator
{
    internal static class LineUtils
    {
        public static double GetParameterAtPoint(Point3D point, Point3D a, Point3D b)
        {
            var ab = b - a;
            double abLenSq = ab.LengthSquared;

            if (abLenSq < 1e-12)
                return 0.0; // Degenerate segment → treat as t=0

            var ap = point - a;
            double dot = Vector3D.Dot(ap, ab);

            return Math.Clamp(dot / abLenSq, 0.0, 1.0);
        }

        public static Point3D GetPointAtParameter(double t, Point3D a, Point3D b)
        {
            return new Point3D(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t
            );
        }

        public static Point3D GetClosestPoint(Point3D point, Point3D start, Point3D end, bool extend = false)
        {
            var seg = end - start;
            double segLenSq = seg.LengthSquared;

            // Degenerate segment
            if (segLenSq < 1e-20)
                return start;

            var v = point - start;

            // Projection parameter
            double t = v.Dot(seg) / segLenSq;

            // Clamp to segment
            if (!extend)
            {
                t = Math.Clamp(t, 0, 1);
            }

            // Compute closest point
            var closest = start + seg * t;

            return closest;
        }

        /// <summary>
        /// Euclidean chord length in XY between two points.
        /// Kept here because chord length is a linear primitive used by bulge/arc helpers.
        /// </summary>
        public static double GetChordLength(Point3D start, Point3D end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        internal static double GetClosestParameter(Point3D point, Point3D start, Point3D end, bool extend)
        {
            var lineVec = end - start;
            double lenSq = lineVec.LengthSquared;

            // Degenerate segment → treat as a point
            if (lenSq < 1e-20)
                return 0.0;

            var pointVec = point - start;

            // Projection parameter
            double t = Vector3D.Dot(pointVec, lineVec) / lenSq;

            // Clamp if not extending
            if (!extend)
                t = Math.Max(0.0, Math.Min(1.0, t));

            return t;
        }

        internal static Vector3D GetFirstDerivative(double t, Point3D start, Point3D end)
        {
            // For a straight line the first derivative (tangent) is constant:
            // the normalized direction vector from StartPoint to EndPoint.
            var dir = end - start; // returns Vector3D
            double len = dir.Length;

            // If the line has zero length there is no well-defined tangent.
            if (len <= 1e-12)
                return Vector3D.Zero;

            return new Vector3D(dir.X / len, dir.Y / len, dir.Z / len);
        }

        internal static Vector3D GetSecondDerivative(double t, Point3D start, Point3D end)
        {
            return new Vector3D(0, 0, 0);
        }

        internal static bool IsPointOnSegment(Point3D point, Point3D start, Point3D end, double tolerance)
        {
            // Degenerate segment (start == end)
            if ((end - start).LengthSquared < double.Epsilon)
                return point.DistanceTo(start) <= tolerance;

            // Vector from start to end
            var seg = end - start;
            var v = point - start;

            double segLenSq = seg.LengthSquared;

            // Projection parameter t = (v·seg) / |seg|²
            double t = Vector3D.Dot(v, seg) / segLenSq;

            // Clamp to segment domain
            if (t < 0.0 || t > 1.0)
                return false;

            // Closest point on the segment
            Point3D closest = start + seg * t;

            // Check perpendicular distance
            return point.DistanceTo(closest) <= tolerance;
        }
    }
}