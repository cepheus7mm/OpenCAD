using OpenCAD.Geometry.Helpers.Snaps;
using OpenCAD.Interfaces;
using OpenCAD.Undo;
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
        private bool _isCopyDrag;
        private Dictionary<OpenCADObject, OpenCADObject> _previewEntities = new();
        private Dictionary<OpenCADObject, OpenCADObject> _initialEntities = new();
        private HashSet<OpenCADObject> _cachedEditedOwners = new();

        private readonly IPreviewManager _preview;
        private readonly ISnapManager _snapManager;
        private readonly ISelectionManager _selectionManager;


        public Grip? HoverGrip => _hoverGrip;

        private HashSet<Grip> _selectedGrips = new();
        private bool isCopyDrag;
        private bool _copyForcedByMenu;    // persistent until commit/cancel
        private bool _copyLoopActive;

        public bool IsEditing => _isEditing;

        public IReadOnlyList<Grip> SelectedGrips => _selectedGrips.ToList();

        public Grip? ActiveGrip => _activeGrip;

        public GripEditMode ActiveMode { get; private set; } = defaultMode;

        private const GripEditMode defaultMode = GripEditMode.Stretch;

        public GripManager(OpenCADDocument document, IHitTester hitTester, IGripProviderFactory gripProviderFactory, IPreviewManager previewManager, ISnapManager snapManager, ISelectionManager selectionManager)
        {
            _document = document;
            _hitTester = hitTester;
            _providerFactory = gripProviderFactory;
            _preview = previewManager;
            _snapManager = snapManager;
            _selectionManager = selectionManager;
        }

        // ------------------------------------------------------------
        // HOVER LOGIC
        // ------------------------------------------------------------
        public void UpdateHover(HitResult? hit)
        {
            var newHover = hit?.Grip;

            // Case 1: cursor left all grips
            if (!newHover.HasValue)
            {
                if (_hoverGrip.HasValue)
                {
                    _hoverGrip = null;
                    _snapManager.ClearForcedSnap();
                }
                return;
            }

            // Case 2: cursor is still on the same grip → nothing to do
            if (_hoverGrip.HasValue &&
                newHover.Value.Position == _hoverGrip.Value.Position)
            {
                return;
            }

            // Case 3: cursor moved onto a new grip
            _hoverGrip = newHover;
            _snapManager.SetForcedSnap(_hoverGrip.Value.Position);
        }

        // ------------------------------------------------------------
        // BEGIN EDIT
        // ------------------------------------------------------------
        public void BeginEdit(Vector2 mouseWorld)
        {
            if (_hoverGrip == null || _providerFactory == null)
                return;

            _snapManager.ClearForcedSnap();

            _isEditing = true;
            _activeGrip = _hoverGrip;

            // Correct: anchor all modes to the grip’s world position
            _startGripPos = _activeGrip.Value.Position;
            _startMouseWorld = _startGripPos;

            // Capture original entity state for advanced modes (rotate, scale, mirror, etc.)
            _initialEntities.Clear();
            foreach (var grip in _selectedGrips)
                _initialEntities[grip.Owner] = grip.Owner.Clone();

            // Copy mode: no exclusions
            if (!_isCopyDrag)
            {
                _cachedEditedOwners = new HashSet<OpenCADObject>(
                    _selectedGrips.Select(g => g.Owner)
                );
                _snapManager.SetSnapExclusions(_cachedEditedOwners);
            }
            else
            {
                _cachedEditedOwners = new HashSet<OpenCADObject>();
                _snapManager.SetSnapExclusions(_cachedEditedOwners);
            }

            _preview.Begin();

            if (!_isCopyDrag)
            {
                foreach (var grip in _selectedGrips)
                    _preview.HideOriginal(grip.Owner);
            }

            // Initial preview (zero delta)
            UpdateEdit(mouseWorld);
        }

        // ------------------------------------------------------------
        // UPDATE EDIT (DRAG)
        // ------------------------------------------------------------
        public void UpdateEdit(Vector2 mouseWorld)
        {
            if (!_isEditing || _activeGrip == null || _providerFactory == null)
                return;

            var snapped = _snapManager.GetFinalSnapPoint(mouseWorld);
            var delta = snapped - _startMouseWorld;

            _preview.Clear();
            _previewEntities.Clear();

            bool isEntityLevel =
                ActiveMode == GripEditMode.Move ||
                ActiveMode == GripEditMode.Rotate ||
                ActiveMode == GripEditMode.Scale ||
                ActiveMode == GripEditMode.Mirror;

            if (isEntityLevel)
            {
                // ------------------------------------------------------------
                // ENTITY-LEVEL TRANSFORM
                // ------------------------------------------------------------
                foreach (var entity in _selectionManager.SelectedObjects)
                {
                    var provider = _providerFactory.GetProvider(entity);
                    if (provider == null)
                        continue;

                    var preview = provider.ApplyGripDelta(
                        _activeGrip.Value,
                        entity,
                        delta,
                        ActiveMode
                    );

                    if (preview != null)
                    {
                        _previewEntities[entity] = preview;
                        _preview.ShowPreview(preview);
                    }
                }
            }
            else
            {
                // ------------------------------------------------------------
                // GRIP-LEVEL TRANSFORM (Stretch, Lengthen)
                // ------------------------------------------------------------
                foreach (var grip in _selectedGrips)
                {
                    var provider = _providerFactory.GetProvider(grip.Owner);
                    if (provider == null)
                        continue;

                    var preview = provider.ApplyGripDelta(
                        _activeGrip.Value,
                        grip,
                        delta,
                        ActiveMode
                    );

                    if (preview != null)
                    {
                        _previewEntities[grip.Owner] = preview;
                        _preview.ShowPreview(preview);
                    }
                }
            }

            // ------------------------------------------------------------
            // SNAP EXCLUSIONS
            // ------------------------------------------------------------
            var exclusions = new HashSet<OpenCADObject>();

            if (!_isCopyDrag)
            {
                foreach (var owner in _cachedEditedOwners)
                    exclusions.Add(owner);
            }

            foreach (var preview in _previewEntities.Values)
                exclusions.Add(preview);

            _snapManager.SetSnapExclusions(exclusions);
        }


        // ------------------------------------------------------------
        // COMMIT EDIT
        // ------------------------------------------------------------
        public List<OpenCADObject>? CommitEdit()
        {
            if (!_isEditing)
                return null;

            // Stop snapping exclusions and forced snap
            _snapManager.ClearSnapExclusions();
            _snapManager.ClearForcedSnap();

            var committed = new List<OpenCADObject>();
            var undo = _document.GetUndoRedoManager();
            undo.BeginTransaction(_isCopyDrag ? "Grip copy" : "Grip edit");

            foreach (var kvp in _previewEntities)
            {
                var original = kvp.Key;
                var preview = kvp.Value;

                if (_isCopyDrag)
                {
                    // COPY MODE: add new entity instead of replacing original
                    var clone = preview.Clone();
                    var action = new AddGeometryAction(clone, "Grip copy");
                    undo.ExecuteAction(action);
                    committed.Add(clone);
                }
                else
                {
                    // NORMAL MODE: replace original
                    var action = new ReplaceGeometryAction(original, preview, "Grip edit");
                    undo.ExecuteAction(action);
                    committed.Add(preview);
                }
            }

            undo.CommitTransaction();

            // Finalize preview (preview objects become real)
            _preview.Commit();

            if (_isCopyDrag)
            {
                // COPY LOOP: do NOT reset state
                // Keep the active grip, selected grips, and editing state
                // Restart the edit so the user can place another copy

                _previewEntities.Clear();

                // Reinitialize provider for next copy placement
                //var provider = _providerFactory.GetProvider(_activeGrip.Value.Owner);
                //provider.InitializeGripEdit(_activeGrip.Value);

                // Begin next copy iteration
                // (mouseWorld must be the current mouse world position)
                BeginEdit(_startMouseWorld);

                return committed;
            }

            // NORMAL MODE: reset state
            _previewEntities.Clear();
            _cachedEditedOwners.Clear();
            _isEditing = false;
            _activeGrip = null;
            _selectedGrips.Clear();
            _hoverGrip = null;

            // Reset mode to default
            ActiveMode = defaultMode;

            return committed;
        }

        // ------------------------------------------------------------
        // CANCEL EDIT
        // ------------------------------------------------------------
        public void CancelEdit()
        {
            //if (!_isEditing && !_copyLoopActive)
            //    return;

            // Always stop copy loop on ESC
            _copyLoopActive = false;

            // Stop snapping exclusions and forced snap
            _snapManager.ClearSnapExclusions();
            _snapManager.ClearForcedSnap();

            // If we were mid-drag, discard preview geometry
            // (restores originals for normal edit, discards copy preview for copy mode)
            _preview.Clear();

            // Reset state
            _previewEntities.Clear();
            _cachedEditedOwners.Clear();
            _isEditing = false;
            _isCopyDrag = false;
            _activeGrip = null;
            _selectedGrips.Clear();
            _hoverGrip = null;
            ActiveMode = defaultMode;
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

        // ------------------------------------------------------------
        // Modifier Keys
        // ------------------------------------------------------------

        public void UpdateCopyModifier(bool shiftDown, bool ctrlDown)
        {
            if (_copyForcedByMenu)
                return; // menu selection overrides modifiers

            _isCopyDrag = shiftDown || ctrlDown;
        }

        public void SetActiveMode(GripEditMode selectedMode)
        {
            ActiveMode = selectedMode;

            if (selectedMode == GripEditMode.Copy)
            {
                _copyForcedByMenu = true;
                _isCopyDrag = true;
            }
        }

        public string GetGripStateTooltip()
        {
            // Case 1: Currently dragging/editing a grip
            if (_isEditing && _activeGrip.HasValue)
            {
                var pos = _activeGrip.Value.Position;
                return $"Dragging grip at ({pos.X:F3}, {pos.Y:F3})";
            }

            // Case 2: Hovering over a grip but not editing
            if (_hoverGrip.HasValue)
            {
                var pos = _hoverGrip.Value.Position;
                return $"Hovering on grip at ({pos.X:F3}, {pos.Y:F3})";
            }

            // Case 3: Not on any grip
            return "Not on a grip";
        }
    }
}
