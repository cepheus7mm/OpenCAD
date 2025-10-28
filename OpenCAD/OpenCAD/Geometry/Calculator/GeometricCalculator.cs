using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Calculator
{
    internal class GeometricCalculator
    {
        static GeometricCalculator()
        {
        }

        static Point3D MidPoint(Point3D p1, Point3D p2)
        {
            return new Point3D(
                (p1.X + p2.X) / 2.0,
                (p1.Y + p2.Y) / 2.0,
                (p1.Z + p2.Z) / 2.0
            );
        }

        static double AngleBetweenVectors(Point3D v1, Point3D v2)
        {
            double dotProduct = v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
            double lengthsProduct = v1.Length * v2.Length;
            if (lengthsProduct == 0) return 0;
            double cosAngle = dotProduct / lengthsProduct;
            cosAngle = Math.Clamp(cosAngle, -1.0, 1.0);
            return Math.Acos(cosAngle);
        }

        static Point3D ReflectPoint(Point3D point, Point3D planePoint, Point3D planeNormal)
        {
            Point3D pToPoint = new Point3D(point.X - planePoint.X, point.Y - planePoint.Y, point.Z - planePoint.Z);
            double distance = pToPoint.X * planeNormal.X + pToPoint.Y * planeNormal.Y + pToPoint.Z * planeNormal.Z;
            return new Point3D(
                point.X - 2 * distance * planeNormal.X,
                point.Y - 2 * distance * planeNormal.Y,
                point.Z - 2 * distance * planeNormal.Z
            );
        }

        static Point3D Perpendicular (Point3D point, Line line)
        {
            Point3D lineDir = new Point3D(
                line.End.X - line.Start.X,
                line.End.Y - line.Start.Y,
                line.End.Z - line.Start.Z
            );
            Point3D pToStart = new Point3D(
                point.X - line.Start.X,
                point.Y - line.Start.Y,
                point.Z - line.Start.Z
            );
            double t = (pToStart.X * lineDir.X + pToStart.Y * lineDir.Y + pToStart.Z * lineDir.Z) /
                       (lineDir.X * lineDir.X + lineDir.Y * lineDir.Y + lineDir.Z * lineDir.Z);
            Point3D projection = new Point3D(
                line.Start.X + t * lineDir.X,
                line.Start.Y + t * lineDir.Y,
                line.Start.Z + t * lineDir.Z
            );
            return projection;
        }
    }
}
