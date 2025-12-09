using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using System.Numerics;
using UI.Controls.Viewport;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Undoable transform action using a 4x4 matrix.
    /// Apply matrix in Execute(), apply inverse in Undo().
    /// </summary>
    public class TransformGeometryAction : IUndoableAction
    {
        private readonly List<OpenCADObject> _objects;
        private readonly Matrix4D _matrix;
        private readonly Matrix4D _inverse;
        public string Description { get; }

        public TransformGeometryAction(IEnumerable<OpenCADObject> objects, Matrix4D matrix, string description)
        {
            _objects = new List<OpenCADObject>(objects);
            _matrix = matrix;
            if (!Matrix4D.TryInvert(matrix, out _inverse))
                throw new InvalidOperationException("Transform matrix is not invertible.");
            Description = description;
        }

        public void Execute()
        {
            foreach (var obj in _objects)
            {
                if (obj is GeometryBase geom)
                {
                    try { geom.Transform(_matrix); }
                    catch (NotImplementedException)
                    {
                    }
                }
            }
        }

        public void Undo()
        {
            foreach (var obj in _objects)
            {
                if (obj is GeometryBase geom)
                {
                    try { geom.Transform(_inverse); }
                    catch (NotImplementedException)
                    {
                    }
                }
            }
        }
    }
}