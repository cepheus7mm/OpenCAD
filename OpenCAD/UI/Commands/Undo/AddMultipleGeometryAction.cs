using OpenCAD;
using UI.Controls.Viewport;

namespace UI.Commands.Undo
{
    /// <summary>
    /// Undoable action for adding/removing multiple geometry objects as a single unit.
    /// </summary>
    public class AddMultipleGeometryAction : IUndoableAction
    {
        private readonly List<OpenCADObject> _geometry;
        private readonly OpenCADDocument _document;
        private readonly ViewportControl? _viewport;

        public string Description { get; }

        public AddMultipleGeometryAction(
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

        public void Execute()
        {
            foreach (var obj in _geometry)
            {
                _document.Add(obj);
                _viewport?.AddObject(obj);
            }
            _viewport?.Refresh();
        }

        public void Undo()
        {
            foreach (var obj in _geometry)
            {
                _document.Remove(obj);
                _viewport?.RemoveObject(obj);
            }
            _viewport?.Refresh();
        }
    }
}