using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Calculator
{
    public static class GeometricCalculator
    {
        public static Point3D MidPoint(Point3D p1, Point3D p2)
        {
            return new Point3D(
                (p1.X + p2.X) / 2.0,
                (p1.Y + p2.Y) / 2.0,
                (p1.Z + p2.Z) / 2.0
            );
        }

        public static GeoPoint MidPoint(Line line)
        {
            var geoPoint = new GeoPoint(MidPoint(line.StartPoint, line.EndPoint), GeoPointModes.Middle);
            geoPoint.RelatedGeometryId = line.ID;
            return  geoPoint;
        }

        public static GeoPoint MidPoint(Arc arc)
        {
            double midAngle = arc.StartAngle + arc.GetSweepAngle() / 2.0;
            var mid = new Point3D(
                arc.Center.X + arc.Radius * Math.Cos(midAngle),
                arc.Center.Y + arc.Radius * Math.Sin(midAngle),
                arc.Center.Z
            );
            var geoPoint = new GeoPoint(mid, GeoPointModes.Middle);
            geoPoint.RelatedGeometryId = arc.ID;
            return geoPoint;
        }

        public static double AngleBetweenVectors(Point3D vec1, Point3D vec2)
        {
            double dotProduct = vec1.X * vec2.X + vec1.Y * vec2.Y + vec1.Z * vec2.Z;
            double lengthsProduct = vec1.Length * vec2.Length;
            if (lengthsProduct == 0) return 0;
            double cosAngle = dotProduct / lengthsProduct;
            cosAngle = Math.Clamp(cosAngle, -1.0, 1.0);
            return Math.Acos(cosAngle);
        }

        public static Point3D ReflectPoint(Point3D point, Point3D planePoint, Point3D planeNormal)
        {
            Point3D pToPoint = new Point3D(point.X - planePoint.X, point.Y - planePoint.Y, point.Z - planePoint.Z);
            double distance = pToPoint.X * planeNormal.X + pToPoint.Y * planeNormal.Y + pToPoint.Z * planeNormal.Z;
            return new Point3D(
                point.X - 2 * distance * planeNormal.X,
                point.Y - 2 * distance * planeNormal.Y,
                point.Z - 2 * distance * planeNormal.Z
            );
        }

        public static GeoPoint Perpendicular (Point3D point, Line line)
        {
            Point3D lineDir = new Point3D(
                line.EndPoint.X - line.StartPoint.X,
                line.EndPoint.Y - line.StartPoint.Y,
                line.EndPoint.Z - line.StartPoint.Z
            );
            Point3D pToStart = new Point3D(
                point.X - line.StartPoint.X,
                point.Y - line.StartPoint.Y,
                point.Z - line.StartPoint.Z
            );
            double t = (pToStart.X * lineDir.X + pToStart.Y * lineDir.Y + pToStart.Z * lineDir.Z) /
                       (lineDir.X * lineDir.X + lineDir.Y * lineDir.Y + lineDir.Z * lineDir.Z);
            Point3D projection = new Point3D(
                line.StartPoint.X + t * lineDir.X,
                line.StartPoint.Y + t * lineDir.Y,
                line.StartPoint.Z + t * lineDir.Z
            );
            var geoPoint = new GeoPoint(projection, GeoPointModes.Perpendicular);
            geoPoint.RelatedGeometryId = line.ID;
            return geoPoint;
        }

        public static GeoPoint Perpendicular(Point3D point, Arc arc)
        {
            var geoPoint = new GeoPoint(arc.GetClosestPointTo(point, false), GeoPointModes.Perpendicular);
            geoPoint.RelatedGeometryId = arc.ID;
            return geoPoint;
        }

        public static bool TryGetCircleThroughThreePoints(Point3D p1, Point3D p2, Point3D p3, out Point3D center, out double radius)
        {
            center = Point3D.Origin;
            radius = double.NaN;

            double x1 = p1.X, y1 = p1.Y;
            double x2 = p2.X, y2 = p2.Y;
            double x3 = p3.X, y3 = p3.Y;

            double a = x1 - x2;
            double b = y1 - y2;
            double c = x1 - x3;
            double d = y1 - y3;

            double e = ((x1 * x1 - x2 * x2) + (y1 * y1 - y2 * y2)) / 2.0;
            double f = ((x1 * x1 - x3 * x3) + (y1 * y1 - y3 * y3)) / 2.0;

            double det = a * d - b * c;
            if (Math.Abs(det) < 1e-12)
                return false; // colinear or nearly so

            double cx = (d * e - b * f) / det;
            double cy = (-c * e + a * f) / det;

            center = new Point3D(cx, cy, (p1.Z + p2.Z + p3.Z) / 3.0);
            radius = Math.Sqrt((cx - x1) * (cx - x1) + (cy - y1) * (cy - y1));
            if (double.IsNaN(radius) || double.IsInfinity(radius) || radius < 1e-12)
                return false;

            return true;
        }

        /// <summary>
        /// Normalize angle to [0, 2*PI)
        /// </summary>
        public static double NormalizeAngle(double angle)
        {
            double twoPi = Math.PI * 2.0;
            double a = angle % twoPi;
            if (a < 0) a += twoPi;
            return a;
        }

        public static GeoPoint GetClosestTangent(Point3D center, double radius, Point3D fromPoint, Point3D nearPoint)
        {
            Vector3D centerToFrom = new Vector3D(fromPoint.X - center.X, fromPoint.Y - center.Y, fromPoint.Z - center.Z);
            double distSq = centerToFrom.LengthSquared;
            double radiusSq = radius * radius;
            if (distSq <= radiusSq)
                throw new ArgumentException("Point is inside or on the circle; no tangent exists.");
            double dist = Math.Sqrt(distSq);
            double angleToFrom = Math.Atan2(centerToFrom.Y, centerToFrom.X);
            double angleOffset = Math.Acos(radius / dist);
            double tangentAngle1 = angleToFrom + angleOffset;
            double tangentAngle2 = angleToFrom - angleOffset;
            Point3D tangentPoint1 = new Point3D(
                center.X + radius * Math.Cos(tangentAngle1),
                center.Y + radius * Math.Sin(tangentAngle1),
                center.Z
            );
            Point3D tangentPoint2 = new Point3D(
                center.X + radius * Math.Cos(tangentAngle2),
                center.Y + radius * Math.Sin(tangentAngle2),
                center.Z
            );
            double distToTangent1 = (tangentPoint1 - nearPoint).LengthSquared;
            double distToTangent2 = (tangentPoint2 - nearPoint).LengthSquared;
            if (distToTangent1 < distToTangent2)
                return new GeoPoint(tangentPoint1, GeoPointModes.Tangent);
            else
                return new GeoPoint(tangentPoint2, GeoPointModes.Tangent);
        }

        /// <summary>
        /// Calculates intersection points between two drawable objects.
        /// For arcs, this treats them as full circles - the caller is responsible for filtering points that fall outside arc sweeps.
        /// </summary>
        /// <param name="obj1">First drawable object (Line, Arc, or Circle)</param>
        /// <param name="obj2">Second drawable object (Line, Arc, or Circle)</param>
        /// <returns>A tuple of two Point3D objects. If only one intersection exists, pt2 will be Point3D.NotAPoint. If no intersections exist, both will be Point3D.NotAPoint.</returns>
        public static (Point3D, Point3D) Intersection(IDrawable obj1, IDrawable obj2)
        {
            if (obj1 == null || obj2 == null)
                return (Point3D.NotAPoint, Point3D.NotAPoint);

            if (obj1 is Line line1 && obj2 is Line line2)
            {
                var pt1 = LineLineIntersection(line1, line2);
                return (pt1, Point3D.NotAPoint);
            }
            else if (obj1 is Line lineA && obj2 is ICircularGeometry circ)
            {
                return LineCircleIntersections(lineA, circ);
            }
            else if (obj1 is ICircularGeometry circ1 && obj2 is Line lineB)
            {
                return LineCircleIntersections(lineB, circ1);
            }
            else if (obj1 is ICircularGeometry circA && obj2 is ICircularGeometry circB)
            {
                return CircleCircleIntersections(circA, circB);
            }

            return (Point3D.NotAPoint, Point3D.NotAPoint);
        }

        /// <summary>
        /// Calculates intersection points between a line and a circle/arc.
        /// Arcs are treated as full circles - caller must filter points outside arc sweep if needed.
        /// </summary>
        private static (Point3D, Point3D) LineCircleIntersections(Line line, ICircularGeometry circle)
        {
            // Project to XY plane
            var p1 = line.StartPoint;
            var p2 = line.EndPoint;
            var cx = circle.Center.X;
            var cy = circle.Center.Y;
            var r = circle.Radius;

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;

            double a = dx * dx + dy * dy;
            double b = 2 * (dx * (p1.X - cx) + dy * (p1.Y - cy));
            double c = (p1.X - cx) * (p1.X - cx) + (p1.Y - cy) * (p1.Y - cy) - r * r;

            double discriminant = b * b - 4 * a * c;

            if (a == 0 || discriminant < 0)
                return (Point3D.NotAPoint, Point3D.NotAPoint); // No intersection

            if (Math.Abs(discriminant) < 1e-10)
            {
                // One intersection (tangent)
                double t = -b / (2 * a);
                var pt = new Point3D(
                    p1.X + t * dx,
                    p1.Y + t * dy,
                    p1.Z + t * (p2.Z - p1.Z)
                );
                return (pt, Point3D.NotAPoint);
            }
            else
            {
                // Two intersections
                double sqrtDisc = Math.Sqrt(discriminant);
                double t1 = (-b + sqrtDisc) / (2 * a);
                double t2 = (-b - sqrtDisc) / (2 * a);

                var pt1 = new Point3D(
                    p1.X + t1 * dx,
                    p1.Y + t1 * dy,
                    p1.Z + t1 * (p2.Z - p1.Z)
                );
                var pt2 = new Point3D(
                    p1.X + t2 * dx,
                    p1.Y + t2 * dy,
                    p1.Z + t2 * (p2.Z - p1.Z)
                );
                return (pt1, pt2);
            }
        }

        private static Point3D LineLineIntersection(Line line1, Line line2)
        {
            Vector3D dir1 = line1.GetFirstDerivate(line1.StartPoint);
            Vector3D dir2 = line2.GetFirstDerivate(line2.StartPoint);
            // if lines are parallel return NotAPoint
            if (dir1 == dir2)
                return Point3D.NotAPoint;

            // Using parametric line equations to find intersection
            var p1 = (line1.StartPoint.X, line1.StartPoint.Y);
            var p2 = (line2.StartPoint.X, line2.StartPoint.Y);
            var intersection = (double.NaN, double.NaN);

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;

            // Determinant
            double det = dir1.X * dir2.Y - dir1.Y * dir2.X;
            if (Math.Abs(det) < 1e-10)
                return Point3D.NotAPoint; // parallel or coincident

            double t = (dx * dir2.Y - dy * dir2.X) / det;
            intersection = (p1.X + t * dir1.X, p1.Y + t * dir1.Y);
            return new Point3D(intersection.Item1, intersection.Item2, line1.StartPoint.Z);
        }

        /// <summary>
        /// Calculates intersection points between two circles/arcs.
        /// Arcs are treated as full circles - caller must filter points outside arc sweeps if needed.
        /// </summary>
        private static (Point3D, Point3D) CircleCircleIntersections(ICircularGeometry circ1, ICircularGeometry circ2)
        {
            // Get centers and radii
            var c1 = circ1.Center;
            var c2 = circ2.Center;
            var r1 = circ1.Radius;
            var r2 = circ2.Radius;

            // Distance between centers
            double dx = c2.X - c1.X;
            double dy = c2.Y - c1.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);

            // Check for no intersection cases
            if (d < 1e-10)
            {
                // Concentric circles
                return (Point3D.NotAPoint, Point3D.NotAPoint);
            }

            if (d > r1 + r2 + 1e-10)
            {
                // Circles too far apart
                return (Point3D.NotAPoint, Point3D.NotAPoint);
            }

            if (d < Math.Abs(r1 - r2) - 1e-10)
            {
                // One circle inside the other
                return (Point3D.NotAPoint, Point3D.NotAPoint);
            }

            // Check for tangent (one intersection point)
            if (Math.Abs(d - (r1 + r2)) < 1e-10 || Math.Abs(d - Math.Abs(r1 - r2)) < 1e-10)
            {
                // Tangent - one intersection point
                double t = r1 / d;
                var pt = new Point3D(
                    c1.X + t * dx,
                    c1.Y + t * dy,
                    (c1.Z + c2.Z) / 2.0
                );

                return (pt, Point3D.NotAPoint);
            }

            // Two intersection points
            // Using formula from: https://mathworld.wolfram.com/Circle-CircleIntersection.html
            double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
            double h = Math.Sqrt(r1 * r1 - a * a);

            // Point on the line between centers
            double px = c1.X + a * dx / d;
            double py = c1.Y + a * dy / d;

            // Two intersection points (perpendicular to line between centers)
            var pt1 = new Point3D(
                px + h * dy / d,
                py - h * dx / d,
                (c1.Z + c2.Z) / 2.0
            );
            var pt2 = new Point3D(
                px - h * dy / d,
                py + h * dx / d,
                (c1.Z + c2.Z) / 2.0
            );

            return (pt1, pt2);
        }

        /// <summary>
        /// Checks if a point lies on an arc's sweep (between start and end angles).
        /// Useful for filtering intersection points after calling Intersection() with arcs.
        /// </summary>
        /// <param name="point">The point to check</param>
        /// <param name="arc">The arc to check against</param>
        /// <returns>True if the point is on the arc's sweep, false otherwise</returns>
        public static bool IsPointOnArc(Point3D point, Arc arc)
        {
            // Calculate angle from arc center to point
            double dx = point.X - arc.Center.X;
            double dy = point.Y - arc.Center.Y;
            double angle = Math.Atan2(dy, dx);

            // Normalize to [0, 2π)
            angle = NormalizeAngle(angle);
            double start = NormalizeAngle(arc.StartAngle);
            double end = NormalizeAngle(arc.EndAngle);

            // Check if angle is within arc sweep (with small tolerance for numerical errors)
            const double tolerance = 1e-10;
            if (start <= end)
            {
                // Normal case: no wrap-around
                return angle >= start - tolerance && angle <= end + tolerance;
            }
            else
            {
                // Wrap-around case: arc crosses 0°
                return angle >= start - tolerance || angle <= end + tolerance;
            }
        }
    }
}
