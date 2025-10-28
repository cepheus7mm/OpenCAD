using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry
{
    public class Line : GeometryBase
    {
        public Line(Point3D start, Point3D end)
        {
            properties.TryAdd(0, new Property(PropertyType.PointStart, start));
            properties.TryAdd(1, new Property(PropertyType.PointEnd, end));
        }

        public Point3D Start => (Point3D)properties[0].Value;
        public Point3D End => (Point3D)properties[1].Value;

        public double Length => Start.DistanceTo(End);
    }
}
