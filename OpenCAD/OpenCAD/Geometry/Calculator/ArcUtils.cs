using OpenCAD.Geometry.Helpers;
using OpenCAD.Geometry.Helpers.GeoPoints;
using System;
using System.Drawing;

namespace OpenCAD.Geometry.Calculator
{
    internal static class ArcUtils
    {
        public static Point3D GetPointAtParameter(double t, Point3D start, Point3D center, double sweep)
        {
            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);

            double angle = a0 + t * sweep;

            double r = (start - center).Length;

            return new Point3D(
                center.X + r * Math.Cos(angle),
                center.Y + r * Math.Sin(angle),
                center.Z
            );
        }

        public static double GetParameterAtPoint(Point3D point, Point3D start, Point3D center, double sweep)
        {
            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double ap = Math.Atan2(point.Y - center.Y, point.X - center.X);

            // Directed angle from start → point
            double delta = AngleUtils.NormalizeSigned(ap - a0);

            // Parameter = delta / sweep
            double t = delta / sweep;

            return Math.Clamp(t, 0.0, 1.0);
        }

        public static Point3D GetClosestPoint(Point3D point, Point3D start, Point3D center, double sweep, bool extend = false)
        {
            double R = (start - center).Length;

            // Project point onto circle
            var v = point - center;
            double len = v.Length;

            if (len < 1e-12)
                return start;

            var projected = center + v * (R / len);

            // Angles
            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double ap = Math.Atan2(projected.Y - center.Y, projected.X - center.X);

            // Directed angle from start → projected
            double delta = AngleUtils.NormalizeUnsigned(ap - a0);

            // Parameter along arc
            double t = delta / sweep;

            if (!extend)
                t = Math.Clamp(t, 0.0, 1.0);

            // Final angle
            double aFinal = a0 + t * sweep;

            return new Point3D(
                center.X + R * Math.Cos(aFinal),
                center.Y + R * Math.Sin(aFinal),
                start.Z
            );
        }

        /// <summary>
        /// Computes the tangent point on a circle (center, radius) from an external point.
        /// Returns a GeoPoint with mode <see cref="GeoPointModes.Tangent"/>. Throws if the source point lies inside or on the circle.
        /// </summary>
        /// <param name="center">Circle center.</param>
        /// <param name="radius">Circle radius (positive).</param>
        /// <param name="fromPoint">Point from which tangent(s) are drawn (must be outside circle).</param>
        /// <param name="nearPoint">Reference point used to choose the nearer tangent if two exist.</param>
        /// <returns>A <see cref="GeoPoint"/> positioned at the chosen tangent point.</returns>
        public static GeoPoint GetClosestTangent(Point3D center, double radius, Point3D fromPoint, Point3D nearPoint)
        {
            if (radius <= 0.0)
                throw new ArgumentException("Radius must be positive.", nameof(radius));

            Vector3D v = fromPoint - center;
            double distSq = v.LengthSquared;
            double radiusSq = radius * radius;

            if (distSq <= radiusSq)
                throw new ArgumentException("Point is inside or on the circle; no tangent exists.", nameof(fromPoint));

            double dist = Math.Sqrt(distSq);

            // Base angle from center to external point
            double theta = Math.Atan2(v.Y, v.X);

            // Tangent angle offset
            double offset = Math.Acos(radius / dist);

            // Two tangent angles
            double a1 = theta + offset;
            double a2 = theta - offset;

            // Normalize (optional but recommended)
            a1 = AngleUtils.NormalizeSigned(a1);
            a2 = AngleUtils.NormalizeSigned(a2);

            // Construct tangent points
            Point3D t1 = new Point3D(
                center.X + radius * Math.Cos(a1),
                center.Y + radius * Math.Sin(a1),
                center.Z);

            Point3D t2 = new Point3D(
                center.X + radius * Math.Cos(a2),
                center.Y + radius * Math.Sin(a2),
                center.Z);

            // Choose the tangent closest to nearPoint
            double d1 = (t1 - nearPoint).LengthSquared;
            double d2 = (t2 - nearPoint).LengthSquared;

            Point3D chosen = d1 < d2 ? t1 : t2;

            return new GeoPoint(chosen, GeoPointModes.Tangent);
        }

        public static bool IsPointOnArc(Point3D point, Arc arc)
        {
            return IsPointOnArc(point, arc.StartPoint, arc.Center, arc.Angle, 1e-9);
        }

        public static void GetRawArcAnglesFromCenter(Point3D center, Point3D start, Point3D end, out double startAngle, out double endAngle)
        {
            startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
        }

        internal static double GetClosestParameter(Point3D point, Point3D start, Point3D center, double sweep, bool IsClosed, bool extend = false)
        {
            // Angle of start
            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);

            // Angle of projected point
            double ap = Math.Atan2(point.Y - center.Y, point.X - center.X);

            // Directed angle from start → point
            double delta = AngleUtils.NormalizeSigned(ap - a0);

            // Parameter along arc
            double t = delta / sweep;

            if (!extend)
                t = Math.Clamp(t, 0.0, 1.0);

            return t;
        }

        internal static Vector3D GetFirstDerivative(double t, Point3D start, Point3D center, double sweep)
        {
            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double angle = a0 + t * sweep;

            // Unit tangent direction
            double tx = -Math.Sin(angle);
            double ty = Math.Cos(angle);

            // Scale by angular speed (sweep)
            return new Vector3D(tx, ty, 0) * sweep;
        }

        internal static Vector3D GetSecondDerivative(double t, Point3D start, Point3D center, double sweep)
        {
            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double angle = a0 + t * sweep;

            double R = (start - center).Length;
            if (R < 1e-12)
                return new Vector3D(0, 0, 0);

            // Second derivative of circular arc
            double factor = -(sweep * sweep) / R;

            double nx = Math.Cos(angle);
            double ny = Math.Sin(angle);

            return new Vector3D(nx * factor, ny * factor, 0);
        }

        internal static double GetLength(Point3D start, Point3D center, double sweep)
        {
            double r = (start - center).Length;
            if (r < 1e-12)
                return 0.0;

            return r * sweep;
        }

        internal static bool IsPointOnArc(Point3D point, Point3D start, Point3D center, double sweep, double tolerance)
        {
            // 1. Radius check
            double R = (start - center).Length;
            double dist = point.DistanceTo(center);

            if (Math.Abs(dist - R) > tolerance)
                return false;

            // 2. Compute angles
            double a0 = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double ap = Math.Atan2(point.Y - center.Y, point.X - center.X);

            // 3. Directed angle from start → point
            double delta = AngleUtils.NormalizeSigned(ap - a0);

            // 4. Check inclusion based on sweep direction
            if (sweep > 0)
            {
                // CCW arc
                return delta >= -tolerance && delta <= sweep + tolerance;
            }
            else
            {
                // CW arc
                return delta <= tolerance && delta >= sweep - tolerance;
            }
        }
    }
}