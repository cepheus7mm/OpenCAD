using OpenCAD.Geometry.Calculator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers
{
    public sealed class PolylineSegmentCache : Cache<PolylineSegment>
    {
        private readonly Polyline _polyline;

        public PolylineSegmentCache(Polyline polyline)
        {
            _polyline = polyline;
        }

        protected override List<PolylineSegment> Rebuild()
        {
            var verts = _polyline.Vertices.ToList();
            var bulges = _polyline.Bulges.ToList();

            var list = new List<PolylineSegment>(verts.Count());
            for (int i = 0; i < verts.Count() - 1; i++)
            {
                list.Add(CreateSegment(verts[i].Position, verts[i + 1].Position, bulges[i]));
            }

            if (_polyline.IsClosed)
            {
                list.Add(CreateSegment(verts[^1].Position, verts[0].Position, bulges[^1]));
            }

            return list;
        }

        private PolylineSegment CreateSegment(Point3D start, Point3D end, double bulge)
        {
            if (Math.Abs(bulge) < 1e-10)
            {
                return new PolylineSegment(start, end);
            }
            var center = GeometricCalculator.GetCenterFromBulge(start, end, bulge);
            var sweep = GeometricCalculator.GetAngleFromBulge(bulge);
            return new PolylineSegment(start, end, center, sweep);
        }

        public (PolylineSegment segment, int index, double localT) GetSegmentT(double t)
        {
            var segments = Items;
            int count = segments.Count;

            if (count == 0)
                return (default, 0, 0);

            // Clamp t
            if (t <= 0)
                return (segments[0], 0, 0);

            if (t >= count)
                return (segments[count - 1], count - 1, 1);

            int index = (int)Math.Floor(t);
            double localT = t - index;

            return (segments[index], index, localT);
        }
    }
}
