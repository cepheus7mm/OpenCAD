using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints.GeoPointProviders
{
    [GeoPointProvider(typeof(Arc))]
    public sealed class ArcGeoPointProvider : IGeoPointProvider
    {
        public IEnumerable<GeoPoint> GetGeoPoints(Arc arc, GeoPointModes modes, Vector2 refPt)
        {
            var seg = PolylineSegment.FromArc(arc);
            return seg.GetGeoPoints(new Point3D(refPt.X, refPt.Y, 0), modes)
                      .Select(gp => gp.WithOwner(arc));
        }

        public IEnumerable<GeoPoint> GetGeoPoints(OpenCADObject obj, GeoPointModes modes, Point3D referencePoint)
        {
            if (obj is not Arc arc)
                yield break;
            var refPt2D = new Vector2((float)referencePoint.X, (float)referencePoint.Y);
            foreach (var gp in GetGeoPoints(arc, modes, refPt2D))
            {
                yield return gp;
            }
        }
    }
}
