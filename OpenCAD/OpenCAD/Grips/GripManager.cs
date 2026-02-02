using OpenCAD.Geometry.Helpers.Snaps;
using OpenCAD.Interfaces;
using OpenCAD.Undo.OpenCAD.Undo;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Numerics;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips
{
    public sealed class GripManager : IGripManager
    {
        private readonly OpenCADDocument _document;   // Your model that stores entities
        private readonly IHitTester _hitTester;      // Your existing hit-testing system

        private OpenCADObject? _targetEntity;
        private readonly IGripProviderFactory _providerFactory;

        private Grip? _hoverGrip;
        private Grip? _activeGrip;

        private OpenCADObject? _previewEntity;

        private Vector2 _startMouseWorld;
        private Vector2 _startGripPos;

        private bool _isEditing;

        private Dictionary<OpenCADObject, OpenCADObject> _previewEntities = new();

        private readonly IPreviewManager _preview;
        private readonly ISnapManager _snapManager;


        public Grip? HoverGrip => _hoverGrip;

        private HashSet<Grip> _selectedGrips = new();

        public IReadOnlyList<Grip> SelectedGrips => _selectedGrips.ToList();

        public Grip? ActiveGrip => _activeGrip;

        public GripManager(OpenCADDocument document, IHitTester hitTester, IGripProviderFactory gripProviderFactory, IPreviewManager previewManager, ISnapManager snapManager)
        {
            _document = document;
            _hitTester = hitTester;
            _providerFactory = gripProviderFactory;
            _preview = previewManager;
            _snapManager = snapManager;
        }

        // ------------------------------------------------------------
        // HOVER LOGIC
        // ------------------------------------------------------------
        public void UpdateHover(HitResult? hit)
        {
            var newHover = hit?.Grip;

            if (!newHover.HasValue || (HoverGrip.HasValue && newHover.Value.Position == HoverGrip.Value.Position))
                return;

            _hoverGrip = newHover;

            if (HoverGrip != null)
            {
                // Lock snap to the grip position
                _snapManager.SetForcedSnap(HoverGrip.Value.Position);
            }
            else
            {
                // Release forced snap
                _snapManager.ClearForcedSnap();
            }
        }

        // ------------------------------------------------------------
        // BEGIN EDIT
        // ------------------------------------------------------------
        public void BeginEdit(Vector2 mouseWorld)
        {
            if (_hoverGrip == null || _providerFactory == null)
                return;

            // Stop forcing snap — allow normal snapping during drag
            _snapManager.ClearForcedSnap();

            _isEditing = true;
            _activeGrip = _hoverGrip;
            _startMouseWorld = mouseWorld;
            _startGripPos = _activeGrip.Value.Position;

            // Start preview session
            _preview.Begin();

            // Hide originals
            foreach (var grip in _selectedGrips)
                _preview.HideOriginal(grip.Owner);

            // Create initial preview (zero delta)
            UpdateEdit(mouseWorld);
        }

        // ------------------------------------------------------------
        // UPDATE EDIT (DRAG)
        // ------------------------------------------------------------
        public void UpdateEdit(Vector2 mouseWorld)
        {
            if (!_isEditing || _activeGrip == null || _providerFactory == null)
                return;

            // Apply snapping
            var snapped = _snapManager.GetFinalSnapPoint(mouseWorld);

            var delta = snapped - _startMouseWorld;

            // Clear previous preview objects
            _preview.Clear();

            _previewEntities.Clear();

            foreach (var grip in _selectedGrips)
            {
                var provider = _providerFactory.GetProvider(grip.Owner);
                if (provider == null)
                    continue;

                var preview = provider.ApplyGripDelta(grip, delta);
                if (preview != null)
                {
                    _previewEntities[grip.Owner] = preview;
                    _preview.ShowPreview(preview);
                }
            }
        }

        // ------------------------------------------------------------
        // COMMIT EDIT
        // ------------------------------------------------------------
        public List<OpenCADObject>? CommitEdit()
        {
            if (!_isEditing)
                return null;

            var committed = new List<OpenCADObject>();

            var undo = _document.GetUndoRedoManager();
            undo.BeginTransaction("Grip edit");

            foreach (var kvp in _previewEntities)
            {
                var original = kvp.Key;
                var preview = kvp.Value;

                var action = new ReplaceGeometryAction(original, preview, "Grip edit");
                undo.ExecuteAction(action);

                committed.Add(preview);
            }

            undo.CommitTransaction();

            // Finalize preview (keep preview objects visible)
            _preview.Commit();

            // Reset state
            _previewEntities.Clear();
            _isEditing = false;
            _activeGrip = null;
            _selectedGrips.Clear();
            _hoverGrip = null;

            return committed;
        }

        // ------------------------------------------------------------
        // CANCEL EDIT
        // ------------------------------------------------------------
        public void CancelEdit()
        {
            if (!_isEditing)
                return;

            _preview.Clear(); // restore originals

            _previewEntities.Clear();
            _isEditing = false;
            _activeGrip = null;
            _selectedGrips.Clear();
            _hoverGrip = null;
        }

        // ------------------------------------------------------------
        // PREVIEW ACCESSOR
        // ------------------------------------------------------------
        public OpenCADObject? GetPreviewEntity() => _previewEntity;

        public void SelectSingle(Grip grip)
        {
            _selectedGrips.Clear();
            _selectedGrips.Add(grip);
            _activeGrip = grip;
        }

        public void AddToSelection(Grip grip)
        {
            _selectedGrips.Add(grip);
            _activeGrip = grip;
        }

        public void ClearSelection()
        {
            _selectedGrips.Clear();
            _activeGrip = null;
        }

        public bool IsGripSelected(Grip grip)
        {
            return _selectedGrips.Contains(grip);
        }

        public IReadOnlyDictionary<OpenCADObject, OpenCADObject> GetPreviewObjects()
        {
            return _previewEntities;
        }
    }
}
