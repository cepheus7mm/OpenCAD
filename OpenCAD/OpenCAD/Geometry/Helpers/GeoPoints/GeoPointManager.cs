using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints
{
    public class GeoPointManager : IGeoPointManager
    {
        private GeoPoint? _currentSnap = null;
        private IGeoPointProviderFactory _geoPointProviderFactory;
        private static readonly Dictionary<GeoPointModes, int> ModePriority = new()
        {
            { GeoPointModes.Vertex,          100 },
            { GeoPointModes.Middle,           90 },
            { GeoPointModes.Center,           80 },
            { GeoPointModes.Quadrant,         70 },
            { GeoPointModes.Tangent,          60 },
            { GeoPointModes.Perpendicular,    50 },
            { GeoPointModes.Intersection,     40 },
            { GeoPointModes.NearestPoint,     30 },
            { GeoPointModes.Point,            20 },
            { GeoPointModes.ExtensionSnap,    10 },
        };

        public GeoPointManager(IGeoPointProviderFactory geoPointProviderFactory)
        {
            _geoPointProviderFactory = geoPointProviderFactory;
        }

        public GeoPoint? CurrentSnap => _currentSnap;

        public void Clear()
        {
            _currentSnap = null;
        }

        public GeoPoint? GetBestGeoPoint(
            Point3D cursorWorld,
            IEnumerable<OpenCADObject> visibleObjects,
            GeoPointModes activeModes,
            double apertureWorld)
        {
            // Always clear first
            _currentSnap = null;

            GeoPoint? best = null;
            double bestScore = double.MaxValue;
            double apertureSq = apertureWorld * apertureWorld;

            foreach (var obj in visibleObjects)
            {
                var provider = _geoPointProviderFactory.GetProvider(obj);

                foreach (var gp in provider?.GetGeoPoints(obj, activeModes, cursorWorld) ?? Enumerable.Empty<GeoPoint>())
                {
                    double distSq = (gp.Position - cursorWorld).LengthSquared;
                    if (gp.PointType == GeoPointModes.Center && gp.Owner != null)
                    {
                        var curve = gp.Owner as ICurve;
                        var nearest = curve.GetClosestPoint(cursorWorld);
                        distSq = (nearest - cursorWorld).LengthSquared;
                    }

                    // Reject outside aperture
                    if (distSq > apertureSq)
                        continue;

                    double score = ComputeScore(gp, cursorWorld);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = gp;
                    }
                }
            }

            // Only set if we found something
            _currentSnap = best;
            return best;
        }

        double ComputeScore(GeoPoint gp, Point3D cursor)
        {
            int priority = ModePriority.TryGetValue(gp.PointType, out var p) ? p : 0;

            double dist = (gp.Position - cursor).LengthSquared;

            return (1000 - priority) * 1_000_000 + dist;
        }
    }
}
