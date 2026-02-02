using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints.GeoPointProviders
{
    public sealed class LineGeoPointProvider : IGeoPointProvider
    {
        public IEnumerable<GeoPoint> GetGeoPoints(Line line, GeoPointModes modes, Vector2 refPt)
        {
            var seg = PolylineSegment.FromLine(line);
            return seg.GetGeoPoints(new Point3D(refPt.X, refPt.Y, 0), modes)
                      .Select(gp => gp.WithOwner(line));
        }

        public IEnumerable<GeoPoint> GetGeoPoints(OpenCADObject obj, GeoPointModes modes, Point3D referencePoint)
        {
            if (obj is not Line line)
                yield break;
            var refPt2D = new Vector2((float)referencePoint.X, (float)referencePoint.Y);
            foreach (var gp in GetGeoPoints(line, modes, refPt2D))
            {
                yield return gp;
            }
        }
    }
}
