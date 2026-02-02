using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers.GeoPoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers
{
    public readonly struct PolylineSegment
    {
        public bool IsLine { get; }
        public bool IsArc => !IsLine;

        public Point3D Start { get; }
        public Point3D End { get; }

        public double StartAngle
        {
            get
            {
                if (IsLine)
                    return double.NaN;

                return AngleUtils.NormalizeUnsigned(
                    Math.Atan2(Start.Y - Center.Y, Start.X - Center.X));
            }
        }

        public double EndAngle
        {
            get
            {
                if (IsLine)
                    return double.NaN;

                return AngleUtils.NormalizeUnsigned(StartAngle + Sweep);
            }
        }

        public double Bulge => IsLine ? 0.0 : Math.Tan(Sweep / 4.0);

        // Arc-only fields
        public Point3D Center { get; }
        public double Sweep { get; }     // signed sweep
        public double Radius { get; }

        // -------------------------
        // Constructors
        // -------------------------

        public PolylineSegment(Point3D start, Point3D end, Point3D? center = null, double sweep = 0)
        {
            Start = start;
            End = end;

            if (center is null || Math.Abs(sweep) < 1e-12)
            {
                // Line segment
                IsLine = true;
                Center = default;
                Sweep = 0;
                Radius = 0;
            }
            else
            {
                // Arc segment
                IsLine = false;
                Center = center.Value;
                Sweep = sweep;
                Radius = (start - center.Value).Length;
            }
        }

        public static PolylineSegment FromLine(Line line)
        {
            return new PolylineSegment(
                start: line.Start,
                end: line.End
            );
        }

        public static PolylineSegment FromArc(Arc arc)
        {
            return new PolylineSegment(
                start: arc.Start,
                end: arc.End,
                center: arc.Center,
                sweep: arc.Angle
            );
        }

        public static PolylineSegment FromCircle(Circle circle)
        {
            // Pick angle 0 as the canonical start point
            var start = new Point3D(
                circle.Center.X + circle.Radius,
                circle.Center.Y,
                circle.Center.Z
            );

            return new PolylineSegment(
                start: start,
                end: start,               // same point, but sweep defines full circle
                center: circle.Center,
                sweep: AngleUtils.TwoPi   // full 360° arc
            );
        }

        // -------------------------
        // Derived geometry
        // -------------------------

        public Point3D GetPointAt(double t)
        {
            if (IsLine)
                return Start + (End - Start) * t;

            double a0 = Math.Atan2(Start.Y - Center.Y, Start.X - Center.X);
            double angle = a0 + t * Sweep;

            return new Point3D(
                Center.X + Radius * Math.Cos(angle),
                Center.Y + Radius * Math.Sin(angle),
                Start.Z);
        }

        public double GetLength()
        {
            if (IsLine)
                return (End - Start).Length;

            return Radius * Math.Abs(Sweep);
        }

        public double GetLength(double t0, double t1)
        {
            if (IsLine)
                return (End - Start).Length * Math.Abs(t1 - t0);

            return Radius * Math.Abs(Sweep) * Math.Abs(t1 - t0);
        }

        public double GetClosestParameter(Point3D point, bool extend = false)
        {
            if (IsLine)
                return GeometricCalculator.GetClosestParameter(point, Start, End, extend);

            return GeometricCalculator.GetClosestParameter(point, Start, Center, Sweep, extend);
        }

        public bool IsPointOnSegment(Point3D point, double tolerance)
        {
            if (IsLine)
                return GeometricCalculator.IsPointOnLine(point, Start, End, tolerance);

            return GeometricCalculator.IsPointOnArc(point, Start, Center, Sweep, tolerance);
        }

        public Vector3D GetFirstDerivative(double localT)
        {
            // Line segment derivative
            if (IsLine)
                return GeometricCalculator.GetFirstDerivative(localT, Start, End);

            // Arc segment derivative
            return GeometricCalculator.GetFirstDerivative(localT, Start, Center, Sweep);
        }

        public Vector3D GetSecondDerivative(double localT)
        {
            // Line segment derivative
            if (IsLine)
                return GeometricCalculator.GetSecondDerivative(localT, Start, End);

            // Arc segment derivative
            return GeometricCalculator.GetSecondDerivative(localT, Start, Center, Sweep);
        }
        public PolylineSegment Trim(double t0, double t1)
        {
            if (IsLine)
            {
                Point3D p0 = GetPointAt(t0);
                Point3D p1 = GetPointAt(t1);
                return new PolylineSegment(p0, p1);
            }
            else
            {
                // Arc
                double a0 = Math.Atan2(Start.Y - Center.Y, Start.X - Center.X);
                double angle0 = a0 + t0 * Sweep;
                double angle1 = a0 + t1 * Sweep;

                Point3D p0 = new Point3D(
                    Center.X + Radius * Math.Cos(angle0),
                    Center.Y + Radius * Math.Sin(angle0),
                    Start.Z);

                Point3D p1 = new Point3D(
                    Center.X + Radius * Math.Cos(angle1),
                    Center.Y + Radius * Math.Sin(angle1),
                    Start.Z);

                double newSweep = (t1 - t0) * Sweep;

                return new PolylineSegment(p0, p1, Center, newSweep);
            }
        }

        public PolylineSegment Transform(Matrix4D m)
        {
            Point3D s = Start.Transform(m);
            Point3D e = End.Transform(m);

            if (IsLine)
                return new PolylineSegment(s, e);

            Point3D c = Center.Transform(m);
            return new PolylineSegment(s, e, c, Sweep);
        }

        public Extents GetExtents()
        {
            return IsLine
                ? new Extents(Start, End)
                : Extents.FromArc(Start, End, Center, Sweep);
        }

        //
        // GeoPoint Helpers
        //

        public IEnumerable<GeoPoint> GetGeoPoints(Point3D referencePoint, GeoPointModes modes)
        {
            var list = new List<GeoPoint>();

            // -----------------------------
            // 1. Vertex snaps
            // -----------------------------
            if (modes.HasFlag(GeoPointModes.Vertex))
            {
                list.Add(new GeoPoint(Start, GeoPointModes.Vertex));
                list.Add(new GeoPoint(End, GeoPointModes.Vertex));
            }

            // -----------------------------
            // 2. Midpoint snap
            // -----------------------------
            if (modes.HasFlag(GeoPointModes.Middle))
            {
                var mid = GetPointAt(0.5);
                list.Add(new GeoPoint(mid, GeoPointModes.Middle));
            }

            // -----------------------------
            // 3. Perpendicular snap
            // -----------------------------
            if (modes.HasFlag(GeoPointModes.Perpendicular))
            {
                var perp = GetPerpendicularPoint(referencePoint);
                if (perp != null)
                    list.Add(new GeoPoint(perp.Value, GeoPointModes.Perpendicular));
            }

            // -----------------------------
            // 4. Nearest point snap
            // -----------------------------
            if (modes.HasFlag(GeoPointModes.NearestPoint))
            {
                var nearest = GetNearestPoint(referencePoint);
                list.Add(new GeoPoint(nearest, GeoPointModes.NearestPoint));
            }

            // -----------------------------
            // 5. Line-only snaps end here
            // -----------------------------
            if (IsLine)
                return list;

            // -----------------------------
            // 6. Arc-only snaps
            // -----------------------------
            if (modes.HasFlag(GeoPointModes.Center))
            {
                list.Add(new GeoPoint(Center, GeoPointModes.Center));
            }

            if (modes.HasFlag(GeoPointModes.Quadrant))
            {
                foreach (var q in GetArcQuadrants())
                    list.Add(new GeoPoint(q, GeoPointModes.Quadrant));
            }

            if (modes.HasFlag(GeoPointModes.Tangent))
            {
                foreach (var t in GetArcTangents(referencePoint))
                    list.Add(new GeoPoint(t, GeoPointModes.Tangent));
            }

            return list;
        }

        public Point3D? GetPerpendicularPoint(Point3D referencePoint)
        {
            return IsLine
                ? GetPerpendicularPointLine(referencePoint)
                : GetPerpendicularPointArc(referencePoint);
        }

        private Point3D? GetPerpendicularPointLine(Point3D referencePoint)
        {
            var s = Start;
            var e = End;

            var seg = e - s;
            var v = referencePoint - s;

            double lenSq = seg.LengthSquared;
            if (lenSq < double.Epsilon)
                return null; // degenerate segment

            double t = Vector3D.Dot(v, seg) / lenSq;

            if (t < 0.0 || t > 1.0)
                return null;

            return s + seg * t;
        }

        private Point3D? GetPerpendicularPointArc(Point3D referencePoint)
        {
            // Vector from center to reference point
            var v = referencePoint - Center;

            if (v.LengthSquared < double.Epsilon)
                return null; // reference point is at center

            // Angle of reference point relative to arc center
            double angle = Math.Atan2(v.Y, v.X);

            // Clamp to arc sweep
            angle = AngleUtils.ClampAngleToSweep(StartAngle, Sweep, angle);

            // Return point on arc
            return new Point3D(
                Center.X + Radius * Math.Cos(angle),
                Center.Y + Radius * Math.Sin(angle),
                Start.Z);
        }

        public Point3D GetNearestPoint(Point3D referencePoint)
        {
            return IsLine
                ? GetNearestPointLine(referencePoint)
                : GetNearestPointArc(referencePoint);
        }

        private Point3D GetNearestPointLine(Point3D referencePoint)
        {
            var s = Start;
            var e = End;

            var seg = e - s;
            var v = referencePoint - s;

            double lenSq = seg.LengthSquared;
            if (lenSq < double.Epsilon)
                return s; // degenerate segment

            double t = Vector3D.Dot(v, seg) / lenSq;
            t = Math.Clamp(t, 0.0, 1.0);

            return s + seg * t;
        }

        private Point3D GetNearestPointArc(Point3D referencePoint)
        {
            // Vector from center to reference point
            var v = referencePoint - Center;

            // If reference point is at center, nearest point is arc start
            if (v.LengthSquared < double.Epsilon)
                return Start;

            // Angle of reference point relative to arc center
            double angle = Math.Atan2(v.Y, v.X);

            // Clamp to arc sweep
            angle = AngleUtils.ClampAngleToSweep(StartAngle, Sweep, angle);

            // Return point on arc
            return new Point3D(
                Center.X + Radius * Math.Cos(angle),
                Center.Y + Radius * Math.Sin(angle),
                Start.Z);
        }

        public IEnumerable<Point3D> GetArcQuadrants()
        {
            if (IsLine)
                yield break;

            foreach (double qAngle in AngleUtils.Cardinals)
            {
                if (AngleUtils.SweepContains(StartAngle, Sweep, qAngle))
                {
                    yield return new Point3D(
                        Center.X + Radius * Math.Cos(qAngle),
                        Center.Y + Radius * Math.Sin(qAngle),
                        Start.Z
                    );
                }
            }
        }

        public IEnumerable<Point3D> GetArcTangents(Point3D referencePoint)
        {
            if (IsLine)
                yield break;

            var C = Center;
            var P = referencePoint;

            var v = P - C;
            double d2 = v.LengthSquared;
            double R = Radius;
            double R2 = R * R;

            // No tangents if reference point is inside the circle
            if (d2 < R2 - 1e-12)
                yield break;

            double d = Math.Sqrt(d2);

            // Angle from center to reference point
            double baseAngle = Math.Atan2(v.Y, v.X);

            // If point is exactly on the circle → one tangent (degenerate)
            if (Math.Abs(d - R) < 1e-12)
            {
                double angle = AngleUtils.ClampAngleToSweep(StartAngle, Sweep, baseAngle);
                if (AngleUtils.SweepContains(StartAngle, Sweep, angle))
                {
                    yield return new Point3D(
                        C.X + R * Math.Cos(angle),
                        C.Y + R * Math.Sin(angle),
                        Start.Z
                    );
                }
                yield break;
            }

            // General case: two tangents
            double alpha = Math.Acos(R / d); // tangent offset angle

            double t1 = baseAngle + alpha;
            double t2 = baseAngle - alpha;

            // Normalize
            t1 = AngleUtils.NormalizeUnsigned(t1);
            t2 = AngleUtils.NormalizeUnsigned(t2);

            // Clamp to arc sweep
            t1 = AngleUtils.ClampAngleToSweep(StartAngle, Sweep, t1);
            t2 = AngleUtils.ClampAngleToSweep(StartAngle, Sweep, t2);

            // Emit only those that lie on the arc
            if (AngleUtils.SweepContains(StartAngle, Sweep, t1))
            {
                yield return new Point3D(
                    C.X + R * Math.Cos(t1),
                    C.Y + R * Math.Sin(t1),
                    Start.Z
                );
            }

            if (AngleUtils.SweepContains(StartAngle, Sweep, t2))
            {
                yield return new Point3D(
                    C.X + R * Math.Cos(t2),
                    C.Y + R * Math.Sin(t2),
                    Start.Z
                );
            }
        }
    }
}
