using System;
using System.Collections.Generic;
using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Undoable transform action using a 4x4 matrix.
    /// Apply matrix in Execute(), apply inverse in Undo().
    /// Operates on model only; notifies document listeners of changes.
    /// </summary>
    public class TransformGeometryAction : IUndoableAction
    {
        private readonly List<OpenCADObject> _before;
        private readonly List<OpenCADObject> _after;
        public string Description { get; }

        public TransformGeometryAction(IEnumerable<OpenCADObject> objects, Matrix4D matrix, string description)
        {
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));

            _before = new List<OpenCADObject>();
            _after = new List<OpenCADObject>();

            foreach (var obj in objects)
            {
                if (obj is not ICurve)
                    continue; // Only transform curves for now

                // Transform returns a NEW object
                var transformed = ((ICurve)obj).Transform(matrix) as OpenCADObject;

                if (transformed == null)
                    continue; // Skip if transformation failed
                _before.Add(obj);

                // Preserve ID
                transformed.ID = obj.ID;

                _after.Add(transformed);
            }

            Description = description;
        }

        public void Execute(ICommandContext context)
        {
            var doc = context?.GetDocument();
            if (doc == null) return;

            for (int i = 0; i < _before.Count; i++)
            {
                var oldObj = _before[i];
                var newObj = _after[i];

                doc.ReplaceObject(oldObj, newObj);
            }

            doc.MarkAsModified();
            context?.PostToUI(() => context?.GetActiveViewport()?.Refresh());
        }

        public void Undo(ICommandContext context)
        {
            var doc = context?.GetDocument();
            if (doc == null) return;

            for (int i = 0; i < _before.Count; i++)
            {
                var oldObj = _before[i];
                var newObj = _after[i];

                doc.ReplaceObject(newObj, oldObj);
            }

            doc.MarkAsModified();
            context?.PostToUI(() => context?.GetActiveViewport()?.Refresh());
        }
    }
}