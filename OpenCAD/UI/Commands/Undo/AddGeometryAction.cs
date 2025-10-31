using OpenCAD;
using UI.Controls.Viewport;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Undoable action for adding geometry to the document
    /// </summary>
    public class AddGeometryAction : IUndoableAction
    {
        private readonly OpenCADObject _geometry;
        private readonly OpenCADDocument _document;
        private readonly ViewportControl? _viewport;

        public string Description { get; }

        public AddGeometryAction(OpenCADObject geometry, OpenCADDocument document, ViewportControl? viewport, string description)
        {
            _geometry = geometry;
            _document = document;
            _viewport = viewport;
            Description = description;
        }

        public void Execute()
        {
            _document.Add(_geometry);
            _viewport?.AddObject(_geometry);
        }

        public void Undo()
        {
            _document.Remove(_geometry);
            _viewport?.RemoveObject(_geometry);
        }
    }
}