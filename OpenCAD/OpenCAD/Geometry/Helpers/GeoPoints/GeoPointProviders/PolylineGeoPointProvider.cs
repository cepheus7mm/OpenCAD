using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints.GeoPointProviders
{
    public sealed class PolylineGeoPointProvider : IGeoPointProvider
    {
        public IEnumerable<GeoPoint> GetGeoPoints(
            OpenCADObject owner,
            GeoPointModes modes,
            Point3D referencePoint)
        {
            if (owner is not Polyline pl)
                yield break;

            // Preview geometry should not provide osnaps
            if (pl.IsPreviewGeometry)
                yield break;

            var results = new List<GeoPoint>();

            var segments = pl.PolylineSegments;

            foreach ( var segment in segments )
            {
                results.AddRange(segment.GetGeoPoints(referencePoint, modes));
            }

            yield return (GeoPoint)results.Select(gp => gp.WithOwner(pl));
        }

        public IEnumerable<GeoPoint> GetGeoPoints(GeoPointModes modes, Point3D referencePoint)
        {
            throw new NotImplementedException();
        }
    }
}
