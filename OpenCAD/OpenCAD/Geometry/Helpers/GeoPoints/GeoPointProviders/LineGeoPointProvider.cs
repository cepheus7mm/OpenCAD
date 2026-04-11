using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints.GeoPointProviders
{
    [GeoPointProvider(typeof(Line))]
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
            //Debug.WriteLine($"[LineGeoPointProvider] RefPt: {referencePoint}, Modes: {modes}");

            if (obj is not Line line)
            {
                //Debug.WriteLine($"[LineGeoPointProvider] Skipped: obj is {obj?.GetType().Name ?? "null"}, not a Line");
                yield break;
            }

            var refPt2D = new Vector2((float)referencePoint.X, (float)referencePoint.Y);
            var count = 0;
            foreach (var gp in GetGeoPoints(line, modes, refPt2D))
            {
                //Debug.WriteLine($"[LineGeoPointProvider] Found: {gp}");
                count++;
                yield return gp;
            }

            //Debug.WriteLine($"[LineGeoPointProvider] Total yielded: {count}");
        }
    }
}
