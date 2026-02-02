using OpenCAD.Geometry;
using OpenCAD.Grips;
using OpenCAD.Grips.GripProviders;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD
{
    public sealed class HitTester : IHitTester
    {
        private readonly OpenCADObject _document;
        private readonly Func<Point, Vector3?> _screenToWorld;
        private readonly Func<Vector3, Point?> _worldToScreen;
        private readonly Func<int> _pickboxSizeProvider;
        private readonly Func<int> _gripSizeProvider;
        private readonly IGripProviderFactory _gripProviderFactory;

        public HitTester(
            OpenCADObject root,
            IGripProviderFactory gripProviderFactory,
            Func<Point, Vector3?> screenToWorld,
            Func<Vector3, Point?> worldToScreen,
            Func<int> pickboxSizeProvider,
            Func<int> gripSizeProvider)
        {
            _document = root;
            _screenToWorld = screenToWorld;
            _worldToScreen = worldToScreen;
            _pickboxSizeProvider = pickboxSizeProvider;
            _gripSizeProvider = gripSizeProvider;
            _gripProviderFactory = gripProviderFactory;
        }

        // ------------------------------------------------------------
        // Unified high-level hit test
        // ------------------------------------------------------------
        public HitResult HitTest(Point screenPos)
        {
            // 1. Try grip first
            var gripHit = HitTestGrip(screenPos, _gripSizeProvider());
            if (gripHit != null)
                return gripHit.Value;

            // 2. Fall back to entity
            var entity = HitTestEntity(screenPos, _pickboxSizeProvider());
            if (entity != null)
                return HitResult.FromEntity(entity);

            return HitResult.None();
        }

        // ------------------------------------------------------------
        // Entity hit test (your existing logic, refactored)
        // ------------------------------------------------------------
        public OpenCADObject? HitTestEntity(Vector2 mouseWorld)
        {
            var screenPos = _worldToScreen(new Vector3(mouseWorld.X, mouseWorld.Y, 0));
            if (screenPos is null)
                return null;
            return HitTestEntity(screenPos.Value, _pickboxSizeProvider());
        }

        public OpenCADObject? HitTestEntity(Point screenPos, int boxSize)
        {
            return HitTestEntities(screenPos, boxSize).FirstOrDefault();
        }

        public IEnumerable<OpenCADObject> HitTestEntities(Point screenPos, int boxSize)
        {
            var curveObjects = new List<OpenCADObject>();
            CollectCurveObjects(_document, curveObjects);

            var hitObjects = new List<OpenCADObject>();
            var worldPos = _screenToWorld(screenPos);
            if (worldPos is null || curveObjects.Count < 1)
                return hitObjects;

            var c1 = _screenToWorld(new Point(screenPos.X - boxSize, screenPos.Y - boxSize));
            var c2 = _screenToWorld(new Point(screenPos.X + boxSize, screenPos.Y + boxSize));

            if (c1 is null || c2 is null)
                return hitObjects;

            foreach (var obj in curveObjects)
            {
                if (obj is ICurve curve)
                {
                    var pt = curve.GetClosestPoint(
                        new Point3D(worldPos.Value.X, worldPos.Value.Y, 0));

                    if (pt.IsValid)
                    {
                        if (pt.X >= c1.Value.X && pt.X <= c2.Value.X &&
                            pt.Y <= c1.Value.Y && pt.Y >= c2.Value.Y)
                        {
                            hitObjects.Add(obj);
                        }
                    }
                }
            }

            return hitObjects;
        }

        private void CollectCurveObjects(OpenCADObject parent, List<OpenCADObject> list)
        {
            var children = parent.GetChildren();
            foreach (var child in children)
            {
                if (child is ICurve)
                    list.Add(child);

                CollectCurveObjects(child, list);
            }
        }

        // ------------------------------------------------------------
        // Grip hit test (screen-space)
        // ------------------------------------------------------------
        public HitResult? HitTestGrip(Point screenPos, double gripSizePx)
        {
            var drawable = new List<OpenCADObject>();
            CollectDrawableObjects(_document, drawable);

            Grip? bestGrip = null;
            OpenCADObject? bestEntity = null;
            double bestDistSq = double.MaxValue;

            foreach (var obj in drawable)
            {
                // Get provider from factory (new architecture)
                var provider = _gripProviderFactory.GetProvider(obj);
                if (provider == null)
                    continue;

                foreach (var grip in provider.GetGrips(obj))
                {
                    var world = new Vector3((float)grip.Position.X, (float)grip.Position.Y, 0f);
                    var screen = _worldToScreen(world);
                    if (screen is null)
                        continue;

                    var dx = screen.Value.X - screenPos.X;
                    var dy = screen.Value.Y - screenPos.Y;
                    var distSq = dx * dx + dy * dy;

                    var half = gripSizePx * 0.5;
                    if (Math.Abs(dx) <= half && Math.Abs(dy) <= half)
                    {
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestGrip = grip;
                            bestEntity = obj;
                        }
                    }
                }
            }

            if (bestGrip != null && bestEntity != null)
                return HitResult.FromGrip(bestEntity, bestGrip.Value);

            return null;
        }

        private void CollectDrawableObjects(OpenCADObject parent, List<OpenCADObject> list)
        {
            var children = parent.GetChildren();
            foreach (var child in children)
            {
                if (child.IsDrawable)
                    list.Add(child);

                CollectDrawableObjects(child, list);
            }
        }
    }
}
