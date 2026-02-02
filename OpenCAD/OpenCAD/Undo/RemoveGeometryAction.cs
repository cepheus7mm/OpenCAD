using System;
using System.Collections.Generic;
using OpenCAD;

namespace OpenCAD.Undo
{
    /// <summary>
    /// Undoable action for removing geometry from the document.
    /// Document is mutated only through Execute/Undo.
    /// </summary>
    public class RemoveGeometryAction : IUndoableAction
    {
        private readonly List<OpenCADObject> _geometry;

        public string Description { get; }

        public RemoveGeometryAction(IEnumerable<OpenCADObject> geometry, string description)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));

            _geometry = new List<OpenCADObject>(geometry);
            Description = description;
        }

        public RemoveGeometryAction(OpenCADObject geometry, string description)
            : this(new[] { geometry ?? throw new ArgumentNullException(nameof(geometry)) }, description)
        {
        }

        public void Execute(OpenCADDocument document)
        {
            foreach (var obj in _geometry)
                document.Remove(obj);

            document.MarkAsModified();
        }

        public void Undo(OpenCADDocument document)
        {
            foreach (var obj in _geometry)
                document.Add(obj);

            document.MarkAsModified();
        }
    }
}