using System;
using System.Collections.Generic;
using OpenCAD;

namespace OpenCAD.Undo
{
    /// <summary>
    /// Undoable action for adding multiple geometry objects (model-first).
    /// </summary>
    public class AddMultipleGeometryAction : IUndoableAction
    {
        private readonly IReadOnlyList<OpenCADObject> _geometries;
        private readonly OpenCADDocument _document;

        public string Description { get; }

        public AddMultipleGeometryAction(IEnumerable<OpenCADObject> geometries, OpenCADDocument document, string description)
        {
            if (geometries == null) throw new ArgumentNullException(nameof(geometries));

            _geometries = new List<OpenCADObject>(geometries);
            _document = document ?? throw new ArgumentNullException(nameof(document));
            Description = description;
        }

        public void Execute(OpenCADDocument document)
        {
            foreach (var g in _geometries)
                _document.Add(g);

            _document.MarkAsModified();
        }

        public void Undo(OpenCADDocument document)
        {
            foreach (var g in _geometries)
                _document.Remove(g);

            _document.MarkAsModified();
        }
    }
}