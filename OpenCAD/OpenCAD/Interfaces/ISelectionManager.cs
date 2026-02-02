using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface ISelectionManager
    {
        event EventHandler SelectionChanged;
        IReadOnlyCollection<OpenCADObject> SelectedObjects { get; }
        Point3D? WindowSelectionStart { get; }
        Point3D? WindowSelectionCurrent { get; }

        IReadOnlyCollection<OpenCADObject> PreviewObjects { get; }


        void SelectSingle(OpenCADObject obj);
        void AddToSelection(OpenCADObject obj);
        void AddToSelection(List<OpenCADObject> objects);
        void ToggleSelection(OpenCADObject obj);
        void Deselect(OpenCADObject obj);
        void ClearSelection();

        void BeginWindowSelection(Point3D startWorld);
        void UpdateWindowSelection(Point3D currentWorld);
        void CommitWindowSelection();
        void CancelWindowSelection();
    }
}
