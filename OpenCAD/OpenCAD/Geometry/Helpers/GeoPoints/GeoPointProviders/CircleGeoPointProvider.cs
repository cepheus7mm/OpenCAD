using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints.GeoPointProviders
{
    public sealed class CircleGeoPointProvider : IGeoPointProvider
    {
        public IEnumerable<GeoPoint> GetGeoPoints(Circle circle, GeoPointModes modes, Vector2 refPt)
        {
            var seg = PolylineSegment.FromCircle(circle);
            return seg.GetGeoPoints(new Point3D(refPt.X, refPt.Y, 0), modes)
                      .Select(gp => gp.WithOwner(circle));
        }

        public IEnumerable<GeoPoint> GetGeoPoints(OpenCADObject obj, GeoPointModes modes, Point3D referencePoint)
        {
            if (obj is Circle circle)
            {
                return GetGeoPoints(circle, modes, new Vector2((float)referencePoint.X, (float)referencePoint.Y));
            }
            return Enumerable.Empty<GeoPoint>();
        }
    }
}
