using OpenCAD;
using UI.Controls.Viewport;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Undoable action for removing geometry from the document
    /// </summary>
    public class RemoveGeometryAction : IUndoableAction
    {
        private readonly OpenCADObject _geometry;
        private readonly OpenCADDocument _document;
        private readonly ViewportControl? _viewport;

        public string Description { get; }

        public RemoveGeometryAction(OpenCADObject geometry, OpenCADDocument document, ViewportControl? viewport, string description)
        {
            _geometry = geometry;
            _document = document;
            _viewport = viewport;
            Description = description;
        }

        public void Execute()
        {
            _document.Remove(_geometry);
            _viewport?.RemoveObject(_geometry);
        }

        public void Undo()
        {
            _document.Add(_geometry);
            _viewport?.AddObject(_geometry);
        }
    }
}