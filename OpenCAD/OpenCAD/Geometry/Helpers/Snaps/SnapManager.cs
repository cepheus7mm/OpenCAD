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
        private HashSet<OpenCADObject> _excludedObjects = new();
        private readonly ICamera _camera;

        private SnapPoint? _currentObjectSnap;
        private Vector2? _forcedSnap;

        public SnapManager(
            IGeoPointProviderFactory providerFactory,
            ICamera camera,
            Func<IEnumerable<OpenCADObject>> visibleObjectsAccessor)
        {
            _providerFactory = providerFactory;
            _visibleObjectsAccessor = visibleObjectsAccessor;
            _camera = camera;
        }

        // ------------------------------------------------------------
        // SNAP MODE STATE
        // ------------------------------------------------------------

        public bool GridSnapEnabled { get; set; }
        public bool OrthoEnabled { get; set; }
        public bool PolarTrackingEnabled { get; set; }
        public double GridSize { get; set; } = 1.0;
        public double ApertureSize { get; set; } = 15.0;
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

        public Vector2 GetFinalSnapPoint(Vector2 rawMouseWorld, bool applyCursorSnap = true, bool applyGeoSnap = true)
        {
            // 1. Forced snap overrides everything
            if (_forcedSnap.HasValue)
            {
                UpdateCurrentObjectSnap(null);
                return _forcedSnap.Value;
            }

            // 2. Apply cursor snapping (grid, ortho, polar)
            var cursorSnapped = applyCursorSnap ? ApplyCursorSnap(rawMouseWorld) : rawMouseWorld;

            // 3. Compute object snap
            SnapPoint? osnap = null;
            if (applyGeoSnap)
            {
                ComputeObjectSnap(cursorSnapped);

                UpdateCurrentObjectSnap(osnap); 
            }

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

            // Convert cursor to screen space once
            var cursorScreen = _camera.WorldToScreen(new Vector3(cursorPos.X, cursorPos.Y, 0f));

            foreach (var obj in _visibleObjectsAccessor())
            {
                if (_excludedObjects.Contains(obj) || obj == null)
                    continue;

                var provider = _providerFactory.GetProvider(obj);
                if (provider == null)
                    continue;

                var geoPoints = provider.GetGeoPoints(obj, EnabledObjectSnaps, new Point3D(cursorPos));

                foreach (var gp in geoPoints)
                {
                    var world = new Vector2((float)gp.Position.X, (float)gp.Position.Y);
                    var screen = _camera.WorldToScreen(new Vector3(world.X, world.Y, 0f));

                    // Pixel distance
                    var dx = screen.X - cursorScreen.X;
                    var dy = screen.Y - cursorScreen.Y;
                    var pixelDistSq = dx * dx + dy * dy;

                    // Reject if outside aperture
                    if (pixelDistSq > ApertureSize * ApertureSize)
                        continue;

                    // Score by world distance (or screen distance if you prefer)
                    var dist = Vector2.Distance(cursorPos, world);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = new SnapPoint(world, gp.PointType, obj);
                    }
                }
            }

            return best;
        }

        public void SetSnapExclusions(IEnumerable<OpenCADObject> objects)
        {
            _excludedObjects = new HashSet<OpenCADObject>(objects);
        }

        public void ClearSnapExclusions()
        {
            _excludedObjects.Clear();
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
