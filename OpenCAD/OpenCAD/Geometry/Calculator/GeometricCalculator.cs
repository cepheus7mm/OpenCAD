using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCAD.Geometry.Helpers;

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

        public static double AngleBetweenVectors(Point3D v1, Point3D v2)
        {
            double dotProduct = v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
            double lengthsProduct = v1.Length * v2.Length;
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
    }
}
