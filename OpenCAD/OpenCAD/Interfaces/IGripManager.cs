using OpenCAD.Grips;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface IGripManager
    {
        /// <summary>
        /// Updates hover state based on mouse position.
        /// </summary>
        void UpdateHover(HitResult? hitResult);

        /// <summary>
        /// Attempts to begin a grip edit if a grip is hovered.
        /// </summary>
        void BeginEdit(Vector2 mouseWorld);

        /// <summary>
        /// Updates the active grip edit (preview geometry).
        /// </summary>
        void UpdateEdit(Vector2 mouseWorld);

        /// <summary>
        /// Commits the preview geometry and ends the grip edit.
        /// </summary>
        List<OpenCADObject>? CommitEdit();

        /// <summary>
        /// Cancels the grip edit and restores the original entity.
        /// </summary>
        void CancelEdit();

        /// <summary>
        /// Returns the preview entity if one exists.
        /// </summary>
        OpenCADObject? GetPreviewEntity();

        /// <summary>
        /// Returns the currently hovered grip, if any.
        /// </summary>
        Grip? HoverGrip { get; }

        /// <summary>
        /// Returns the currently active grip, if any.
        /// </summary>
        Grip? ActiveGrip { get; }

        IReadOnlyList<Grip> SelectedGrips { get; }

        void SelectSingle(Grip grip);
        void AddToSelection(Grip grip);
        void ClearSelection();
        bool IsGripSelected(Grip grip);
        IReadOnlyDictionary<OpenCADObject, OpenCADObject> GetPreviewObjects();
    }
}
