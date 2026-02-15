using OpenCAD.Geometry.Helpers.GeoPoints;
using OpenCAD.Geometry.Helpers.Snaps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface ISnapManager
    {
        // ------------------------------------------------------------
        // SNAP MODES
        // ------------------------------------------------------------

        // Cursor snapping (grid, ortho, polar)
        bool GridSnapEnabled { get; set; }
        bool OrthoEnabled { get; set; }
        bool PolarTrackingEnabled { get; set; }
        double GridSize { get; set; }
        IReadOnlyList<double> PolarAngles { get; }

        // Object snapping (endpoint, midpoint, center, etc.)
        GeoPointModes EnabledObjectSnaps { get; set; }

        // ------------------------------------------------------------
        // FORCED SNAP (e.g., grip hover)
        // ------------------------------------------------------------

        void SetForcedSnap(Vector2 position);
        void ClearForcedSnap();
        bool HasForcedSnap { get; }

        // ------------------------------------------------------------
        // MAIN ENTRY POINT
        // ------------------------------------------------------------

        /// <summary>
        /// Computes the final snap point given the raw mouse world position.
        /// Applies cursor snapping, then object snapping, then forced snap.
        /// </summary>
        Vector2 GetFinalSnapPoint(Vector2 rawMouseWorld, bool applyCursorSnap = true, bool applyGeoSnap = true);

        // ------------------------------------------------------------
        // SNAP MARKER (for UI)
        // ------------------------------------------------------------

        /// <summary>
        /// The current object snap point (if any).
        /// </summary>
        SnapPoint? CurrentObjectSnap { get; }

        /// <summary>
        /// Raised whenever the snap marker changes (UI listens to this).
        /// </summary>
        event Action<SnapPoint?> SnapPointChanged;

        // ------------------------------------------------------------
        // INTERNAL SUPPORT (called by GripManager, SelectionManager, etc.)
        // ------------------------------------------------------------

        /// <summary>
        /// Computes object snap candidates only (no cursor snap).
        /// </summary>
        SnapPoint? ComputeObjectSnap(Vector2 position);

        void SetSnapExclusions(IEnumerable<OpenCADObject> objects);

        void ClearSnapExclusions();

        /// <summary>
        /// Applies cursor snapping only (grid, ortho, polar).
        /// </summary>
        Vector2 ApplyCursorSnap(Vector2 rawMouseWorld);
    }
}
