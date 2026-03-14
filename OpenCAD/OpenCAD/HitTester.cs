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
        private readonly Func<int> _pickboxSizeProvider;
        private readonly Func<int> _gripSizeProvider;
        private readonly IGripProviderFactory _gripProviderFactory;
        private readonly ISelectionManager _selectionManager;
        private readonly ICamera _camera;

        public HitTester(
            OpenCADObject root,
            ISelectionManager selectionManager,
            IGripProviderFactory gripProviderFactory,
            ICamera camera,
            Func<int> pickboxSizeProvider,
            Func<int> gripSizeProvider)
        {
            _document = root;
            _selectionManager = selectionManager;
            _pickboxSizeProvider = pickboxSizeProvider;
            _gripSizeProvider = gripSizeProvider;
            _gripProviderFactory = gripProviderFactory;
            _camera = camera;
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
            var screenPos = _camera.WorldToScreen(new Vector3(mouseWorld.X, mouseWorld.Y, 0));
            return HitTestEntity(screenPos, _pickboxSizeProvider());
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
            var worldPos = _camera.ScreenToWorld(screenPos);
            if (curveObjects.Count < 1)
                return hitObjects;

            var c1 = _camera.ScreenToWorld(new Point(screenPos.X - boxSize, screenPos.Y - boxSize));
            var c2 = _camera.ScreenToWorld(new Point(screenPos.X + boxSize, screenPos.Y + boxSize));

            foreach (var obj in curveObjects)
            {
                if (obj is ICurve curve)
                {
                    var pt = curve.GetClosestPoint(
                        new Point3D(worldPos.X, worldPos.Y, 0));

                    if (pt.IsValid)
                    {
                        if (pt.X >= c1.X && pt.X <= c2.X &&
                            pt.Y <= c1.Y && pt.Y >= c2.Y)
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
            if (gripSizePx <= 0)
                return null;

            Grip? bestGrip = null;
            OpenCADObject? bestEntity = null;
            double bestDistSq = double.MaxValue;

            foreach (var obj in _selectionManager.SelectedObjects)
            {
                // Get provider from factory (new architecture)
                var provider = _gripProviderFactory.GetProvider(obj);
                if (provider == null)
                    continue;

                foreach (var grip in provider.GetGrips(obj))
                {
                    var world = new Vector3((float)grip.Position.X, (float)grip.Position.Y, 0f);
                    var screen = _camera.WorldToScreen(world);

                    var dx = screen.X - screenPos.X;
                    var dy = screen.Y - screenPos.Y;
                    var distSq = dx * dx + dy * dy;

                    var half = gripSizePx * 0.5;
                    if (Math.Abs(dx) <= half && Math.Abs(dy) <= half)
                    {
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestGrip = grip;
                            bestEntity = grip.Owner;
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
