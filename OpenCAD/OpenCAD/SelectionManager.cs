using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD
{
    public sealed class SelectionManager : ISelectionManager
    {
        private readonly HashSet<OpenCADObject> _selected = new();
        private readonly HashSet<OpenCADObject> _previewObjects = new();
        private readonly OpenCADObject _rootObject;
        public IReadOnlyCollection<OpenCADObject> SelectedObjects => _selected;
        public IReadOnlyCollection<OpenCADObject> PreviewObjects => _previewObjects;
        public event EventHandler? SelectionChanged;
        public event EventHandler? SelectionPreviewChanged;
        public event EventHandler? ObjectSelected;

        private Point3D? _windowStart;
        private Point3D? _windowCurrent;

        public Point3D? WindowSelectionStart => _windowStart;
        public Point3D? WindowSelectionCurrent => _windowCurrent;

        public SelectionManager(OpenCADObject rootObject)
        {
            _rootObject = rootObject;
        }


        public void SelectSingle(OpenCADObject obj)
        {
            ClearSelection();
            if (_selected.Add(obj))
                SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddToSelection(OpenCADObject obj)
        {
            if (_selected.Add(obj))
                SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddToSelection(List<OpenCADObject> objects)
        {
            var addedAny = false;
            foreach (var obj in objects)
                addedAny |= _selected.Add(obj);

            if (addedAny)
                SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleSelection(OpenCADObject obj)
        {
            if (_selected.Contains(obj))
            {
                Deselect(obj);
            }
            else if (_selected.Add(obj))
                SelectionChanged?.Invoke(this, EventArgs.Empty);
       }

        public void Deselect(OpenCADObject obj)
        {
            if(_selected.Remove(obj))
                SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearSelection()
        {
            if (!_selected.Any())
                return;
            _selected.Clear();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void BeginWindowSelection(Point3D startWorld)
        {
            _windowStart = startWorld;
            _windowCurrent = startWorld;
        }

        public void CommitWindowSelection()
        {
            if (_windowStart == null || _windowCurrent == null)
                return;

            foreach (var obj in _previewObjects)
                _selected.Add(obj);

            _windowStart = null;
            _windowCurrent = null;
            _previewObjects.Clear();

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateWindowSelection(Point3D currentWorld)
        {
            _windowCurrent = currentWorld;
            _previewObjects.Clear();

            if (_windowStart == null)
                return;

            double minX = Math.Min(_windowStart.Value.X, _windowCurrent.Value.X);
            double maxX = Math.Max(_windowStart.Value.X, _windowCurrent.Value.X);
            double minY = Math.Min(_windowStart.Value.Y, _windowCurrent.Value.Y);
            double maxY = Math.Max(_windowStart.Value.Y, _windowCurrent.Value.Y);

            var drawableObjects = new List<OpenCADObject>();
            CollectDrawableObjects(_rootObject, drawableObjects);

            foreach (var obj in drawableObjects)
            {
                if (IsObjectInsideRectangle(obj, minX, maxX, minY, maxY))
                    _previewObjects.Add(obj);
            }

            SelectionPreviewChanged?.Invoke(this, EventArgs.Empty);
        }
        public void CancelWindowSelection()
        {
            _windowStart = null;
            _windowCurrent = null;
        }

        /// <summary>
        /// Recursively collect all drawable objects from the scene
        /// </summary>
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

        /// <summary>
        /// Determines if an object is fully inside the selection rectangle (window selection)
        /// </summary>
        private bool IsObjectInsideRectangle(OpenCADObject obj, double minX, double maxX, double minY, double maxY)
        {
            if (obj is GeometryBase geometry)
            {
                // For other drawable objects, try to get their bounds
                // This is a simplified check - you may need to implement proper bounds checking
                var extents = geometry.GetExtents();
                return extents.Min.X >= minX && extents.Max.X <= maxX &&
                        extents.Min.Y >= minY && extents.Max.Y <= maxY;
            }

            return false;
        }
    }
}
