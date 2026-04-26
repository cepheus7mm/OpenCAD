using GraphicsEngine;
using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Geometry.Helpers.GeoPoints;
using OpenCAD.Geometry.Helpers.GeoPoints.GeoPointProviders;
using OpenCAD.Geometry.Helpers.Snaps;
using OpenCAD.Grips;
using OpenCAD.Grips.GripProviders;
using OpenCAD.Interfaces;
using OpenCAD.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
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
        public enum MouseMoveState
        {
            WindowSelection,
            GripDrag,
            HoverUpdate,
            PointPicking,
            CameraPan,
            CameraOrbit,
            None
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

        // Rectangle preview state (used by GetRectangleInput)
        private Point3D? _rectanglePreviewStartPoint;
        private Point3D? _rectanglePreviewCurrentPoint;
        private System.Drawing.Color _rectanglePreviewEdgeColor;
        private System.Drawing.Color _rectanglePreviewFillColor;
        private Point3D? _previewPoint;

        private IHitTester? _hitTester;
        private SnapManager _snapManager;
        private IGripManager _gripManager;
        private GripRenderer _gripRenderer;

        // Selection state
        //private bool _isSelectionMode = false;
        private OpenCADObject? _highlightedObject;

        // Mouse state
        private Point _lastMousePos;
        private Point _mouseDownPos;

        // Cursor state
        private Cursor _cursor = Cursors.Arrow;

        private ViewportSettings _viewportSettings;

        private IGeoPointManager _geoPointManager;
        private ISelectionManager _selectionManager;
        private IGripProviderFactory? _gripProviderFactory;
        private IGeoPointProviderFactory? _geoPointProviderFactory;

        #endregion

        #region Properties

        private readonly HashSet<OpenCADObject> _visibleObjects = new();

        public IEnumerable<OpenCADObject> VisibleObjects => _visibleObjects;

        private InputMode _previousSelectionMode = InputMode.None;
        private RenderEngine _renderEngine;
        private bool _mouseDownOnGrip;
        private bool _isDragging;
        private Vector3D? _lastWorldPos;
        private bool _isShiftKeyPressed;
        private bool _isCtrlKeyPressed;
        private string _diagnosticToolTip;
        private string _appendedDiagnosticToolTip = string.Empty;
        private Point3D _pickPoint = Point3D.NotAPoint;

        /// <summary>
        /// Gets whether point picking mode is enabled
        /// </summary>
        public bool IsPointPickingMode => _inputMode == InputMode.PointPicking;


        /// <summary>
        /// Gets whether selection mode is enabled
        /// </summary>
        public bool IsSelectionMode => _inputMode == InputMode.Selection;

        public bool IsWindowSelectionMode => _inputMode == InputMode.WindowSelection;

        public string DiagnosticToolTip
        {
            get => _diagnosticToolTip;
            set
            {
                if (_diagnosticToolTip != value)
                {
                    _diagnosticToolTip = value;
                    _diagnosticToolTip += _appendedDiagnosticToolTip;
                    OnPropertyChanged();
                }
            }
        }

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

        public IPreviewManager PreviewManager { get; }

        public ISelectionManager SelectionManager => _selectionManager;

        /// <summary>
        /// Gets the currently highlighted object (hover)
        /// </summary>
        public OpenCADObject? HighlightedObject
        {
            get => _highlightedObject;
            internal set
            {
                if (_highlightedObject != value)
                {
                    _highlightedObject = value;
                    OnPropertyChanged();
                    RefreshRequested?.Invoke(this, EventArgs.Empty);
                    var args = new ObjectHoverEventArgs(_highlightedObject, _pickPoint);
                    ObjectHovered?.Invoke(this, args);
                }
            }
        }

        /// <summary>
        /// Gets the list of selected objects
        /// </summary>
        public IReadOnlyList<OpenCADObject> SelectedObjects => _selectionManager?.SelectedObjects?.ToList() ?? new List<OpenCADObject>();

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
                    if (value.HasValue)
                        PreviewPointChanged?.Invoke(this, new PointPickedEventArgs(value.Value));
#if DEBUG
                    if (value.HasValue)
                    {
                        DiagnosticToolTip += Environment.NewLine + $"Preview Point: ({value.Value.X:F3}, {value.Value.Y:F3}, {value.Value.Z:F3})";
                    }
                    else
                    {
                        DiagnosticToolTip += Environment.NewLine + "Preview Point: None";
                    }
#endif
                }
            }
        }

        /// <summary>
        /// Gets whether snapping is enabled
        /// </summary>
        public int ApertureSize
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
                    _snapManager.GridSnapEnabled = value;
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
                    _snapManager.GridSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public Point3D? WindowSelectionStartPoint => _selectionManager.WindowSelectionStart;
        public Point3D? WindowSelectionCurrentPoint => _selectionManager.WindowSelectionCurrent;
        public IReadOnlyList<OpenCADObject> WindowSelectionPreviewObjects => _selectionManager.PreviewObjects.ToList();

        public Point3D? RectanglePreviewStartPoint => _rectanglePreviewStartPoint;
        public Point3D? RectanglePreviewCurrentPoint => _rectanglePreviewCurrentPoint;
        public System.Drawing.Color RectanglePreviewEdgeColor => _rectanglePreviewEdgeColor;
        public System.Drawing.Color RectanglePreviewFillColor => _rectanglePreviewFillColor;

        public void BeginRectanglePreview(Point3D start, System.Drawing.Color edgeColor, System.Drawing.Color fillColor)
        {
            _rectanglePreviewStartPoint = start;
            _rectanglePreviewCurrentPoint = null;
            _rectanglePreviewEdgeColor = edgeColor;
            _rectanglePreviewFillColor = fillColor;
        }

        public void UpdateRectanglePreview(Point3D current)
        {
            _rectanglePreviewCurrentPoint = current;
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        public void EndRectanglePreview()
        {
            _rectanglePreviewStartPoint = null;
            _rectanglePreviewCurrentPoint = null;
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        public bool IsShiftKeyPressed 
        { 
            get => _isShiftKeyPressed; 
            internal set
            {
                if (_isShiftKeyPressed != value)
                {
                    _isShiftKeyPressed = value;
                    _gripManager.UpdateCopyModifier(_isShiftKeyPressed, _isCtrlKeyPressed);
                }
            } 
        }

        public bool IsCtrlKeyPressed
        {
            get => _isCtrlKeyPressed;
            internal set
            {
                if (_isCtrlKeyPressed != value)
                {
                    _isCtrlKeyPressed = value;
                    _gripManager.UpdateCopyModifier(_isShiftKeyPressed, _isCtrlKeyPressed);
                }
            }
        }

        public int PickboxSize 
        { 
            get => _viewportSettings?.Crosshair?.PickboxSize ?? 5;
            set
            {
                if (_viewportSettings != null && _viewportSettings.Crosshair != null && _viewportSettings.Crosshair.PickboxSize != value)
                {
                    _viewportSettings.Crosshair.PickboxSize = value;
                    OnPropertyChanged();
                }
            } 
        }

        public IGripProviderFactory GripProviderFactory
        {
            get => _gripProviderFactory ??= new GripProviderFactory();
            set => _gripProviderFactory ??= value
                ?? throw new ArgumentNullException(nameof(value));
        }

        public IGeoPointProviderFactory GeoPointProviderFactory
        {
            get => _geoPointProviderFactory ??= new GeoPointProviderFactory();
            set => _geoPointProviderFactory ??= value
                ?? throw new ArgumentNullException(nameof(value));
        }

        public OpenCADDocument Document => _document;

        //private IGripProviderFactory RegisterGripProviders()
        //{
        //    var factory = new GripProviderFactory();
        //    // Register grip providers here
        //    factory.Register<Line>(new LineGripProvider());
        //    factory.Register<Circle>(new CircleGripProvider());
        //    factory.Register<Arc>(new ArcGripProvider());
        //    return factory;
        //}

        //private IGeoPointProviderFactory RegisterGeoPointProviders()
        //{
        //    var factory = new GeoPointProviderFactory();
        //    // Register geo point providers here
        //    factory.Register<Line>(new LineGeoPointProvider());
        //    factory.Register<Arc>(new ArcGeoPointProvider());
        //    factory.Register<Circle>(new CircleGeoPointProvider());
        //    factory.Register<Polyline>(new PolylineGeoPointProvider());
        //    return factory;
        //}

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
        public event EventHandler<ObjectHoverEventArgs>? ObjectHovered;

        /// <summary>
        /// Event raised when an object is selected
        /// </summary>
        public event EventHandler<ObjectClickedEventArgs>? ObjectClicked;

        /// <summary>
        /// Event raised when the viewport should be refreshed
        /// </summary>
        public event EventHandler? RefreshRequested;

        public event EventHandler<PointPickedEventArgs> PreviewPointChanged;

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
        public event EventHandler<ObjectSelectedEventArgs>? SelectionChanged;

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

            PreviewManager = new PreviewManager();
            PreviewManager.PreviewAdded += obj => AddObject(obj);
            PreviewManager.PreviewRemoved += obj => RemoveObject(obj);
            PreviewManager.OriginalHidden += obj => RemoveObject(obj);
            PreviewManager.OriginalRestored += obj => AddObject(obj);
            _viewportSettings = objectToDisplay.GetViewportSettings();
        }

        #endregion
        public void AddObject(OpenCADObject obj)
        {
            _visibleObjects.Add(obj);
        }

        public void RemoveObject(OpenCADObject obj)
        {
            _visibleObjects.Remove(obj);
        }

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

        #endregion

        #region Public Methods - Point Picking

        /// <summary>
        /// Enable point picking mode
        /// </summary>
        public void EnablePointPickingMode()
        {
            CurrentInputMode = InputMode.PointPicking;
            _geoPointManager.Clear();
        }

        /// <summary>
        /// Disable point picking mode
        /// </summary>
        public void DisablePointPickingMode()
        {
            CurrentInputMode = InputMode.Selection;
            _geoPointManager.Clear();
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
        }

        /// <summary>
        /// Disable preview mode
        /// </summary>
        public void DisablePreviewMode()
        {
            _previewCallback = null;
            PreviewPoint = null;
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
            _geoPointManager.Clear();
            // Now disable the mode
            DisablePointPickingMode();
        }

        #endregion

        #region Public Methods - Snapping

        /// <summary>
        /// Snap a point to the nearest grid intersection
        /// </summary>
        public Point3D SnapToGrid(Point3D point)
        {
            var applyGridSnap = IsPointPickingMode || _gripManager.IsEditing;
            var applyGeoSnap = IsPointPickingMode || _gripManager.IsEditing;
            var snapped = new Point3D(_snapManager.GetFinalSnapPoint(new Vector2((float)point.X, (float)point.Y), applyGridSnap, applyGeoSnap));
            UpdateStatusBarWithWorldCoordinates(snapped.AsVector3D());

            return snapped;
        }

        public string GetGripTooltip()
        {
            return _gripManager.GetGripStateTooltip();
        }

        #endregion

        #region Public Methods - Object Management

        ///// <summary>
        ///// Add an object to the scene (model-first).
        ///// The document will raise ObjectAdded and the VM will react via subscription.
        ///// </summary>
        //public void AddObject(OpenCADObject obj)
        //{
        //    ObjectToDisplay?.Add(obj);
        //    // Do not raise ObjectAdded/Refresh here — document event handler will do it.
        //}

        ///// <summary>
        ///// Remove an object from the scene (model-first).
        ///// The document will raise ObjectRemoved and the VM will react via subscription.
        ///// </summary>
        //public void RemoveObject(OpenCADObject obj)
        //{
        //    ObjectToDisplay?.Remove(obj);
        //    // Do not raise Refresh here — document event handler will do it.
        //}

        /// <summary>
        /// Document event handlers - update VM state when the canonical model changes.
        /// </summary>
        private void OnDocumentObjectAdded(object? sender, OpenCAD.DocumentObjectEventArgs e)
        {
            if (sender != _document) return;

            AddObject(e.Object);

            // Forward as VM-level event for UI consumers
            ObjectAdded?.Invoke(this, new ObjectEventArgs(e.Object));

            // Single refresh for the change
            RefreshRequested?.Invoke(this, EventArgs.Empty);

            // Notify property changes if selection collections depend on this
            //OnPropertyChanged(nameof(ObjectToDisplay));
        }

        private void OnDocumentObjectRemoved(object? sender, OpenCAD.DocumentObjectEventArgs e)
        {
            if (sender != _document) return;
            _selectionManager.Deselect(e.Object);

            // If the object was highlighted, clear highlight (this triggers RefreshRequested via setter)
            if (HighlightedObject == e.Object)
            {
                HighlightedObject = null;
            }
            RemoveObject(e.Object);
            ObjectRemoved?.Invoke(this, new ObjectEventArgs(e.Object));
            RefreshRequested?.Invoke(this, EventArgs.Empty);

            //OnPropertyChanged(nameof(ObjectToDisplay));
        }

        private void OnDocumentObjectChanged(object? sender, OpenCAD.DocumentObjectEventArgs e)
        {
            if (sender != _document) return;

            // When an existing object is mutated, simply refresh the viewport.
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnSnapSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SnapSettings.SnapEnabled) && _snapManager != null)
            {
                _snapManager.GridSnapEnabled = _viewportSettings!.Snap!.SnapEnabled;
            }
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
            return button switch
            {
                MouseButton.Left => HandleLeftMouseDown(mousePos, worldPos),
                MouseButton.Right => HandleRightMouseDown(mousePos, worldPos),
                MouseButton.Middle => HandleMiddleMouse(mousePos, worldPos),

                _ => new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = true }
            };
        }

        private MouseHandlingResult HandleLeftMouseDown(Point mousePos, Vector3? worldPos)
        {
            _lastMousePos = mousePos;
            _mouseDownPos = mousePos;

            return CurrentInputMode switch
            {
                InputMode.PointPicking => HandleLeftMouseDownPointPicking(mousePos, worldPos),
                InputMode.Selection => HandleLeftMouseDownSelection(mousePos, worldPos),
                InputMode.CommandInput => HandleLeftMouseDownCommandInput(mousePos, worldPos),
                _ => new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = true },
            };
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
            _pickPoint = worldPos.HasValue ? new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z) : Point3D.NotAPoint;

            if (_gripManager.HoverGrip.HasValue)
            {
                var grip = _gripManager.HoverGrip.Value;
                bool shiftDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

                if (shiftDown)
                {
                    // Shift-click always adds, never clears
                    _gripManager.AddToSelection(grip);
                }
                else
                {
                    // If this grip is already selected, do NOT clear selection
                    if (!_gripManager.IsGripSelected(grip))
                    {
                        _gripManager.SelectSingle(grip);
                    }

                    // Mark that mouse is down on a grip (for drag detection)
                    _mouseDownOnGrip = true;
                }

                return new MouseHandlingResult { Handled = true, NeedsRefresh = true };
            }

            if (HighlightedObject != null)
            {
                _selectionManager.ToggleSelection(HighlightedObject);
                var args = new ObjectSelectedEventArgs(HighlightedObject, _pickPoint);
                ObjectSelected?.Invoke(this, args);
                return new MouseHandlingResult { Handled = true, NeedsRefresh = true };
            }

            // Begin window selection
            CurrentInputMode = InputMode.WindowSelection;

            if (worldPos.HasValue)
                _selectionManager.BeginWindowSelection(new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z));
            else
                _selectionManager.BeginWindowSelection(default);

            return new MouseHandlingResult { Handled = true, NeedsRefresh = true, CaptureMouse = true };
        }

        private MouseHandlingResult HandleLeftMouseDownPointPicking(Point mousePos, Vector3? worldPos)
        {
            if (worldPos.HasValue)
            {
                var point = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);

                if (_viewportSettings?.GeoPointModes != GeoPointModes.None && _geoPointManager.CurrentSnap != null)
                {
                    point = _geoPointManager.CurrentSnap.Position;// GetClosestGeoPoint(_geoPoints, point);
                    _geoPointManager.Clear();  // Clear after use to avoid stale snaps
                }
                else if (SnappingEnabled)
                {
                    var snappedPoint = _snapManager.GetFinalSnapPoint(new Vector2(worldPos.Value.X, worldPos.Value.Y));
                    point = new Point3D(snappedPoint);
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
            if (_gripManager.ActiveGrip != null)
            {
                // Release capture BEFORE opening the menu
                if (Mouse.Captured != null)
                    Mouse.Capture(null);

                var menu = BuildGripContextMenu();
                menu.IsOpen = true;
                return new MouseHandlingResult { Handled = true, NeedsRefresh = false, CaptureMouse = false };
            }

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
        private ContextMenu BuildGripContextMenu()
        {
            var menu = new ContextMenu();

            var activeMode = _gripManager.ActiveMode;

            void Add(string label, GripEditMode mode)
            {
                var item = new MenuItem { Header = label };
                item.Click += (_, __) => _gripManager.SetActiveMode(mode);
                item.IsCheckable = true;
                item.IsChecked = (mode == activeMode);
                menu.Items.Add(item);
            }

            Add("Move", GripEditMode.Move);
            Add("Stretch", GripEditMode.Stretch);
            Add("Rotate", GripEditMode.Rotate);
            Add("Scale", GripEditMode.Scale);
            Add("Mirror", GripEditMode.Mirror);
            Add("Lengthen", GripEditMode.Lengthen);
            Add("Copy", GripEditMode.Copy);

            menu.Items.Add(new Separator());

            var cancel = new MenuItem { Header = "Cancel" };
            cancel.Click += (_, __) => _gripManager.CancelEdit();
            menu.Items.Add(cancel);

            return menu;
        }

        private MouseHandlingResult HandleMiddleMouse(Point mousePos, Vector3? worldPos)
        {
            _lastMousePos = mousePos;
            return new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = true };
        }

        private bool MouseMovedBeyondThreshold(Point currentPos)
        {
            const double threshold = 3.0; // pixels
            double dx = currentPos.X - _mouseDownPos.X;
            double dy = currentPos.Y - _mouseDownPos.Y;
            return (dx * dx + dy * dy) > (threshold * threshold);
        }

        private MouseMoveState GetMouseMoveState(
                    Point currentPos,
                    Vector3D? worldPos,
                    MouseButtonState middleButton,
                    MouseButtonState rightButton)
        {
            if (IsWindowSelectionMode && worldPos.HasValue)
                return MouseMoveState.WindowSelection;

            if (_isDragging && _gripManager.ActiveGrip != null)
                return MouseMoveState.GripDrag;

            if (CurrentInputMode == InputMode.PointPicking)
                return MouseMoveState.PointPicking;

            if (middleButton == MouseButtonState.Pressed)
                return _isShiftKeyPressed ? MouseMoveState.CameraOrbit : MouseMoveState.CameraPan;

            return MouseMoveState.HoverUpdate;
        }

        private MouseHandlingResult HandleWindowSelectionMove(Point currentPos, Vector3D worldPos)
        {
            _selectionManager.UpdateWindowSelection(
                new Point3D(worldPos.X, worldPos.Y, worldPos.Z));

            _lastMousePos = currentPos;

            return new MouseHandlingResult
            {
                Handled = true,
                NeedsRefresh = true,
                CaptureMouse = true
            };
        }

        private MouseHandlingResult HandleGripDragMove(Point currentPos, Vector3D worldPos)
        {
            var vec = new Vector2((float)worldPos.X, (float)worldPos.Y);
            _gripManager.UpdateEdit(vec);

            _lastMousePos = currentPos;

            return new MouseHandlingResult
            {
                Handled = true,
                NeedsRefresh = true,
                CaptureMouse = true
            };
        }

        private MouseHandlingResult HandleHoverMove(Point currentPos, Vector3D? worldPos)
        {
            if (_hitTester == null)
                return NoOpResult;

            // Update grip hover
            var point = new System.Drawing.Point((int)currentPos.X, (int)currentPos.Y);
            var hitResult = _hitTester.HitTest(point);
            _gripManager.UpdateHover(hitResult);
# if DEBUG
            DiagnosticToolTip += $"HitResult: {hitResult.Kind}, Entity: {(hitResult.Entity != null ? hitResult.Entity.GetType().Name : "null")}, Grip: {(hitResult.Grip.HasValue ? hitResult.Grip.Value.Kind.ToString() : "null")}" + Environment.NewLine;
            DiagnosticToolTip += _gripManager.GetGripStateTooltip() + Environment.NewLine;
            DiagnosticToolTip += $"Forced snapping: {_snapManager.HasForcedSnap}";
#endif

            if (worldPos.HasValue && _gripManager.HoverGrip is Grip hoverGrip)
            {
                var snapped = new Vector3D(hoverGrip.Position.X, hoverGrip.Position.Y, worldPos.Value.Z);
                UpdateStatusBarWithWorldCoordinates(snapped);
            }

            if (hitResult.Kind == HitResultKind.Entity && hitResult.Entity != null)
            {
                // 1. Update pick point FIRST
                _pickPoint = worldPos.HasValue
                    ? new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z)
                    : Point3D.NotAPoint;

                // 2. Then update highlighted object
                HighlightedObject = hitResult.Entity;
            }
            else
            {
                // Clear hover when nothing is hit
                _pickPoint = Point3D.NotAPoint;
                HighlightedObject = null;
            }

            _lastMousePos = currentPos;

            return new MouseHandlingResult
            {
                Handled = false,
                NeedsRefresh = true,
                CaptureMouse = false
            };
        }

        private MouseHandlingResult HandlePointPickingMove(Point currentPos, Vector3D? worldPos)
        {
            if (worldPos.HasValue)
            {
                var snappedPoint = _snapManager.GetFinalSnapPoint(new Vector2((float)worldPos.Value.X, (float)worldPos.Value.Y));

                var point = new Point3D(snappedPoint);

                PreviewPoint = point;

                _previewCallback?.Invoke(point);
                UpdateStatusBarWithWorldCoordinates(point.AsVector3D());
            }

            _lastMousePos = currentPos;

            return new MouseHandlingResult
            {
                Handled = true,
                NeedsRefresh = true,
                CaptureMouse = false
            };
        }

        private MouseHandlingResult HandleCameraPanMove(
                    Point currentPos,
                    float panScale,
                    out CameraOperation? cameraOp)
        {
            double dx = currentPos.X - _lastMousePos.X;
            double dy = currentPos.Y - _lastMousePos.Y;

            cameraOp = new CameraOperation
            {
                Type = CameraOperationType.Pan,
                DeltaX = (float)-dx * panScale,
                DeltaY = (float)dy * panScale
            };

            _lastMousePos = currentPos;

            return new MouseHandlingResult
            {
                Handled = true,
                NeedsRefresh = true,
                CaptureMouse = true
            };
        }

        private MouseHandlingResult HandleCameraOrbitMove(
                    Point currentPos,
                    out CameraOperation? cameraOp)
        {
            double dx = currentPos.X - _lastMousePos.X;
            double dy = currentPos.Y - _lastMousePos.Y;

            cameraOp = new CameraOperation
            {
                Type = CameraOperationType.Orbit,
                DeltaX = (float)dx * 0.01f,
                DeltaY = (float)dy * 0.01f
            };

            _lastMousePos = currentPos;

            return new MouseHandlingResult
            {
                Handled = true,
                NeedsRefresh = true,
                CaptureMouse = true
            };
        }

        private static readonly MouseHandlingResult NoOpResult = new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = false };


        /// <summary>
        /// Handle mouse move event
        /// </summary>
        public MouseHandlingResult HandleMouseMove(Point currentPos, Vector3D? worldPos, MouseButtonState middleButton, MouseButtonState rightButton, float panScale, out CameraOperation? cameraOp)
        {
            cameraOp = null;

            if (_mouseDownOnGrip && !_isDragging)
            {
                if (MouseMovedBeyondThreshold(currentPos))
                {
                    _isDragging = true;
                    var vec = new Vector2((float)worldPos!.Value.X, (float)worldPos!.Value.Y);
                    _gripManager.BeginEdit(vec);
                }
            }
            UpdateStatusBarWithWorldCoordinates(worldPos);
            _lastWorldPos = worldPos;

            var state = GetMouseMoveState(currentPos, worldPos, middleButton, rightButton);

#if DEBUG
            DiagnosticToolTip += $"MouseMoveState: {state}" + Environment.NewLine;
#endif

            return state switch
            {
                MouseMoveState.WindowSelection => HandleWindowSelectionMove(currentPos, worldPos!.Value),

                MouseMoveState.GripDrag => HandleGripDragMove(currentPos, worldPos!.Value),

                MouseMoveState.HoverUpdate => HandleHoverMove(currentPos, worldPos),

                MouseMoveState.PointPicking => HandlePointPickingMove(currentPos, worldPos),

                MouseMoveState.CameraPan => HandleCameraPanMove(currentPos, panScale, out cameraOp),

                MouseMoveState.CameraOrbit => HandleCameraOrbitMove(currentPos, out cameraOp),

                _ => NoOpResult
            };

        }

        private MouseHandlingResult HandleMouseMoveWindowSelection(Point currentPos, Vector3D worldPos)
        {
            _selectionManager.UpdateWindowSelection(
                new Point3D(worldPos.X, worldPos.Y, worldPos.Z));

            _lastMousePos = currentPos;

            return new MouseHandlingResult
            {
                Handled = true,
                NeedsRefresh = true,
                CaptureMouse = true
            };
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

        public void Initialize(ICamera camera)
        {
            _selectionManager = new SelectionManager(_document);
            _hitTester = new HitTester(_document, _selectionManager, GripProviderFactory, camera,
                () => PickboxSize,
                () => _viewportSettings?.GripSize ?? 10);
            _snapManager = new SnapManager(GeoPointProviderFactory, camera, () => VisibleObjects);
            _snapManager.GridSnapEnabled = SnappingEnabled;
            _snapManager.GridSize = SnapSize;
            _snapManager.ApertureSize = ApertureSize;
            _snapManager.SnapPointChanged += OnSnapPointChanged;
            _geoPointManager = new GeoPointManager(GeoPointProviderFactory);
            _gripManager = new GripManager(_document, _hitTester, GripProviderFactory, PreviewManager, _snapManager, _selectionManager);
            _selectionManager.SelectionChanged += OnSelectionChanged;
            _viewportSettings.Snap.PropertyChanged += OnSnapSettingsPropertyChanged;
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            var args = new ObjectSelectedEventArgs(_selectionManager.SelectedObjects.FirstOrDefault(), _pickPoint);
            SelectionChanged?.Invoke(this, args);
        }

        private void OnSnapPointChanged(SnapPoint? point)
        {
            if (point != null)
            {
                UpdateStatusBarWithWorldCoordinates(new Point3D(point.Position).AsVector3D());//this will always show Z as 0
            }
            else
            {
                // No object snap — use cursor snap only
                var world = new Vector2((float)_lastWorldPos!.Value.X, (float)_lastWorldPos!.Value.Y);
                world = _snapManager.ApplyCursorSnap(world);
                var world3D = new Vector3D(world.X, world.Y, 0);
                UpdateStatusBarWithWorldCoordinates(world3D);
            }
        }

        /// <summary>
        /// Perform hit testing with pickbox to find objects near the cursor
        /// </summary>
        public OpenCADObject? HitTest(Point screenPos, Func<Point, Vector3?> screenToWorld)
        {
            var hit = _hitTester?.HitTest(new System.Drawing.Point((int)screenPos.X, (int)screenPos.Y));
            _gripManager.UpdateHover(hit);

            return hit!.Value.Entity;
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

        internal MouseHandlingResult HandleMouseUp(MouseButton button, Point point, Vector3? worldPos)
        {
            // ------------------------------------------------------------
            // 1. WINDOW SELECTION COMMIT
            // ------------------------------------------------------------
            if (IsWindowSelectionMode && button == MouseButton.Left)
            {
                _selectionManager.CommitWindowSelection();

                CurrentInputMode = _previousSelectionMode != InputMode.None
                    ? _previousSelectionMode
                    : InputMode.Selection;

                return new MouseHandlingResult
                {
                    Handled = true,
                    NeedsRefresh = true,
                    CaptureMouse = false
                };
            }

            // ------------------------------------------------------------
            // 2. GRIP DRAG COMMIT
            // ------------------------------------------------------------
            if (_isDragging)
            {
                var newEntity = _gripManager.CommitEdit();

                if (newEntity != null)
                {
                    // Replace selection with the new entity
                    _selectionManager.ClearSelection();
                    _selectionManager.AddToSelection(newEntity);
                }

                _isDragging = false;
                _mouseDownOnGrip = false;

                return new MouseHandlingResult
                {
                    Handled = true,
                    NeedsRefresh = true
                };
            }


            // ------------------------------------------------------------
            // 3. MOUSE DOWN ON GRIP BUT NO DRAG
            // (Click on grip already handled in MouseDown)
            // ------------------------------------------------------------
            if (_mouseDownOnGrip)
            {
                _mouseDownOnGrip = false;

                return new MouseHandlingResult
                {
                    Handled = true,
                    NeedsRefresh = true,
                    CaptureMouse = false
                };
            }

            // ------------------------------------------------------------
            // 4. DEFAULT FALLTHROUGH
            // ------------------------------------------------------------
            return new MouseHandlingResult
            {
                Handled = false,
                NeedsRefresh = false,
                CaptureMouse = false
            };
        }

        internal GeoPoint? GetGeoPointAtCurrentMousePosition(Point screenPos, Func<Point, Vector3?> screenToWorld)
        {
            if (screenToWorld == null || _viewportSettings == null)
                return null;

            var worldPos = screenToWorld(screenPos);
            if (!worldPos.HasValue)
                return null;

            float aperture = _viewportSettings?.ApertureSize ?? 15;
            aperture *= _renderEngine.Viewport.DpiScaleX;
            var hitObjects = _hitTester!.HitTestEntities(new System.Drawing.Point((int)screenPos.X, (int)screenPos.Y), (int)aperture); // HitTest(screenPos, screenToWorld, aperture);

            var point = new Point3D(_snapManager.GetFinalSnapPoint(new (worldPos.Value.X, worldPos.Value.Y)));

            if (!hitObjects.Any() || _viewportSettings == null || _viewportSettings.GeoPointModes == GeoPointModes.None)
                return null;
            var gp = _geoPointManager.GetBestGeoPoint(point, hitObjects, _viewportSettings.GeoPointModes, aperture * ScreenToWorldScaleX(screenToWorld, screenPos));
            //_geoPointManager.Clear();

            return gp;
        }

        double ScreenToWorldScaleX(Func<Point, Vector3?> screenToWorld, Point screenPoint)
        {
            var w0 = screenToWorld(screenPoint);
            var w1 = screenToWorld(new Point(screenPoint.X + 1, screenPoint.Y));

            if (!w0.HasValue || !w1.HasValue)
                return 1;

            return Math.Abs((w1.Value - w0.Value).Length());
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

        internal OpenCADObject CreateGlyph(GeoPoint geoPoint, double scaleFactor)
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

#if DEBUG
        public void AppendDiagnosticInfo(string info)
        {
            _appendedDiagnosticToolTip = string.Empty; // Environment.NewLine + info + Environment.NewLine;
        }
#endif

        internal void RenderGrips()
        {
            var grips = CollectGripVisuals();

            GripVisual? hover = null;
            GripVisual? active = null;

            if (_gripManager.HoverGrip is Grip hg)
                hover = new GripVisual(hg.Position, GripStyles.Hover);

            if (_gripManager.ActiveGrip is Grip ag)
                active = new GripVisual(ag.Position, GripStyles.Active);

            _gripRenderer.RenderGrips(grips, hover, active);
        }

        private IEnumerable<GripVisual> CollectGripVisuals()
        {
            foreach (var obj in _selectionManager.SelectedObjects)
            {
                // Get provider from factory (new architecture)
                var provider = GripProviderFactory.GetProvider(obj);
                if (provider == null)
                    continue;

                foreach (var grip in provider.GetGrips(obj))
                {
                    // Skip hover/active grips — they are drawn separately
                    if (_gripManager.HoverGrip?.Equals(grip) == true)
                        continue;

                    if (_gripManager.ActiveGrip?.Equals(grip) == true)
                        continue;

                    // Selected grips
                    if (_gripManager.IsGripSelected(grip))
                    {
                        yield return new GripVisual(grip.Position, GripStyles.Selected);
                    }
                    else
                    {
                        yield return new GripVisual(grip.Position, GripStyles.Inactive);
                    }
                }
            }
        }


        internal void SetRenderEngine(RenderEngine renderEngine)
        {
            _renderEngine = renderEngine;
            _gripRenderer = new GripRenderer(_renderEngine.Viewport);
        }

        internal void RenderGripPreviewObjects(RenderEngine renderEngine)
        {
            var previewObjects = _gripManager.GetPreviewObjects();
            renderEngine.Render(previewObjects.Values, new HashSet<OpenCADObject>(), new HashSet<OpenCADObject>(), RenderStyle.Preview);
        }

        internal bool HandleEscapeKey()
        {
            _gripManager.CancelEdit();
            // Priority 1: Cancel point picking mode if active
            if (IsPointPickingMode)
            {
                //System.Diagnostics.Debug.WriteLine("ESC handled - cancelling point picking mode");
                CancelPointPicking();
                return true;
            }

            // Priority 2: Clear selection if there are selected objects
            if (IsSelectionMode && SelectedObjects.Count > 0)
            {
                //System.Diagnostics.Debug.WriteLine("ESC handled - clearing selection");
                SelectionManager.ClearSelection();
                return true;
            }

            return false;
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
        public Point3D? PickedPoint { get; }

        public ObjectSelectedEventArgs(OpenCADObject obj, Point3D? pickedPoint = null)
        {
            Object = obj;
            PickedPoint = pickedPoint;
        }
    }

    /// <summary>
    /// Event args for object selected event
    /// </summary>
    public class ObjectHoverEventArgs : EventArgs
    {
        public OpenCADObject Object { get; }
        public Point3D? PickedPoint { get; }

        public ObjectHoverEventArgs(OpenCADObject obj, Point3D? pickedPoint = null)
        {
            Object = obj;
            PickedPoint = pickedPoint;
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