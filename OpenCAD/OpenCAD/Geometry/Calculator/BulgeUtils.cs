using OpenCAD.Geometry.Helpers;
using System;

namespace OpenCAD.Geometry.Calculator
{
    internal static class BulgeUtils
    {
        public static double GetSagittaFromBulge(Point3D start, Point3D end, double bulge)
        {
            double c = LineUtils.GetChordLength(start, end);
            return Math.Abs(bulge) * c / 2.0;
        }

        public static double GetRadiusFromBulge(Point3D start, Point3D end, double bulge)
        {
            double c = LineUtils.GetChordLength(start, end);
            if (Math.Abs(bulge) < 1e-12)
                return double.PositiveInfinity; // straight line

            double s = GetSagittaFromBulge(start, end, bulge);
            return (c * c) / (8.0 * s) + s / 2.0;
        }

        public static double GetBulgeFromRadius(Point3D start, Point3D end, double radius)
        {
            double c = LineUtils.GetChordLength(start, end);
            if (c < 1e-12 || Math.Abs(radius) < 1e-12)
                return 0.0;

            double Rabs = Math.Abs(radius);
            double arg = c / (2.0 * Rabs);
            arg = Math.Clamp(arg, -1.0, 1.0);

            double theta = 2.0 * Math.Asin(arg);
            double bulge = Math.Tan(theta / 4.0);

            if (radius < 0.0)
                bulge = -bulge;

            return bulge;
        }

        public static double GetBulgeFromThreePoints(Point3D start, Point3D arc, Point3D end)
        {
            double x1 = start.X, y1 = start.Y;
            double x2 = arc.X, y2 = arc.Y;
            double x3 = end.X, y3 = end.Y;

            double a = x1 * (y2 - y3) - y1 * (x2 - x3) + x2 * y3 - x3 * y2;
            if (Math.Abs(a) < 1e-12)
                return 0.0; // collinear → no arc

            double b = (x1 * x1 + y1 * y1) * (y3 - y2)
                        + (x2 * x2 + y2 * y2) * (y1 - y3)
                        + (x3 * x3 + y3 * y3) * (y2 - y1);

            double c = (x1 * x1 + y1 * y1) * (x2 - x3)
                        + (x2 * x2 + y2 * y2) * (x3 - x1)
                        + (x3 * x3 + y3 * y3) * (x1 - x2);

            double cx = -b / (2.0 * a);
            double cy = -c / (2.0 * a);

            double dx = x1 - cx;
            double dy = y1 - cy;
            double radius = Math.Sqrt(dx * dx + dy * dy);
            if (radius < 1e-12)
                return 0.0;

            double startAngle = Math.Atan2(y1 - cy, x1 - cx);
            double midAngle = Math.Atan2(y2 - cy, x2 - cx);
            double endAngle = Math.Atan2(y3 - cy, x3 - cx);

            double theta1 = AngleUtils.NormalizeSigned(midAngle - startAngle);
            double theta2 = AngleUtils.NormalizeSigned(endAngle - midAngle);
            double theta = theta1 + theta2; // signed included angle

            double bulge = Math.Tan(theta / 4.0);
            return bulge;
        }

        public static Point3D GetMidpointFromBulge(Point3D startPoint, Point3D endPoint, double bulge)
        {
            if (Math.Abs(bulge) < 1e-10)
            {
                return new Point3D(
                    (startPoint.X + endPoint.X) / 2.0,
                    (startPoint.Y + endPoint.Y) / 2.0,
                    (startPoint.Z + endPoint.Z) / 2.0
                );
            }

            double chordMidX = (startPoint.X + endPoint.X) / 2.0;
            double chordMidY = (startPoint.Y + endPoint.Y) / 2.0;
            double chordLength = LineUtils.GetChordLength(startPoint, endPoint);
            double sagitta = (bulge * chordLength) / 4.0;
            double dx = endPoint.X - startPoint.X;
            double dy = endPoint.Y - startPoint.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            double perpX = -dy / length;
            double perpY = dx / length;
            double midX = chordMidX + perpX * sagitta;
            double midY = chordMidY + perpY * sagitta;
            return new Point3D(midX, midY, (startPoint.Z + endPoint.Z) / 2.0);
        }

        public static double GetBulgeFromDirection(Point3D startPoint, Point3D endPoint, double directionAngle)
        {
            double dx_tan = Math.Cos(directionAngle);
            double dy_tan = Math.Sin(directionAngle);

            double dx_chord = endPoint.X - startPoint.X;
            double dy_chord = endPoint.Y - startPoint.Y;
            double chordLength = Math.Sqrt(dx_chord * dx_chord + dy_chord * dy_chord);

            if (chordLength < 1e-10)
                return 0; // Degenerate case

            double perpX = -dy_tan;
            double perpY = dx_tan;

            double midX = (startPoint.X + endPoint.X) / 2.0;
            double midY = (startPoint.Y + endPoint.Y) / 2.0;

            double toMidX = midX - startPoint.X;
            double toMidY = midY - startPoint.Y;

            double dot = (dx_chord * dx_tan + dy_chord * dy_tan) / chordLength;
            double sagitta = 0.0;
            double bulge = 0.0;
            double cross = 0.0;
            if (Math.Abs(dot) < 1e-10)
            {
                sagitta = chordLength / 2.0;
                bulge = (4 * sagitta) / chordLength;
                cross = dx_chord * dy_tan - dy_chord * dx_tan;
                return cross < 0 ? -bulge : bulge;
            }

            double projection = toMidX * perpX + toMidY * perpY;
            double sinAngle = Math.Sqrt(1 - dot * dot);
            sagitta = Math.Abs(projection / sinAngle);
            bulge = (4 * sagitta) / chordLength;

            cross = dx_chord * dy_tan - dy_chord * dx_tan;
            if (cross < 0)
                bulge = -bulge;

            return bulge;
        }

        public static double GetBulgeFromCenter(Point3D startPoint, Point3D endPoint, Point3D center)
        {
            double angleStart = Math.Atan2(startPoint.Y - center.Y, startPoint.X - center.X);
            double angleEnd = Math.Atan2(endPoint.Y - center.Y, endPoint.X - center.X);

            double includedAngle = angleEnd - angleStart;

            // Normalize to [-π, π]
            includedAngle = AngleUtils.NormalizeSigned(includedAngle);

            return Math.Tan(includedAngle / 4.0);
        }

        public static (double radius, double startAngle, double endAngle, Point3D center) GetArcParametersFromBulge(Point3D startPoint, Point3D endPoint, double bulge)
        {
            double radius = GetRadiusFromBulge(startPoint, endPoint, bulge);
            var chordMid = LineUtils.GetPointAtParameter(0.5, startPoint, endPoint);
            double chordLength = startPoint.DistanceTo(endPoint);
            double sagitta = GetSagittaFromBulge(startPoint, endPoint, bulge);
            double dx = endPoint.X - startPoint.X;
            double dy = endPoint.Y - startPoint.Y;
            double perpX = -dy / chordLength;
            double perpY = dx / chordLength;
            double dir = bulge > 0 ? 1.0 : -1.0;
            double centerX = chordMid.X + perpX * (radius - sagitta) * dir;
            double centerY = chordMid.Y + perpY * (radius - sagitta) * dir;
            double startAngle = Math.Atan2(startPoint.Y - centerY, startPoint.X - centerX);
            double endAngle = Math.Atan2(endPoint.Y - centerY, endPoint.X - centerX);

            if (dir > 0)
            {
                if (endAngle < startAngle) endAngle += 2.0 * Math.PI;
            }
            else
            {
                if (endAngle > startAngle) endAngle -= 2.0 * Math.PI;
            }

            return (radius, startAngle, endAngle, new Point3D(centerX, centerY, startPoint.Z));
        }

        public static Point3D GetCenterFromBulge(Point3D start, Point3D end, double bulge)
        {
            double c = LineUtils.GetChordLength(start, end);
            if (c < 1e-12 || Math.Abs(bulge) < 1e-12)
                return Point3D.NotAPoint;

            double s = GetSagittaFromBulge(start, end, bulge);
            double R = GetRadiusFromBulge(start, end, bulge);

            double centerOffset = R - s; // distance from chord midpoint to center

            double mx = (start.X + end.X) * 0.5;
            double my = (start.Y + end.Y) * 0.5;

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double invLen = 1.0 / c;
            double perpX = -dy * invLen;
            double perpY = dx * invLen;

            double side = bulge >= 0.0 ? 1.0 : -1.0;

            double cx = mx + perpX * centerOffset * side;
            double cy = my + perpY * centerOffset * side;

            return new Point3D(cx, cy, start.Z);
        }

        public static double GetIncludedAngleFromBulge(Point3D center, Point3D start, Point3D end, double bulge)
        {
            ArcUtils.GetRawArcAnglesFromCenter(center, start, end, out double startAngle, out double endAngle);
            double theta = AngleUtils.NormalizeSigned(endAngle - startAngle);

            if (bulge > 0.0 && theta < 0.0)
                theta += 2.0 * Math.PI;
            else if (bulge < 0.0 && theta > 0.0)
                theta -= 2.0 * Math.PI;

            return theta;
        }

        public static double GetBulgeFromAngle(double includedAngle)
        {
            return Math.Tan(includedAngle / 4.0);
        }

        public static double GetAngleFromBulge(double bulge)
        {
            if (Math.Abs(bulge) < 1e-12)
                return 0.0;
            return 4.0 * Math.Atan(bulge);
        }

        public static PolylineSegment GetPolylineSegment(Point3D start, Point3D end, double bulge)
        {
            // Line segment
            if (Math.Abs(bulge) < 1e-12)
                return new PolylineSegment(start, end);

            // Arc segment
            Point3D center = GetCenterFromBulge(start, end, bulge);
            double sweep = 4.0 * Math.Atan(bulge);   // signed sweep

            return new PolylineSegment(start, end, center, sweep);
        }
    }
}