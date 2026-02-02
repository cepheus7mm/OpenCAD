using OpenCAD.Geometry.Helpers;
using OpenCAD.Geometry.Helpers.GeoPoints;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;

namespace OpenCAD.Geometry.Calculator
{
    public static class GeometricCalculator
    {
        // ---------------------------------------------------------------------
        // Linear Geometry
        // ---------------------------------------------------------------------
        /// <summary>
        /// Returns the midpoint between two 3D points.
        /// </summary>
        public static Point3D MidPoint(Point3D p1, Point3D p2)
        {
            return new Point3D(
                (p1.X + p2.X) / 2.0,
                (p1.Y + p2.Y) / 2.0,
                (p1.Z + p2.Z) / 2.0
            );
        }

        public static double GetParameterAtPoint(Point3D point, Point3D a, Point3D b)
            => LineUtils.GetParameterAtPoint(point, a, b);

        public static Point3D GetPointAtParameter(double t, Point3D a, Point3D b)
            => LineUtils.GetPointAtParameter(t, a, b);

        public static Point3D GetClosestPoint(Point3D point, Point3D start, Point3D end, bool extend = false)
            => LineUtils.GetClosestPoint(point, start, end, extend);

        public static double GetClosestParameter(Point3D point, Point3D start, Point3D end, bool extend = false)
            => LineUtils.GetClosestParameter(point, start, end, extend);

        public static double GetChordLength(Point3D start, Point3D end)
            => LineUtils.GetChordLength(start, end);

        public static Vector3D GetFirstDerivative(double t, Point3D start, Point3D end)
            => LineUtils.GetFirstDerivative(t, start, end);

        public static Vector3D GetSecondDerivative(double t, Point3D start, Point3D end)
            => LineUtils.GetSecondDerivative(t, start, end);

        public static bool IsPointOnLine(Point3D point, Point3D start, Point3D end, double tolerance = 1e-9)
            => LineUtils.IsPointOnSegment(point, start, end, tolerance);

        // ---------------------------------------------------------------------
        // Circular / Arc Geometry
        // ---------------------------------------------------------------------
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

        public static Point3D GetPointAtParameter(double t, Point3D start, Point3D center, double sweep)
            => ArcUtils.GetPointAtParameter(t, start, center, sweep);

        public static double GetParameterAtPoint(Point3D point, Point3D start, Point3D center, double sweep)
            => ArcUtils.GetParameterAtPoint(point, start, center, sweep);


        public static double GetClosestParameter(Point3D point, Point3D start, Point3D center, double sweep, bool IsClosed, bool extend = false)
            => ArcUtils.GetClosestParameter(point, start, center, sweep, IsClosed, extend);


        public static Point3D GetClosestPoint(Point3D point, Point3D start, Point3D center, double sweep, bool extend = false)
            => ArcUtils.GetClosestPoint(point, start, center, sweep, extend);

        public static Vector3D GetFirstDerivative(double t, Point3D start, Point3D center, double sweep)
            => ArcUtils.GetFirstDerivative(t, start, center, sweep);

        public static Vector3D GetSecondDerivative(double t, Point3D start, Point3D center, double sweep)
            => ArcUtils.GetSecondDerivative(t, start, center, sweep);

        public static double GetLength(Point3D start, Point3D center, double sweep)
            => ArcUtils.GetLength(start, center, sweep);


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
            => ArcUtils.GetClosestTangent(center, radius, fromPoint, nearPoint);

        public static void GetRawArcAnglesFromCenter(Point3D center, Point3D start, Point3D end, out double startAngle, out double endAngle)
            => ArcUtils.GetRawArcAnglesFromCenter(center, start, end, out startAngle, out endAngle);

        public static bool IsPointOnArc(Point3D point, Arc arc)
            => ArcUtils.IsPointOnArc(point, arc);

        public static bool IsPointOnArc(Point3D point, Point3D start, Point3D center, double sweep, double tolerance = 1e-9)
            => ArcUtils.IsPointOnArc(point, start, center, sweep, tolerance);


        // ---------------------------------------------------------------------
        // Bulge Geometry
        // ---------------------------------------------------------------------
        public static double GetSagittaFromBulge(Point3D start, Point3D end, double bulge)
            => BulgeUtils.GetSagittaFromBulge(start, end, bulge);

        public static double GetRadiusFromBulge(Point3D start, Point3D end, double bulge)
            => BulgeUtils.GetRadiusFromBulge(start, end, bulge);

        public static double GetBulgeFromRadius(Point3D start, Point3D end, double radius)
            => BulgeUtils.GetBulgeFromRadius(start, end, radius);

        public static double GetBulgeFromThreePoints(Point3D start, Point3D arc, Point3D end)
            => BulgeUtils.GetBulgeFromThreePoints(start, arc, end);

        public static Point3D GetMidpointFromBulge(Point3D startPoint, Point3D endPoint, double bulge)
            => BulgeUtils.GetMidpointFromBulge(startPoint, endPoint, bulge);

        public static double GetBulgeFromDirection(Point3D startPoint, Point3D endPoint, double directionAngle)
            => BulgeUtils.GetBulgeFromDirection(startPoint, endPoint, directionAngle);

        public static double GetBulgeFromCenter(Point3D startPoint, Point3D endPoint, Point3D center)
            => BulgeUtils.GetBulgeFromCenter(startPoint, endPoint, center);

        public static (double radius, double startAngle, double endAngle, Point3D center) GetArcParametersFromBulge(Point3D startPoint, Point3D endPoint, double bulge)
            => BulgeUtils.GetArcParametersFromBulge(startPoint, endPoint, bulge);

        public static Point3D GetCenterFromBulge(Point3D start, Point3D end, double bulge)
            => BulgeUtils.GetCenterFromBulge(start, end, bulge);

        public static double GetIncludedAngleFromBulge(Point3D center, Point3D start, Point3D end, double bulge)
            => BulgeUtils.GetIncludedAngleFromBulge(center, start, end, bulge);

        public static double GetBulgeFromAngle(double includedAngle)
            => BulgeUtils.GetBulgeFromAngle(includedAngle);

        public static double GetAngleFromBulge(double bulge)
            => BulgeUtils.GetAngleFromBulge(bulge);

        // ---------------------------------------------------------------------
        // Intersection / Dispatch (Misc)
        // ---------------------------------------------------------------------
        public static (Point3D, Point3D) Intersection(IDrawable obj1, IDrawable obj2)
            => IntersectionUtils.Intersection(obj1, obj2);

        // ---------------------------------------------------------------------
        // Miscellaneous / Utilities
        // ---------------------------------------------------------------------
        public static double AngleBetweenVectors(Vector3D vec1, Vector3D vec2)
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

        public static GeoPoint Perpendicular(Point3D point, Line line)
        {
            var projection = LineUtils.GetClosestPoint(point, line.Start, line.End);
            var geoPoint = new GeoPoint(projection, GeoPointModes.Perpendicular);
            geoPoint.RelatedGeometryId = line.ID;
            return geoPoint;
        }

        public static GeoPoint Perpendicular(Point3D point, Arc arc)
        {
            var closest = ArcUtils.GetClosestPoint(point, arc.Start, arc.Center, arc.Angle);
            var geoPoint = new GeoPoint(closest, GeoPointModes.Perpendicular);
            geoPoint.RelatedGeometryId = arc.ID;
            return geoPoint;
        }

        public static bool TryGetCircleThroughThreePoints(Point3D p1, Point3D p2, Point3D p3, out Point3D center, out double radius)
        {
            // Keep self-contained implementation here (unchanged)
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
        /// Provides a set of candidate GeoPoints for a segment. Delegates to line/arc/bulge helpers where appropriate.
        /// </summary>
        public static IEnumerable<GeoPoint> GetSegmentGeoPoints(Point3D position1, Point3D position2, double bulge, Point3D referencePoint, GeoPointModes geoPointType)
        {
            var candidates = new List<GeoPoint>();
            bool isLine = Math.Abs(bulge) < 1e-10;

            if (geoPointType.HasFlag(GeoPointModes.Vertex))
            {
                candidates.Add(new GeoPoint(position1, GeoPointModes.Vertex));
                candidates.Add(new GeoPoint(position2, GeoPointModes.Vertex));
            }

            if (geoPointType.HasFlag(GeoPointModes.Middle))
            {
                Point3D midpoint = BulgeUtils.GetMidpointFromBulge(position1, position2, bulge);
                candidates.Add(new GeoPoint(midpoint, GeoPointModes.Middle));
            }

            if (geoPointType.HasFlag(GeoPointModes.NearestPoint))
            {
                Point3D closest;
                if (isLine)
                    closest = LineUtils.GetClosestPoint(referencePoint, position1, position2);
                else
                {
                    var center = BulgeUtils.GetCenterFromBulge(position1, position2, bulge);
                    var sweep = BulgeUtils.GetAngleFromBulge(bulge);
                    closest = ArcUtils.GetClosestPoint(referencePoint, position1, center, sweep);
                }
                candidates.Add(new GeoPoint(closest, GeoPointModes.NearestPoint));
            }

            if (!isLine && geoPointType.HasFlag(GeoPointModes.Center))
            {
                var center = BulgeUtils.GetCenterFromBulge(position1, position2, bulge);
                candidates.Add(new GeoPoint(center, GeoPointModes.Center));
            }

            if (!isLine && geoPointType.HasFlag(GeoPointModes.Quadrant))
            {
                var center = BulgeUtils.GetCenterFromBulge(position1, position2, bulge);
                double radius = BulgeUtils.GetRadiusFromBulge(position1, position2, bulge);
                double startAngle = Math.Atan2(position1.Y - center.Y, position1.X - center.X);
                double endAngle = Math.Atan2(position2.Y - center.Y, position2.X - center.X);

                foreach (var angle in AngleUtils.Cardinals)
                {
                    if (IsAngleInArcSegment(angle, startAngle, endAngle, bulge))
                    {
                        var qPoint = new Point3D(
                            center.X + radius * Math.Cos(angle),
                            center.Y + radius * Math.Sin(angle),
                            center.Z
                        );
                        candidates.Add(new GeoPoint(qPoint, GeoPointModes.Quadrant));
                    }
                }
            }

            return candidates;
        }

        public static IEnumerable<GeoPoint> GetSegmentGeoPoints(
            PolylineSegment seg,
            Point3D referencePoint,
            GeoPointModes modes)
        {
            var list = new List<GeoPoint>();

            // -----------------------------
            // 1. Vertex snaps
            // -----------------------------
            if (modes.HasFlag(GeoPointModes.Vertex))
            {
                list.Add(new GeoPoint(seg.Start, GeoPointModes.Vertex));
                list.Add(new GeoPoint(seg.End, GeoPointModes.Vertex));
            }

            // -----------------------------
            // 2. Midpoint snap
            // -----------------------------
            if (modes.HasFlag(GeoPointModes.Middle))
            {
                var mid = seg.GetPointAt(0.5);
                list.Add(new GeoPoint(mid, GeoPointModes.Middle));
            }

            // Perpendicular snap
            if (modes.HasFlag(GeoPointModes.Perpendicular))
            {
                var perp = seg.GetPerpendicularPoint(referencePoint);
                if (perp != null)
                    list.Add(new GeoPoint(perp.Value, GeoPointModes.Perpendicular));
            }

            // Nearest point snap
            if (modes.HasFlag(GeoPointModes.NearestPoint))
            {
                var nearest = seg.GetNearestPoint(referencePoint);
                list.Add(new GeoPoint(nearest, GeoPointModes.NearestPoint));
            }

            // -----------------------------
            // 3. Line-only snaps
            // -----------------------------
            if (seg.IsLine)
            {
                return list;
            }

            // -----------------------------
            // 4. Arc-only snaps
            // -----------------------------
            // Center snap
            if (modes.HasFlag(GeoPointModes.Center))
            {
                list.Add(new GeoPoint(seg.Center, GeoPointModes.Center));
            }

            // Quadrant snaps
            if (modes.HasFlag(GeoPointModes.Quadrant))
            {
                foreach (var q in seg.GetArcQuadrants())
                    list.Add(new GeoPoint(q, GeoPointModes.Quadrant));
            }

            // Tangent snap
            if (modes.HasFlag(GeoPointModes.Tangent))
            {
                foreach (var t in seg.GetArcTangents(referencePoint))
                    list.Add(new GeoPoint(t, GeoPointModes.Tangent));
            }

            return list;
        }

        // Internal helper used only by GetSegmentGeoPoints (keeps quadrant logic local).
        private static bool IsAngleInArcSegment(double testAngle, double startAngle, double endAngle, double bulge)
        {
            testAngle = AngleUtils.NormalizeUnsigned(testAngle);
            startAngle = AngleUtils.NormalizeUnsigned(startAngle);
            endAngle = AngleUtils.NormalizeUnsigned(endAngle);

            if (bulge > 0)
            {
                if (startAngle <= endAngle)
                    return testAngle >= startAngle && testAngle <= endAngle;
                return testAngle >= startAngle || testAngle <= endAngle;
            }
            else
            {
                if (startAngle >= endAngle)
                    return testAngle <= startAngle && testAngle >= endAngle;
                return testAngle <= startAngle || testAngle >= endAngle;
            }
        }

        // Angle normalization facade
        public static double NormalizeUnsigned(double angle) => AngleUtils.NormalizeUnsigned(angle);
    }
}
