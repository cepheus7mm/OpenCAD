using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCAD.Geometry.Helpers;

namespace OpenCAD.Geometry.Calculator
{
    internal static class GeometricCalculator
    {
        internal static Point3D MidPoint(Point3D p1, Point3D p2)
        {
            return new Point3D(
                (p1.X + p2.X) / 2.0,
                (p1.Y + p2.Y) / 2.0,
                (p1.Z + p2.Z) / 2.0
            );
        }

        internal static GeoPoint MidPoint(Line line)
        {
            var geoPoint = new GeoPoint(MidPoint(line.StartPoint, line.EndPoint), GeoPointModes.Middle);
            geoPoint.RelatedGeometryId = line.ID;
            return  geoPoint;
        }

        internal static GeoPoint MidPoint(Arc arc)
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

        internal static double AngleBetweenVectors(Point3D v1, Point3D v2)
        {
            double dotProduct = v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
            double lengthsProduct = v1.Length * v2.Length;
            if (lengthsProduct == 0) return 0;
            double cosAngle = dotProduct / lengthsProduct;
            cosAngle = Math.Clamp(cosAngle, -1.0, 1.0);
            return Math.Acos(cosAngle);
        }

        internal static Point3D ReflectPoint(Point3D point, Point3D planePoint, Point3D planeNormal)
        {
            Point3D pToPoint = new Point3D(point.X - planePoint.X, point.Y - planePoint.Y, point.Z - planePoint.Z);
            double distance = pToPoint.X * planeNormal.X + pToPoint.Y * planeNormal.Y + pToPoint.Z * planeNormal.Z;
            return new Point3D(
                point.X - 2 * distance * planeNormal.X,
                point.Y - 2 * distance * planeNormal.Y,
                point.Z - 2 * distance * planeNormal.Z
            );
        }

        internal static GeoPoint Perpendicular (Point3D point, Line line)
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

        internal static GeoPoint Perpendicular(Point3D point, Arc arc)
        {
            var geoPoint = new GeoPoint(arc.GetClosestPointTo(point, false), GeoPointModes.Perpendicular);
            geoPoint.RelatedGeometryId = arc.ID;
            return geoPoint;
        }
    }
}
