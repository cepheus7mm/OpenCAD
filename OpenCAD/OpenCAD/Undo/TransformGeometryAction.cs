using System;
using System.Collections.Generic;
using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;

namespace OpenCAD.Undo
{
    /// <summary>
    /// Deprecated: Use ReplaceGeometry instead.
    /// Undoable transform action using a 4x4 matrix.
    /// Apply matrix in Execute(), restore original objects in Undo().
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
                if (obj is not ICurve curve)
                    continue;

                var transformed = curve.Transform(matrix) as OpenCADObject;
                if (transformed == null)
                    continue;

                _before.Add(obj);

                // Preserve ID
                transformed.ID = obj.ID;

                _after.Add(transformed);
            }

            Description = description;
        }

        public void Execute(OpenCADDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            for (int i = 0; i < _before.Count; i++)
                document.ReplaceObject(_before[i], _after[i]);

            document.MarkAsModified();
        }

        public void Undo(OpenCADDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            for (int i = 0; i < _before.Count; i++)
                document.ReplaceObject(_after[i], _before[i]);

            document.MarkAsModified();
        }
    }
}