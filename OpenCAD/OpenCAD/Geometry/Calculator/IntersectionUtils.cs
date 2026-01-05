using System;
using OpenCAD.Interfaces;
using OpenCAD.Geometry.Helpers;

namespace OpenCAD.Geometry.Calculator
{
    internal static class IntersectionUtils
    {
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

        public static (Point3D, Point3D) LineCircleIntersections(Line line, ICircularGeometry circle)
        {
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

        public static Point3D LineLineIntersection(Line line1, Line line2)
        {
            Vector3D? dir1 = line1.GetFirstDerivativeAtParameter(line1.DomainStart);
            Vector3D? dir2 = line2.GetFirstDerivativeAtParameter(line2.DomainStart);
            if (dir1 == dir2 || !dir1.HasValue || !dir2.HasValue)
                return Point3D.NotAPoint;

            var p1 = (line1.StartPoint.X, line1.StartPoint.Y);
            var p2 = (line2.StartPoint.X, line2.StartPoint.Y);

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;

            double det = dir1.Value.X * dir2.Value.Y - dir1.Value.Y * dir2.Value.X;
            if (Math.Abs(det) < 1e-10)
                return Point3D.NotAPoint;

            double t = (dx * dir2.Value.Y - dy * dir2.Value.X) / det;
            var intersection = (p1.X + t * dir1.Value.X, p1.Y + t * dir1.Value.Y);
            return new Point3D(intersection.Item1, intersection.Item2, line1.StartPoint.Z);
        }

        public static (Point3D, Point3D) CircleCircleIntersections(ICircularGeometry circ1, ICircularGeometry circ2)
        {
            var c1 = circ1.Center;
            var c2 = circ2.Center;
            var r1 = circ1.Radius;
            var r2 = circ2.Radius;

            double dx = c2.X - c1.X;
            double dy = c2.Y - c1.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);

            if (d < 1e-10)
                return (Point3D.NotAPoint, Point3D.NotAPoint);

            if (d > r1 + r2 + 1e-10)
                return (Point3D.NotAPoint, Point3D.NotAPoint);

            if (d < Math.Abs(r1 - r2) - 1e-10)
                return (Point3D.NotAPoint, Point3D.NotAPoint);

            if (Math.Abs(d - (r1 + r2)) < 1e-10 || Math.Abs(d - Math.Abs(r1 - r2)) < 1e-10)
            {
                double t = r1 / d;
                var pt = new Point3D(
                    c1.X + t * dx,
                    c1.Y + t * dy,
                    (c1.Z + c2.Z) / 2.0
                );
                return (pt, Point3D.NotAPoint);
            }

            double a = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
            double h = Math.Sqrt(r1 * r1 - a * a);

            double px = c1.X + a * dx / d;
            double py = c1.Y + a * dy / d;

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
    }
}