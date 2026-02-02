using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints
{
    public class GeoPoint
    {
        public OpenCADObject? Owner { get; private set; } = null;
        public Point3D Position { get; private set; }

        public GeoPointModes PointType { get; set; } = GeoPointModes.None;
        public Guid RelatedGeometryId { get; set; } = Guid.Empty;
        public bool IsDelayed { get; set; } = false;

        public GeoPoint(Point3D position, GeoPointModes pointType = GeoPointModes.None)
        {
            Position = position;
            PointType = pointType;
        }

        public GeoPoint(double x, double y, double z, GeoPointModes pointType = GeoPointModes.None)
            : this(new Point3D(x, y, z), pointType)
        {
        }

        public override string ToString() => $"{Position} [{PointType}]";

        public GeoPoint WithOwner(OpenCADObject owner)
        {
            this.Owner = owner;
            return this;
        }
    }
}
