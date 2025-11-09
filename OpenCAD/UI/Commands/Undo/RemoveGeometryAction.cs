using OpenCAD;
using UI.Controls.Viewport;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Undoable action for removing geometry from the document
    /// </summary>
    public class RemoveGeometryAction : IUndoableAction
    {
        private readonly List<OpenCADObject> _geometry;
        private readonly OpenCADDocument _document;
        private readonly ViewportControl? _viewport;

        public string Description { get; }

        public RemoveGeometryAction(
            IEnumerable<OpenCADObject> geometry, 
            OpenCADDocument document, 
            ViewportControl? viewport, 
            string description)
        {
            _geometry = new List<OpenCADObject>(geometry);
            _document = document;
            _viewport = viewport;
            Description = description;
        }

        public RemoveGeometryAction(
            OpenCADObject geometry, 
            OpenCADDocument document, 
            ViewportControl? viewport, 
            string description)
            : this(new[] { geometry }, document, viewport, description)
        {
        }

        public void Execute()
        {
            // Remove each object from the document and viewport
            foreach (var obj in _geometry)
            {
                _document.Remove(obj);
                _viewport?.RemoveObject(obj);
            }
            
            // Refresh the viewport to show changes
            _viewport?.Refresh();
        }

        public void Undo()
        {
            // Re-add each object to the document and viewport
            foreach (var obj in _geometry)
            {
                _document.Add(obj);
                _viewport?.AddObject(obj);
            }
            
            // Refresh the viewport to show changes
            _viewport?.Refresh();
        }
    }
}