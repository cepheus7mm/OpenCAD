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
        private readonly List<OpenCADObject> _objects;
        private readonly Matrix4D _matrix;
        private readonly Matrix4D _inverse;
        public string Description { get; }

        public TransformGeometryAction(IEnumerable<OpenCADObject> objects, Matrix4D matrix, string description)
        {
            _objects = new List<OpenCADObject>(objects ?? throw new ArgumentNullException(nameof(objects)));
            _matrix = matrix;
            if (!Matrix4D.TryInvert(matrix, out _inverse))
                throw new InvalidOperationException("Transform matrix is not invertible.");
            Description = description;
        }

        public void Execute(ICommandContext context)
        {
            var doc = context?.GetDocument();

            foreach (var obj in _objects)
            {
                if (obj is ICurve curve)
                {
                    try { curve.Transform(_matrix); }
                    catch (NotImplementedException) { }
                }

                // Notify document (if available) that object changed
                doc?.NotifyObjectChanged(obj);
            }

            doc?.MarkAsModified();

            // Best-effort UI refresh
            try
            {
                context?.PostToUI(() => context?.GetActiveViewport()?.Refresh());
            }
            catch { }
        }

        public void Undo(ICommandContext context)
        {
            var doc = context?.GetDocument();

            foreach (var obj in _objects)
            {
                if (obj is ICurve curve)
                {
                    try { curve.Transform(_inverse); }
                    catch (NotImplementedException) { }
                }

                doc?.NotifyObjectChanged(obj);
            }

            doc?.MarkAsModified();

            try
            {
                context?.PostToUI(() => context?.GetActiveViewport()?.Refresh());
            }
            catch { }
        }
    }
}