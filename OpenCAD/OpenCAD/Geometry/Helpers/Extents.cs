using OpenCAD.Geometry.Calculator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers
{
    public struct Extents
    {
        public Point3D Min { get; private set; }
        public Point3D Max { get; private set; }

        public Extents(Point3D p1, Point3D p2)
        {
            Min = new Point3D(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Min(p1.Z, p2.Z));

            Max = new Point3D(
                Math.Max(p1.X, p2.X),
                Math.Max(p1.Y, p2.Y),
                Math.Max(p1.Z, p2.Z));
        }

        public void Union(Point3D p)
        {
            Min = new Point3D(
                Math.Min(Min.X, p.X),
                Math.Min(Min.Y, p.Y),
                Math.Min(Min.Z, p.Z));

            Max = new Point3D(
                Math.Max(Max.X, p.X),
                Math.Max(Max.Y, p.Y),
                Math.Max(Max.Z, p.Z));
        }

        public void Union(Extents e)
        {
            Union(e.Min);
            Union(e.Max);
        }

        public static Extents FromArc(Point3D start, Point3D end, Point3D center, double sweep)
        {
            // Start with endpoints
            var ext = new Extents(start, end);

            double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double endAngle = startAngle + sweep;

            startAngle = AngleUtils.NormalizeUnsigned(startAngle);

            double radius = (start - center).Length;

            foreach (double a in AngleUtils.Cardinals)
            {
                if (!AngleUtils.SweepContains(startAngle, sweep, a))
                    continue;

                var p = new Point3D(
                    center.X + radius * Math.Cos(a),
                    center.Y + radius * Math.Sin(a),
                    start.Z);

                ext.Union(p);
            }

            return ext;
        }
    }
}
