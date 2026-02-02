using OpenCAD.Geometry.Helpers.GeoPoints;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.Snaps
{
    public sealed class SnapManager : ISnapManager
    {
        private readonly IGeoPointProviderFactory _providerFactory;
        private readonly Func<IEnumerable<OpenCADObject>> _visibleObjectsAccessor;

        private SnapPoint? _currentObjectSnap;
        private Vector2? _forcedSnap;

        public SnapManager(
            IGeoPointProviderFactory providerFactory,
            Func<IEnumerable<OpenCADObject>> visibleObjectsAccessor)
        {
            _providerFactory = providerFactory;
            _visibleObjectsAccessor = visibleObjectsAccessor;
        }

        // ------------------------------------------------------------
        // SNAP MODE STATE
        // ------------------------------------------------------------

        public bool GridSnapEnabled { get; set; }
        public bool OrthoEnabled { get; set; }
        public bool PolarTrackingEnabled { get; set; }
        public double GridSize { get; set; } = 1.0;
        public IReadOnlyList<double> PolarAngles { get; private set; } = new[] { 0.0, 90.0 };

        public GeoPointModes EnabledObjectSnaps { get; set; } =
            GeoPointModes.Vertex |
            GeoPointModes.Middle |
            GeoPointModes.Center |
            GeoPointModes.Quadrant |
            GeoPointModes.Perpendicular |
            GeoPointModes.Tangent |
            GeoPointModes.NearestPoint;

        // ------------------------------------------------------------
        // FORCED SNAP (e.g., grip hover)
        // ------------------------------------------------------------

        public void SetForcedSnap(Vector2 position) => _forcedSnap = position;
        public void ClearForcedSnap() => _forcedSnap = null;
        public bool HasForcedSnap => _forcedSnap.HasValue;

        // ------------------------------------------------------------
        // MAIN ENTRY POINT
        // ------------------------------------------------------------

        public Vector2 GetFinalSnapPoint(Vector2 rawMouseWorld)
        {
            // 1. Forced snap overrides everything
            if (_forcedSnap.HasValue)
            {
                UpdateCurrentObjectSnap(null);
                return _forcedSnap.Value;
            }

            // 2. Apply cursor snapping (grid, ortho, polar)
            var cursorSnapped = ApplyCursorSnap(rawMouseWorld);

            // 3. Compute object snap
            var osnap = ComputeObjectSnap(cursorSnapped);

            UpdateCurrentObjectSnap(osnap);

            return osnap?.Position ?? cursorSnapped;
        }

        // ------------------------------------------------------------
        // CURSOR SNAP ENGINE
        // ------------------------------------------------------------

        public Vector2 ApplyCursorSnap(Vector2 raw)
        {
            var result = raw;

            // Grid snap
            if (GridSnapEnabled)
            {
                result = new Vector2(
                    (float)(Math.Round(result.X / GridSize) * GridSize),
                    (float)(Math.Round(result.Y / GridSize) * GridSize)
                );
            }

            // Ortho snap
            if (OrthoEnabled)
            {
                var dx = Math.Abs(result.X - raw.X);
                var dy = Math.Abs(result.Y - raw.Y);

                if (dx > dy)
                    result = new Vector2(result.X, raw.Y);
                else
                    result = new Vector2(raw.X, result.Y);
            }

            // Polar tracking (simplified)
            if (PolarTrackingEnabled)
            {
                var v = result - raw;
                var angle = Math.Atan2(v.Y, v.X) * 180.0 / Math.PI;

                foreach (var a in PolarAngles)
                {
                    if (Math.Abs(NormalizeAngle(angle - a)) < 5.0)
                    {
                        var len = v.Length();
                        var rad = a * Math.PI / 180.0;
                        result = raw + new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad)) * len;
                        break;
                    }
                }
            }

            return result;
        }

        private static double NormalizeAngle(double a)
        {
            while (a < -180) a += 360;
            while (a > 180) a -= 360;
            return a;
        }

        // ------------------------------------------------------------
        // OBJECT SNAP ENGINE
        // ------------------------------------------------------------

        public SnapPoint? ComputeObjectSnap(Vector2 cursorPos)
        {
            SnapPoint? best = null;
            double bestDist = double.MaxValue;

            foreach (var obj in _visibleObjectsAccessor())
            {
                var provider = _providerFactory.GetProvider(obj);
                if (provider == null)
                    continue;

                var geoPoints = provider.GetGeoPoints(obj, EnabledObjectSnaps, new Point3D(cursorPos));

                foreach (var gp in geoPoints)
                {
                    var pos = new Vector2((float)gp.Position.X, (float)gp.Position.Y);
                    var dist = Vector2.Distance(cursorPos, pos);

                    // Simple scoring: closest wins
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = new SnapPoint(pos, gp.PointType, obj);
                    }
                }
            }

            return best;
        }

        // ------------------------------------------------------------
        // SNAP MARKER STATE
        // ------------------------------------------------------------

        public SnapPoint? CurrentObjectSnap => _currentObjectSnap;

        public event Action<SnapPoint?>? SnapPointChanged;

        private void UpdateCurrentObjectSnap(SnapPoint? newSnap)
        {
            if (_currentObjectSnap == newSnap)
                return;

            _currentObjectSnap = newSnap;
            SnapPointChanged?.Invoke(newSnap);
        }
    }
}
