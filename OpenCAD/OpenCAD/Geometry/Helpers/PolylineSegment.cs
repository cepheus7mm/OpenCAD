using OpenCAD.Geometry.Calculator;
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

        public double Bulge => IsLine ? 0.0 : Math.Tan(Sweep / 4.0);

        // Arc-only fields
        public Point3D Center { get; }
        public double Sweep { get; }     // signed sweep
        public double Radius { get; }

        // -------------------------
        // Constructor
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
    }
}
