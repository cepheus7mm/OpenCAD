using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers
{
    public class Extents
    {
        public Point3D Min { get; set; } = Point3D.NegativeInfinity;
        public Point3D Max { get; set; } = Point3D.PositiveInfinity;
    }
}
