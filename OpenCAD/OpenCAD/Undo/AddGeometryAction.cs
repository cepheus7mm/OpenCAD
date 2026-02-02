using System;
using OpenCAD;

namespace OpenCAD.Undo
{
    /// <summary>
    /// Undoable action for adding geometry to the document.
    /// Document is mutated only through Execute/Undo.
    /// </summary>
    public class AddGeometryAction : IUndoableAction
    {
        private readonly OpenCADObject _geometry;

        public string Description { get; }

        public AddGeometryAction(OpenCADObject geometry, string description)
        {
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            Description = description;
        }

        public void Execute(OpenCADDocument document)
        {
            document.Add(_geometry);
            document.MarkAsModified();
        }

        public void Undo(OpenCADDocument document)
        {
            document.Remove(_geometry);
            document.MarkAsModified();
        }
    }
}