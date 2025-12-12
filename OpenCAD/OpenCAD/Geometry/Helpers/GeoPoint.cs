using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers
{
    public class GeoPoint : Point3D
    {
        public GeoPointModes PointType { get; set; } = GeoPointModes.None;
        public Guid RelatedGeometryId { get; set; } = Guid.Empty;
        public bool IsDelayed { get; set; } = false;
        public GeoPoint() : base() { }
        public GeoPoint(double x, double y, double z, GeoPointModes pointType = GeoPointModes.None)
            : base(x, y, z)
        {
            PointType = pointType;
        }
        public GeoPoint(Point3D point, GeoPointModes pointType = GeoPointModes.None)
            : base(point.X, point.Y, point.Z)
        {
            PointType = pointType;
        }
    }
}
