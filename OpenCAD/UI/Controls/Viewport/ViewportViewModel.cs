using GraphicsEngine;
using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using OpenCAD.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using UI.Controls.MainWindow;

namespace UI.Controls.Viewport
{
    /// <summary>
    /// ViewModel for ViewportControl
    /// Handles viewport state, point picking, and coordinate transformations
    /// </summary>
    public class ViewportViewModel : INotifyPropertyChanged
    {
        public enum InputMode
        {
            None,
            PointPicking,
            Selection,
            WindowSelection,
            CommandInput
        }
        #region Fields

        private readonly OpenCADDocument _document;
        private StatusBarControl? _statusBar;

        // Input mode
        private InputMode _inputMode = InputMode.Selection;

        // Point picking state
        //private bool _isPointPickingMode = false;
        private readonly List<Point3D> _tempPoints = new();
        private Action<Point3D>? _previewCallback;
        private Point3D? _previewPoint;

        // Selection state
        //private bool _isSelectionMode = false;
        private OpenCADObject? _highlightedObject;
        private readonly List<OpenCADObject> _selectedObjects = new();

        // Mouse state
        private Point _lastMousePos;

        // Cursor state
        private Cursor _cursor = Cursors.Arrow;

        private ViewportSettings? _viewportSettings;

        // Window selection state
        private Point3D? _windowSelectionStartPoint;
        private Point3D? _windowSelectionCurrentPoint;
        private readonly List<OpenCADObject> _windowSelectionPreviewObjects = new();

        private readonly List<GeoPoint> _geoPoints = new();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the object being displayed in this viewport
        /// </summary>
        public OpenCADObject ObjectToDisplay => _document;

        private InputMode _previousSelectionMode = InputMode.None;

        /// <summary>
        /// Gets whether point picking mode is enabled
        /// </summary>
        public bool IsPointPickingMode => _inputMode == InputMode.PointPicking;


        /// <summary>
        /// Gets whether selection mode is enabled
        /// </summary>
        public bool IsSelectionMode => _inputMode == InputMode.Selection;

        public bool IsWindowSelectionMode => _inputMode == InputMode.WindowSelection;

        public InputMode CurrentInputMode
        {
            get => _inputMode;
            internal set
            {
                if (_inputMode != value)
                {
                    _inputMode = value;
                    OnPropertyChanged();
                    UpdateCursor();
                }
            }
        }

        /// <summary>
        /// Gets the currently highlighted object (hover)
        /// </summary>
        public OpenCADObject? HighlightedObject
        {
            get => _highlightedObject;
            internal set  // Changed from private to internal
            {
                if (_highlightedObject != value)
                {
                    _highlightedObject = value;
                    OnPropertyChanged();
                    RefreshRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets the list of selected objects
        /// </summary>
        public IReadOnlyList<OpenCADObject> SelectedObjects => _selectedObjects.AsReadOnly();

        /// <summary>
        /// Gets the current cursor
        /// </summary>
        public Cursor CurrentCursor
        {
            get => _cursor;
            private set
            {
                if (_cursor != value)
                {
                    _cursor = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the temporary points collected during point picking (read-only view)
        /// </summary>
        public IReadOnlyList<Point3D> TempPoints => _tempPoints.AsReadOnly();

        /// <summary>
        /// Gets the mutable temporary points list for commands to add points directly
        /// WARNING: Use AddTempPoint() instead for proper encapsulation
        /// </summary>
        internal List<Point3D> TempPointsMutable => _tempPoints;

        /// <summary>
        /// Gets the preview point for rendering
        /// </summary>
        public Point3D? PreviewPoint
        {
            get => _previewPoint;
            private set
            {
                if (_previewPoint != value)
                {
                    _previewPoint = value;
                    OnPropertyChanged();
                    RefreshRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets whether snapping is enabled
        /// </summary>
        public uint ApertureSize
        {
            get => _viewportSettings?.ApertureSize ?? 15;
            private set
            {
                if (_viewportSettings?.ApertureSize != value)
                {
                    _viewportSettings!.ApertureSize = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets whether snapping is enabled
        /// </summary>
        public GeoPointModes GeoPointModes
        {
            get => _viewportSettings?.GeoPointModes ?? GeoPointModes.None;
            private set
            {
                if (_viewportSettings?.GeoPointModes != value)
                {
                    _viewportSettings!.GeoPointModes = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets whether snapping is enabled
        /// </summary>
        public bool SnappingEnabled
        {
            get => _viewportSettings?.Snap?.SnapEnabled ?? false;
            private set
            {
                if (_viewportSettings?.Snap != null && _viewportSettings.Snap.SnapEnabled != value)
                {
                    _viewportSettings.Snap.SnapEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the grid size for snapping
        /// </summary>
        public double SnapSize
        {
            get => _viewportSettings?.Snap?.SnapSpacing ?? 1.0;
            private set
            {
                if (_viewportSettings?.Snap != null && Math.Abs(_viewportSettings.Snap.SnapSpacing - value) > 0.0001)
                {
                    _viewportSettings.Snap.SnapSpacing = value;
                    OnPropertyChanged();
                }
            }
        }

        public Point3D? WindowSelectionStartPoint => _windowSelectionStartPoint;
        public Point3D? WindowSelectionCurrentPoint => _windowSelectionCurrentPoint;
        public IReadOnlyList<OpenCADObject> WindowSelectionPreviewObjects => _windowSelectionPreviewObjects.AsReadOnly();

        public bool IsShiftKeyPressed { get; internal set; }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a point is picked
        /// </summary>
        public event EventHandler<PointPickedEventArgs>? PointPicked;

        /// <summary>
        /// Event raised when point picking is cancelled
        /// </summary>
        public event EventHandler? PointPickingCancelled;

        /// <summary>
        /// Event raised when an object is selected
        /// </summary>
        public event EventHandler<ObjectSelectedEventArgs>? ObjectSelected;

        /// <summary>
        /// Event raised when an object is selected
        /// </summary>
        public event EventHandler<ObjectClickedEventArgs>? ObjectClicked;

        /// <summary>
        /// Event raised when the viewport should be refreshed
        /// </summary>
        public event EventHandler? RefreshRequested;

        /// <summary>
        /// Event raised when an object is added to the scene
        /// </summary>
        public event EventHandler<ObjectEventArgs>? ObjectAdded;

        /// <summary>
        /// Event raised when an object is removed from the scene
        /// </summary>
        public event EventHandler<ObjectEventArgs>? ObjectRemoved;

        /// <summary>
        /// Event raised when the selection changes (object selected or deselected)
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Event raised to request a context menu at the given mouse position
        /// </summary>
        public event EventHandler<Point>? GeoPointModesOverrideContextMenuRequested;

        #endregion

        #region Constructor

        public ViewportViewModel() : this(new OpenCADDocument())
        {
        }

        public ViewportViewModel(OpenCADDocument objectToDisplay)
        {
            _document = objectToDisplay ?? throw new ArgumentNullException(nameof(objectToDisplay));

            // Subscribe to document events so the VM reacts to canonical model changes.
            _document.ObjectAdded += OnDocumentObjectAdded;
            _document.ObjectRemoved += OnDocumentObjectRemoved;
            _document.ObjectChanged += OnDocumentObjectChanged;
        }

        #endregion

        #region Public Methods - Selection

        /// <summary>
        /// Enable selection mode
        /// </summary>
        //public void EnableSelectionMode()
        //{
        //    //System.Diagnostics.Debug.WriteLine($"=== EnableSelectionMode called ===");
        //    //System.Diagnostics.Debug.WriteLine($"  Current state: PickMode={IsPointPickingMode}, SelectMode={IsSelectionMode}");

        //    // Don't enable selection mode if point picking is active
        //    if (_isPointPickingMode)
        //    {
        //        //System.Diagnostics.Debug.WriteLine("  Selection mode NOT enabled - point picking mode is active");
        //        return;
        //    }

        //    IsSelectionMode = true;
        //    //System.Diagnostics.Debug.WriteLine($"  Selection mode ENABLED - new state: SelectMode={IsSelectionMode}");
        //}

        /// <summary>
        /// Disable selection mode
        /// </summary>
        //public void DisableSelectionMode()
        //{
        //    //System.Diagnostics.Debug.WriteLine($"=== DisableSelectionMode called ===");
        //    IsSelectionMode = false;
        //    HighlightedObject = null;
        //    //System.Diagnostics.Debug.WriteLine("  Selection mode DISABLED");
        //}

        /// <summary>
        /// Clear all selected objects
        /// </summary>
        public void ClearSelection()
        {
            if (_selectedObjects.Count > 0)  // Only raise event if there were selections
            {
                _selectedObjects.Clear();
                OnPropertyChanged(nameof(SelectedObjects));
                RefreshRequested?.Invoke(this, EventArgs.Empty);
                SelectionChanged?.Invoke(this, EventArgs.Empty);  // ADD THIS LINE
            }
        }

        /// <summary>
        /// Add an object to the selection
        /// </summary>
        public void SelectObject(OpenCADObject obj)
        {
            if (!_selectedObjects.Contains(obj))
            {
                _selectedObjects.Add(obj);
                OnPropertyChanged(nameof(SelectedObjects));
                ObjectSelected?.Invoke(this, new ObjectSelectedEventArgs(obj));
                RefreshRequested?.Invoke(this, EventArgs.Empty);
                SelectionChanged?.Invoke(this, EventArgs.Empty);  // ADD THIS LINE
                //System.Diagnostics.Debug.WriteLine($"Object selected: {obj.GetType().Name}");
            }
        }

        /// <summary>
        /// Remove an object from the selection
        /// </summary>
        public void DeselectObject(OpenCADObject obj)
        {
            if (_selectedObjects.Remove(obj))
            {
                OnPropertyChanged(nameof(SelectedObjects));
                RefreshRequested?.Invoke(this, EventArgs.Empty);
                SelectionChanged?.Invoke(this, EventArgs.Empty);  // ADD THIS LINE
                //System.Diagnostics.Debug.WriteLine($"Object deselected: {obj.GetType().Name}");
            }
        }

        #endregion

        #region Public Methods - Point Picking

        /// <summary>
        /// Enable point picking mode
        /// </summary>
        public void EnablePointPickingMode()
        {
            //System.Diagnostics.Debug.WriteLine($"=== EnablePointPickingMode called, current state: InputMode={CurrentInputMode} ===");

            //// Disable selection mode when entering point picking mode
            //if (_isSelectionMode)
            //{
            //    //System.Diagnostics.Debug.WriteLine("  Disabling selection mode");
            //    IsSelectionMode = false;
            //    HighlightedObject = null;
            //}
            //_previousSelectionMode = IsSelectionMode;
            //IsPointPickingMode = true;
            CurrentInputMode = InputMode.PointPicking;
            //System.Diagnostics.Debug.WriteLine($"  Point picking mode ENABLED, _tempPoints.Count={_tempPoints.Count}");
        }

        /// <summary>
        /// Disable point picking mode
        /// </summary>
        public void DisablePointPickingMode()
        {
            ////System.Diagnostics.Debug.WriteLine($"=== DisablePointPickingMode called ===");
            //IsPointPickingMode = false;
            //_tempPoints.Clear();
            //PreviewPoint = null;
            //_previewCallback = null;
            //IsSelectionMode = _previousSelectionMode;
            CurrentInputMode = InputMode.Selection;
            _geoPoints.Clear();
            //System.Diagnostics.Debug.WriteLine("  Point picking mode DISABLED, temp points cleared");
        }

        /// <summary>
        /// Add a temporary point (for commands that need to track points manually)
        /// </summary>
        public void AddTempPoint(Point3D point)
        {
            _tempPoints.Add(point);
            _document.PreviewPoint = point;
            //System.Diagnostics.Debug.WriteLine($"Temp point added: ({point.X:F3}, {point.Y:F3}, {point.Z:F3}), total count: {_tempPoints.Count}");
            OnPropertyChanged(nameof(TempPoints));
        }

        /// <summary>
        /// Clear temporary points
        /// </summary>
        public void ClearTempPoints()
        {
            _tempPoints.Clear();
            _document.PreviewPoint = null;
            OnPropertyChanged(nameof(TempPoints));
        }

        /// <summary>
        /// Enable preview mode with a callback for mouse position updates
        /// </summary>
        public void EnablePreviewMode(Action<Point3D> previewCallback)
        {
            _previewCallback = previewCallback;
            //System.Diagnostics.Debug.WriteLine("Preview mode ENABLED");
        }

        /// <summary>
        /// Disable preview mode
        /// </summary>
        public void DisablePreviewMode()
        {
            _previewCallback = null;
            PreviewPoint = null;
            //System.Diagnostics.Debug.WriteLine("Preview mode DISABLED");
        }

        /// <summary>
        /// Set the preview point for rendering
        /// </summary>
        public void SetPreviewPoint(Point3D? point)
        {
            PreviewPoint = point;
        }

        /// <summary>
        /// Cancel point picking mode and raise the cancellation event
        /// </summary>
        public void CancelPointPicking()
        {
            if (CurrentInputMode != InputMode.PointPicking)
                return;

            //System.Diagnostics.Debug.WriteLine("CancelPointPicking called - raising PointPickingCancelled event");

            // Raise the cancelled event BEFORE disabling the mode
            // This allows commands to clean up properly
            PointPickingCancelled?.Invoke(this, EventArgs.Empty);
            _geoPoints.Clear();
            // Now disable the mode
            DisablePointPickingMode();
        }

        #endregion

        #region Public Methods - Snapping

        /// <summary>
        /// Enable or disable snapping to grid
        /// </summary>
        //public void EnableSnapping(bool enabled, double gridSize = 1.0)
        //{
        //    SnappingEnabled = enabled;
        //    GridSize = gridSize;
        //}

        /// <summary>
        /// Snap a point to the nearest grid intersection
        /// </summary>
        public Point3D SnapToGrid(Point3D point)
        {
            if (!SnappingEnabled)
                return point;

            return new Point3D(
                Math.Round(point.X / SnapSize) * SnapSize,
                Math.Round(point.Y / SnapSize) * SnapSize,
                Math.Round(point.Z / SnapSize) * SnapSize
            );
        }

        #endregion

        #region Public Methods - Object Management

        /// <summary>
        /// Add an object to the scene (model-first).
        /// The document will raise ObjectAdded and the VM will react via subscription.
        /// </summary>
        public void AddObject(OpenCADObject obj)
        {
            ObjectToDisplay?.Add(obj);
            // Do not raise ObjectAdded/Refresh here — document event handler will do it.
        }

        /// <summary>
        /// Remove an object from the scene (model-first).
        /// The document will raise ObjectRemoved and the VM will react via subscription.
        /// </summary>
        public void RemoveObject(OpenCADObject obj)
        {
            ObjectToDisplay?.Remove(obj);
            // Do not raise Refresh here — document event handler will do it.
        }

        /// <summary>
        /// Document event handlers - update VM state when the canonical model changes.
        /// </summary>
        private void OnDocumentObjectAdded(object? sender, OpenCAD.DocumentObjectEventArgs e)
        {
            if (sender != _document) return;

            // Forward as VM-level event for UI consumers
            ObjectAdded?.Invoke(this, new ObjectEventArgs(e.Object));

            // Single refresh for the change
            RefreshRequested?.Invoke(this, EventArgs.Empty);

            // Notify property changes if selection collections depend on this
            OnPropertyChanged(nameof(ObjectToDisplay));
        }

        private void OnDocumentObjectRemoved(object? sender, OpenCAD.DocumentObjectEventArgs e)
        {
            if (sender != _document) return;

            // If the object was selected, remove it from selection
            if (_selectedObjects.Contains(e.Object))
            {
                _selectedObjects.Remove(e.Object);
                OnPropertyChanged(nameof(SelectedObjects));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            // If the object was highlighted, clear highlight (this triggers RefreshRequested via setter)
            if (HighlightedObject == e.Object)
            {
                HighlightedObject = null;
            }

            ObjectRemoved?.Invoke(this, new ObjectEventArgs(e.Object));
            RefreshRequested?.Invoke(this, EventArgs.Empty);

            OnPropertyChanged(nameof(ObjectToDisplay));
        }

        private void OnDocumentObjectChanged(object? sender, OpenCAD.DocumentObjectEventArgs e)
        {
            if (sender != _document) return;

            // When an existing object is mutated, simply refresh the viewport.
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Public Methods - Status Bar

        /// <summary>
        /// Set the status bar control to display mouse coordinates
        /// </summary>
        public void SetStatusBar(StatusBarControl statusBar)
        {
            _statusBar = statusBar;
        }

        /// <summary>
        /// Update the status bar with world coordinates
        /// </summary>
        public void UpdateStatusBarWithWorldCoordinates(Vector3D? worldPos)
        {
            if (worldPos.HasValue)
            {
                _statusBar?.UpdatePositionText(_document.VectorToString(worldPos.Value));
            }
            else
            {
                _statusBar?.UpdatePositionText("--");
            }
        }

        /// <summary>
        /// Update the status bar with screen coordinates
        /// </summary>
        public void UpdateStatusBarWithScreenCoordinates(Point screenPos)
        {
            _statusBar?.UpdatePositionText($"Screen: {screenPos.X:F0}, {screenPos.Y:F0}");
        }

        public void UpdateStatusBarButtons()
        {
            _statusBar?.UpdateButtons();
        }

        /// <summary>
        /// Clear the status bar coordinates
        /// </summary>
        public void ClearStatusBar()
        {
            _statusBar?.UpdatePositionText("--");
        }

        #endregion

        #region Public Methods - Mouse Handling

        /// <summary>
        /// Handle mouse down event
        /// </summary>
        public MouseHandlingResult HandleMouseDown(MouseButton button, Point mousePos, Vector3? worldPos)
        {
            switch (button)
            {
                case MouseButton.Left:
                    return HandleLeftMouseDown(mousePos, worldPos);
                case MouseButton.Right:
                    return HandleRightMouseDown(mousePos, worldPos);
                case MouseButton.Middle:
                    return HandleMiddleMouse(mousePos, worldPos);
                default:
                    _lastMousePos = mousePos;
                    return new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = true };
            }
        }

        private MouseHandlingResult HandleLeftMouseDown(Point mousePos, Vector3? worldPos)
        {
            if (CurrentInputMode == InputMode.PointPicking)
            {
                return HandleLeftMouseDownPointPicking(mousePos, worldPos);
            }
            else if (CurrentInputMode == InputMode.Selection)
            {
                return HandleLeftMouseDownSelection(mousePos, worldPos);
            }
            else if (CurrentInputMode == InputMode.CommandInput)
            {
                return HandleLeftMouseDownCommandInput(mousePos, worldPos);
            }

            _lastMousePos = mousePos;
            return new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = true };
        }

        private MouseHandlingResult HandleLeftMouseDownCommandInput(Point mousePos, Vector3? worldPos)
        {
            if (HighlightedObject != null && worldPos.HasValue)
            {
                var args = new ObjectClickedEventArgs(HighlightedObject, new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z));
                ObjectClicked?.Invoke(this, args);
                return new MouseHandlingResult { Handled = true, NeedsRefresh = false, CaptureMouse = false };
            }
            return new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = false };
        }

        private MouseHandlingResult HandleLeftMouseDownSelection(Point mousePos, Vector3? worldPos)
        {
            if (HighlightedObject != null)
            {
                if (_selectedObjects.Contains(HighlightedObject))
                {
                    DeselectObject(HighlightedObject);
                }
                else
                {
                    SelectObject(HighlightedObject);
                }
                return new MouseHandlingResult { Handled = true, NeedsRefresh = true, CaptureMouse = false };
            }
            else
            {
                _previousSelectionMode = CurrentInputMode;
                CurrentInputMode = InputMode.WindowSelection;

                if (worldPos.HasValue)
                {
                    _windowSelectionStartPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
                }
                else
                {
                    _windowSelectionStartPoint = null;
                }

                _windowSelectionCurrentPoint = null;
                _windowSelectionPreviewObjects.Clear();
                return new MouseHandlingResult { Handled = true, NeedsRefresh = true, CaptureMouse = true };
            }
        }

        private MouseHandlingResult HandleLeftMouseDownPointPicking(Point mousePos, Vector3? worldPos)
        {
            if (worldPos.HasValue)
            {
                var point = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);

                if (_viewportSettings?.GeoPointModes != GeoPointModes.None && _geoPoints.Any())
                {
                    var geoPoint = GetClosestGeoPoint(_geoPoints, point);
                    if (geoPoint != null)
                    {
                        point = geoPoint.Position;
                      }
                }
                else if (SnappingEnabled)
                {
                    var snappedPoint = SnapToGrid(point);
                    point = snappedPoint;
                }

                _tempPoints.Add(point);
                OnPropertyChanged(nameof(TempPoints));
                PointPicked?.Invoke(this, new PointPickedEventArgs(point));
                _document.GetViewportSettings().GeoPointModeOverride = GeoPointModes.None;

                return new MouseHandlingResult { Handled = true, NeedsRefresh = true, CaptureMouse = false };
            }
            return new MouseHandlingResult { Handled = true, NeedsRefresh = false, CaptureMouse = false };
        }

        private MouseHandlingResult HandleRightMouseDown(Point mousePos, Vector3? worldPos)
        {
            if (CurrentInputMode == InputMode.PointPicking && !IsShiftKeyPressed)
            {
                PointPickingCancelled?.Invoke(this, EventArgs.Empty);
                return new MouseHandlingResult { Handled = true, NeedsRefresh = false, CaptureMouse = false };
            }
            else if (CurrentInputMode == InputMode.PointPicking && IsShiftKeyPressed)
            {
                GeoPointModesOverrideContextMenuRequested?.Invoke(this, mousePos);
                return new MouseHandlingResult { Handled = true, NeedsRefresh = false, CaptureMouse = false };
            }

            _lastMousePos = mousePos;
            return new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = true };
        }

        private MouseHandlingResult HandleMiddleMouse(Point mousePos, Vector3? worldPos)
        {
            _lastMousePos = mousePos;
            return new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = true };
        }

        /// <summary>
        /// Handle mouse move event
        /// </summary>
        public MouseHandlingResult HandleMouseMove(Point currentPos, Vector3D? worldPos, MouseButtonState middleButton, MouseButtonState rightButton, bool isShiftPressed, float panScale, out CameraOperation? cameraOp)
        {
            cameraOp = null;
            double dx = currentPos.X - _lastMousePos.X;
            double dy = currentPos.Y - _lastMousePos.Y;

            // Handle window selection mode
            if (IsWindowSelectionMode && worldPos.HasValue)
            {
                return HandleMouseMoveWindowSelection(currentPos, worldPos.Value);
            }

            // Update status bar
            if (worldPos is not null)
            {
                // If in point picking mode with snapping enabled, show snapped coordinates
                if (CurrentInputMode == InputMode.PointPicking && SnappingEnabled)
                {
                    var rawPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
                    var snappedPoint = SnapToGrid(rawPoint);

                    // Update status bar with snapped coordinates
                    var snappedVector = new Vector3D(snappedPoint.X, snappedPoint.Y, snappedPoint.Z);
                    UpdateStatusBarWithWorldCoordinates(snappedVector);
                }
                else
                {
                    // Show raw world coordinates
                    UpdateStatusBarWithWorldCoordinates(worldPos);
                }

                // Call preview callback during point picking AND update preview point
                if (CurrentInputMode == InputMode.PointPicking && _previewCallback != null)
                {
                    var previewPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);

                    // Apply snapping if enabled
                    if (SnappingEnabled)
                    {
                        previewPoint = SnapToGrid(previewPoint);
                    }

                    // Update the preview point for rendering
                    PreviewPoint = previewPoint;

                    // Also call the callback for command logic
                    _previewCallback(previewPoint);
                }
                else
                {
                    // Clear preview point if not in point picking mode
                    PreviewPoint = null;
                }
            }
            //else
            //{
            //    UpdateStatusBarWithScreenCoordinates(currentPos);
            //}

            // Don't do camera manipulation in point picking mode
            if (CurrentInputMode == InputMode.PointPicking)
            {
                _lastMousePos = currentPos;
                return new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = false };
            }

            bool needsRefresh = false;

            // Only allow camera operations if not in point picking or selection mode
            if (middleButton == MouseButtonState.Pressed)
            {
                // Middle mouse button: Pan normally, Orbit with Shift
                if (isShiftPressed)
                {
                    // Shift + Middle = Orbit
                    cameraOp = new CameraOperation
                    {
                        Type = CameraOperationType.Orbit,
                        DeltaX = (float)dx * 0.01f,
                        DeltaY = (float)dy * 0.01f
                    };
                }
                else
                {
                    // Middle = Pan (use provided pan scale)
                    cameraOp = new CameraOperation
                    {
                        Type = CameraOperationType.Pan,
                        DeltaX = (float)-dx * panScale,
                        DeltaY = (float)dy * panScale
                    };
                }
                needsRefresh = true;
            }
            else if (rightButton == MouseButtonState.Pressed)
            {
                // Right mouse button pans (use provided pan scale)
                cameraOp = new CameraOperation
                {
                    Type = CameraOperationType.Pan,
                    DeltaX = (float)-dx * panScale,
                    DeltaY = (float)dy * panScale
                };
                needsRefresh = true;
            }

            _lastMousePos = currentPos;

            return new MouseHandlingResult { Handled = false, NeedsRefresh = needsRefresh, CaptureMouse = false };
        }

        private MouseHandlingResult HandleMouseMoveWindowSelection(Point currentPos, Vector3D worldPos)
        {
            // Update the current point of the selection window
            _windowSelectionCurrentPoint = new Point3D(worldPos.X, worldPos.Y, worldPos.Z);

            // Clear previous preview objects
            _windowSelectionPreviewObjects.Clear();

            // Calculate the selection rectangle bounds
            if (_windowSelectionStartPoint.HasValue)
            {
                double minX = Math.Min(_windowSelectionStartPoint.Value.X, _windowSelectionCurrentPoint.Value.X);
                double maxX = Math.Max(_windowSelectionStartPoint.Value.X, _windowSelectionCurrentPoint.Value.X);
                double minY = Math.Min(_windowSelectionStartPoint.Value.Y, _windowSelectionCurrentPoint.Value.Y);
                double maxY = Math.Max(_windowSelectionStartPoint.Value.Y, _windowSelectionCurrentPoint.Value.Y);

                // Collect all drawable objects
                var drawableObjects = new List<OpenCADObject>();
                CollectDrawableObjects(ObjectToDisplay, drawableObjects);

                // Test each object to see if it's fully inside the selection rectangle
                foreach (var obj in drawableObjects)
                {
                    if (IsObjectInsideRectangle(obj, minX, maxX, minY, maxY))
                    {
                        _windowSelectionPreviewObjects.Add(obj);
                    }
                }

                OnPropertyChanged(nameof(WindowSelectionPreviewObjects));
            }

            _lastMousePos = currentPos;
            return new MouseHandlingResult { Handled = true, NeedsRefresh = true, CaptureMouse = true };
        }

        /// <summary>
        /// Handle mouse wheel event
        /// </summary>
        public CameraOperation HandleMouseWheel(int delta)
        {
            float zoomDelta = delta > 0 ? 0.5f : -0.5f;
            return new CameraOperation
            {
                Type = CameraOperationType.Zoom,
                DeltaX = zoomDelta,
                DeltaY = 0
            };
        }

        /// <summary>
        /// Perform hit testing with pickbox to find objects near the cursor
        /// </summary>
        public OpenCADObject? HitTest(Point screenPos, Func<Point, Vector3?> screenToWorld)
        {
            return HitTest(screenPos, screenToWorld, (_viewportSettings?.Crosshair?.PickboxSize ?? 5.0)).FirstOrDefault();
        }

        /// <summary>
        /// Perform hit testing with pickbox to find objects near the cursor
        /// </summary>
        public IEnumerable<OpenCADObject> HitTest(Point screenPos, Func<Point, Vector3?> screenToWorld, double boxSize)
        {
            // Collect all drawable objects
            var curveObjects = new List<OpenCADObject>();
            CollectCurveObjects(ObjectToDisplay, curveObjects);
            List<OpenCADObject> hitObjects = new List<OpenCADObject>();
            var worldPos = screenToWorld(screenPos);
            if (worldPos is null || curveObjects.Count < 1)
                return hitObjects;

            var c1 = screenToWorld(new Point(screenPos.X - boxSize, screenPos.Y - boxSize));
            var c2 = screenToWorld(new Point(screenPos.X + boxSize, screenPos.Y + boxSize));

            if (c1 is null || c2 is null)
                return hitObjects;

            // Test each object against the pickbox
            foreach (var obj in curveObjects)
            {
                if (obj is ICurve curve)
                {
                    var pt = curve.GetClosestPoint(new Point3D(worldPos.Value.X, worldPos.Value.Y, 0));
                    if (pt.IsValid)
                    {
                        if (pt.X >= c1.Value.X && pt.X <= c2.Value.X &&
                            pt.Y <= c1.Value.Y && pt.Y >= c2.Value.Y)
                        {
                            hitObjects.Add(obj);
                        }
                    }
                }
            }

            return hitObjects;
        }

        /// <summary>
        /// Recursively collect all drawable objects from the scene
        /// </summary>
        private void CollectDrawableObjects(OpenCADObject parent, List<OpenCADObject> list)
        {
            var children = parent.GetChildren();
            foreach (var child in children)
            {
                if (child.IsDrawable)
                    list.Add(child);

                CollectDrawableObjects(child, list);
            }
        }

        /// <summary>
        /// Recursively collect all drawable objects from the scene
        /// </summary>
        private void CollectCurveObjects(OpenCADObject parent, List<OpenCADObject> list)
        {
            var children = parent.GetChildren();
            foreach (var child in children)
            {
                if (child is ICurve)
                    list.Add(child);

                CollectCurveObjects(child, list);
            }
        }

        #endregion

        #region Private Methods

        private void UpdateCursor()
        {
            if (CurrentInputMode == InputMode.PointPicking)
            {
                CurrentCursor = Cursors.Cross;
            }
            else if (CurrentInputMode == InputMode.Selection)
            {
                CurrentCursor = HighlightedObject != null ? Cursors.Hand : Cursors.Arrow;
            }
            else
            {
                CurrentCursor = Cursors.Arrow;
            }
        }

        /// <summary>
        /// Determines if an object is fully inside the selection rectangle (window selection)
        /// </summary>
        private bool IsObjectInsideRectangle(OpenCADObject obj, double minX, double maxX, double minY, double maxY)
        {
            if (obj is GeometryBase geometry)
            {
                // For other drawable objects, try to get their bounds
                // This is a simplified check - you may need to implement proper bounds checking
                var extents = geometry.GetExtents();
                return extents.Min.X >= minX && extents.Max.X <= maxX &&
                        extents.Min.Y >= minY && extents.Max.Y <= maxY;
            }

            return false;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Viewport Settings

        /// <summary>
        /// Set the viewport settings
        /// </summary>
        public void SetViewportSettings(ViewportSettings settings)
        {
            _viewportSettings = settings;
        }

        /// <summary>
        /// Update snapping state from settings
        /// </summary>
        public void UpdateSnappingFromSettings()
        {
            //if (_viewportSettings?.Snap != null)
            //{
            //    SnappingEnabled = _viewportSettings.Snap.SnapEnabled;
            //    GridSize = _viewportSettings.Snap.SnapSpacing;
            //    //System.Diagnostics.Debug.WriteLine($"Snapping updated from settings: Enabled={SnappingEnabled}, GridSize={GridSize}");
            //}
        }

        internal MouseHandlingResult HandleMouseUp(MouseButton button, Point point, Vector3? vector3)
        {
            // Handle window selection completion
            if (IsWindowSelectionMode && button == MouseButton.Left)
            {
                // Select all preview objects
                foreach (var obj in _windowSelectionPreviewObjects)
                {
                    if (!_selectedObjects.Contains(obj))
                    {
                        _selectedObjects.Add(obj);
                    }
                }

                // Clear window selection state
                _windowSelectionStartPoint = null;
                _windowSelectionCurrentPoint = null;
                _windowSelectionPreviewObjects.Clear();

                // Return to previous mode (Selection)
                CurrentInputMode = _previousSelectionMode != InputMode.None ? _previousSelectionMode : InputMode.Selection;

                // Notify of selection changes
                if (_selectedObjects.Count > 0)
                {
                    OnPropertyChanged(nameof(SelectedObjects));
                    OnPropertyChanged(nameof(WindowSelectionPreviewObjects));
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }

                return new MouseHandlingResult { Handled = true, NeedsRefresh = true, CaptureMouse = false };
            }

            return new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = false };
        }

        internal IEnumerable<GeoPoint> GetGeoPointsAtCurrentMousePosition(Point screenPos, Func<Point, Vector3?> screenToWorld)
        {
            _geoPoints.Clear();
            if (screenToWorld == null)
                return _geoPoints;

            var worldPos = screenToWorld(screenPos);
            if (!worldPos.HasValue)
                return _geoPoints;

            var aperture = _viewportSettings?.ApertureSize ?? 15.0;
            var hitObjects = HitTest(screenPos, screenToWorld, aperture);

            var point = SnapToGrid(new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z));

            foreach (var hitObject in hitObjects)
            {
                if (hitObject is GeometryBase geometry)
                {
                    _geoPoints.AddRange(geometry.GetGeoPoints(point, _viewportSettings?.GeoPointModes ?? GeoPointModes.None));
                }
            }

            return _geoPoints;
        }

        internal GeoPoint? GetClosestGeoPoint(IEnumerable<GeoPoint> geoPoints, Point3D referencePoint)
        {
            return geoPoints.OrderBy(x => referencePoint.DistanceTo(x.Position)).FirstOrDefault();
        }

        internal IEnumerable<OpenCADObject> CreateGeoPointGlyphs(IEnumerable<GeoPoint> geoPoints, double scaleFactor)
        {
            foreach (var geoPoint in geoPoints)
            {
                var glyph = CreateGlyph(geoPoint, scaleFactor);
                if (glyph != null)
                {
                    yield return glyph;
                }
            }
        }

        private OpenCADObject CreateGlyph(GeoPoint geoPoint, double scaleFactor)
        {
            if (geoPoint == null || !geoPoint.Position.IsValid)
                return null;

            try
            {
                return new GeoPointGlyph(geoPoint, scaleFactor, _document);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create glyph for {geoPoint.PointType}: {ex.Message}");
                return null;
            }
        }

        internal void SetCommandInputMode()
        {
            _inputMode = InputMode.CommandInput;
        }

        internal void SetSelectionInputMode()
        {
            _inputMode = InputMode.Selection;
        }

        #endregion

        public void UpdateCameraStatus(Vector3 camPos, Vector3 tarPos)
        {
            _statusBar?.UpdateCameraInfo(camPos, tarPos);
        }
    }

    #region Helper Classes

    /// <summary>
    /// Result of mouse handling
    /// </summary>
    public class MouseHandlingResult
    {
        public bool Handled { get; set; }
        public bool NeedsRefresh { get; set; }
        public bool CaptureMouse { get; set; }
    }

    /// <summary>
    /// Camera operation to be performed
    /// </summary>
    public class CameraOperation
    {
        public CameraOperationType Type { get; set; }
        public float DeltaX { get; set; }
        public float DeltaY { get; set; }
    }

    /// <summary>
    /// Type of camera operation
    /// </summary>
    public enum CameraOperationType
    {
        Orbit,
        Pan,
        Zoom
    }

    /// <summary>
    /// Event args for object events
    /// </summary>
    public class ObjectEventArgs : EventArgs
    {
        public OpenCADObject Object { get; }

        public ObjectEventArgs(OpenCADObject obj)
        {
            Object = obj;
        }
    }

    /// <summary>
    /// Event args for point picked event
    /// </summary>
    public class PointPickedEventArgs : EventArgs
    {
        public Point3D Point { get; }

        public PointPickedEventArgs(Point3D point)
        {
            Point = point;
        }
    }

    /// <summary>
    /// Event args for object selected event
    /// </summary>
    public class ObjectSelectedEventArgs : EventArgs
    {
        public OpenCADObject Object { get; }

        public ObjectSelectedEventArgs(OpenCADObject obj)
        {
            Object = obj;
        }
    }

    /// <summary>
    /// Event args for object clicked event
    /// </summary>
    public class ObjectClickedEventArgs : EventArgs
    {
        public OpenCADObject Object { get; }

        public Point3D PickedPoint { get; }

        public ObjectClickedEventArgs(OpenCADObject obj, Point3D point)
        {
            Object = obj;
            PickedPoint = point;
        }
    }

    #endregion
}