using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Settings;
using UI.Controls.MainWindow;

namespace UI.Controls.Viewport
{
    /// <summary>
    /// ViewModel for ViewportControl
    /// Handles viewport state, point picking, and coordinate transformations
    /// </summary>
    public class ViewportViewModel : INotifyPropertyChanged
    {
        #region Fields

        private readonly OpenCADObject _objectToDisplay;
        private StatusBarControl? _statusBar;
        
        // Point picking state
        private bool _isPointPickingMode = false;
        private readonly List<Point3D> _tempPoints = new();
        private Action<Point3D>? _previewCallback;
        private Point3D? _previewPoint;
        
        // Selection state
        private bool _isSelectionMode = false;
        private OpenCADObject? _highlightedObject;
        private readonly List<OpenCADObject> _selectedObjects = new();
        
        // Snapping state
        private bool _snappingEnabled = false;
        private double _gridSize = 1.0;
        
        // Mouse state
        private Point _lastMousePos;
        
        // Cursor state
        private Cursor _cursor = Cursors.Arrow;

        private ViewportSettings? _viewportSettings;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the object being displayed in this viewport
        /// </summary>
        public OpenCADObject ObjectToDisplay => _objectToDisplay;

        /// <summary>
        /// Gets whether point picking mode is enabled
        /// </summary>
        public bool IsPointPickingMode
        {
            get => _isPointPickingMode;
            private set
            {
                if (_isPointPickingMode != value)
                {
                    _isPointPickingMode = value;
                    OnPropertyChanged();
                    UpdateCursor();
                }
            }
        }

        /// <summary>
        /// Gets whether selection mode is enabled
        /// </summary>
        public bool IsSelectionMode
        {
            get => _isSelectionMode;
            private set
            {
                if (_isSelectionMode != value)
                {
                    _isSelectionMode = value;
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
        public bool SnappingEnabled
        {
            get => _snappingEnabled;
            private set
            {
                if (_snappingEnabled != value)
                {
                    _snappingEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the grid size for snapping
        /// </summary>
        public double GridSize
        {
            get => _gridSize;
            private set
            {
                if (Math.Abs(_gridSize - value) > 0.0001)
                {
                    _gridSize = value;
                    OnPropertyChanged();
                }
            }
        }

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

        #endregion

        #region Constructor

        public ViewportViewModel(OpenCADObject objectToDisplay)
        {
            _objectToDisplay = objectToDisplay ?? throw new ArgumentNullException(nameof(objectToDisplay));
        }

        #endregion

        #region Public Methods - Selection

        /// <summary>
        /// Enable selection mode
        /// </summary>
        public void EnableSelectionMode()
        {
            System.Diagnostics.Debug.WriteLine($"=== EnableSelectionMode called, current state: PickMode={IsPointPickingMode}, SelectMode={IsSelectionMode} ===");
            
            // Don't enable selection mode if point picking is active
            if (_isPointPickingMode)
            {
                System.Diagnostics.Debug.WriteLine("  Selection mode NOT enabled - point picking mode is active");
                return;
            }
            
            IsSelectionMode = true;
            System.Diagnostics.Debug.WriteLine("  Selection mode ENABLED");
        }

        /// <summary>
        /// Disable selection mode
        /// </summary>
        public void DisableSelectionMode()
        {
            System.Diagnostics.Debug.WriteLine($"=== DisableSelectionMode called ===");
            IsSelectionMode = false;
            HighlightedObject = null;
            System.Diagnostics.Debug.WriteLine("  Selection mode DISABLED");
        }

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
                System.Diagnostics.Debug.WriteLine($"Object selected: {obj.GetType().Name}");
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
                System.Diagnostics.Debug.WriteLine($"Object deselected: {obj.GetType().Name}");
            }
        }

        #endregion

        #region Public Methods - Point Picking

        /// <summary>
        /// Enable point picking mode
        /// </summary>
        public void EnablePointPickingMode()
        {
            System.Diagnostics.Debug.WriteLine($"=== EnablePointPickingMode called, current state: PickMode={IsPointPickingMode}, SelectMode={IsSelectionMode} ===");
            
            // Disable selection mode when entering point picking mode
            if (_isSelectionMode)
            {
                System.Diagnostics.Debug.WriteLine("  Disabling selection mode");
                IsSelectionMode = false;
                HighlightedObject = null;
            }
            
            IsPointPickingMode = true;
            System.Diagnostics.Debug.WriteLine($"  Point picking mode ENABLED, _tempPoints.Count={_tempPoints.Count}");
        }

        /// <summary>
        /// Disable point picking mode
        /// </summary>
        public void DisablePointPickingMode()
        {
            System.Diagnostics.Debug.WriteLine($"=== DisablePointPickingMode called ===");
            IsPointPickingMode = false;
            _tempPoints.Clear();
            PreviewPoint = null;
            _previewCallback = null;
            System.Diagnostics.Debug.WriteLine("  Point picking mode DISABLED, temp points cleared");
        }

        /// <summary>
        /// Add a temporary point (for commands that need to track points manually)
        /// </summary>
        public void AddTempPoint(Point3D point)
        {
            _tempPoints.Add(point);
            System.Diagnostics.Debug.WriteLine($"Temp point added: ({point.X:F3}, {point.Y:F3}, {point.Z:F3}), total count: {_tempPoints.Count}");
            OnPropertyChanged(nameof(TempPoints));
        }

        /// <summary>
        /// Clear temporary points
        /// </summary>
        public void ClearTempPoints()
        {
            _tempPoints.Clear();
            OnPropertyChanged(nameof(TempPoints));
        }

        /// <summary>
        /// Enable preview mode with a callback for mouse position updates
        /// </summary>
        public void EnablePreviewMode(Action<Point3D> previewCallback)
        {
            _previewCallback = previewCallback;
            System.Diagnostics.Debug.WriteLine("Preview mode ENABLED");
        }

        /// <summary>
        /// Disable preview mode
        /// </summary>
        public void DisablePreviewMode()
        {
            _previewCallback = null;
            PreviewPoint = null;
            System.Diagnostics.Debug.WriteLine("Preview mode DISABLED");
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
            if (!IsPointPickingMode)
                return;
            
            System.Diagnostics.Debug.WriteLine("CancelPointPicking called - raising PointPickingCancelled event");
            
            // Raise the cancelled event BEFORE disabling the mode
            // This allows commands to clean up properly
            PointPickingCancelled?.Invoke(this, EventArgs.Empty);
            
            // Now disable the mode
            DisablePointPickingMode();
        }

        #endregion

        #region Public Methods - Snapping

        /// <summary>
        /// Enable or disable snapping to grid
        /// </summary>
        public void EnableSnapping(bool enabled, double gridSize = 1.0)
        {
            SnappingEnabled = enabled;
            GridSize = gridSize;
        }

        /// <summary>
        /// Snap a point to the nearest grid intersection
        /// </summary>
        public Point3D SnapToGrid(Point3D point)
        {
            if (!SnappingEnabled)
                return point;

            return new Point3D(
                Math.Round(point.X / GridSize) * GridSize,
                Math.Round(point.Y / GridSize) * GridSize,
                Math.Round(point.Z / GridSize) * GridSize
            );
        }

        #endregion

        #region Public Methods - Object Management

        /// <summary>
        /// Add an object to the scene
        /// </summary>
        public void AddObject(OpenCADObject obj)
        {
            ObjectToDisplay?.Add(obj);

            // Log line details if it's a line
            if (obj is Line line)
            {
                System.Diagnostics.Debug.WriteLine($"  Line: Start({line.Start.X}, {line.Start.Y}, {line.Start.Z}) -> End({line.End.X}, {line.End.Y}, {line.End.Z})");
            }

            ObjectAdded?.Invoke(this, new ObjectEventArgs(obj));
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Remove an object from the scene
        /// </summary>
        public void RemoveObject(OpenCADObject obj)
        {
            // Remove from the document's children
            ObjectToDisplay.Remove(obj);
            
            // If the object is currently selected, remove it from selection
            if (_selectedObjects.Contains(obj))
            {
                _selectedObjects.Remove(obj);
                OnPropertyChanged(nameof(SelectedObjects));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            
            // If the object is highlighted, clear the highlight
            if (HighlightedObject == obj)
            {
                HighlightedObject = null;
            }
            
            // Request a refresh
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clear all objects from the scene
        /// </summary>
        public void ClearObjects()
        {
            System.Diagnostics.Debug.WriteLine("Cleared all objects");
            // Note: OpenCADObject doesn't have a Clear method, so we can't implement this fully
            // This would need to be added to OpenCADObject
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
        public void UpdateStatusBarWithWorldCoordinates(Vector3? worldPos)
        {
            if (worldPos.HasValue)
            {
                _statusBar?.UpdatePositionText($"X: {worldPos.Value.X:F3}  Y: {worldPos.Value.Y:F3}  Z: {worldPos.Value.Z:F3}");
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
            System.Diagnostics.Debug.WriteLine($"HandleMouseDown: Button={button}, PickMode={IsPointPickingMode}, SelectMode={IsSelectionMode}, WorldPos={(worldPos.HasValue ? $"({worldPos.Value.X:F2},{worldPos.Value.Y:F2},{worldPos.Value.Z:F2})" : "null")}");

            // If in point picking mode and left button clicked
            if (IsPointPickingMode && button == MouseButton.Left)
            {
                System.Diagnostics.Debug.WriteLine($"Point picking: screen=({mousePos.X:F2}, {mousePos.Y:F2})");

                if (worldPos.HasValue)
                {
                    var point = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
                    
                    // Apply snapping if enabled
                    if (SnappingEnabled)
                    {
                        var snappedPoint = SnapToGrid(point);
                        System.Diagnostics.Debug.WriteLine($"Snapping: ({point.X:F3}, {point.Y:F3}, {point.Z:F3}) -> ({snappedPoint.X:F3}, {snappedPoint.Y:F3}, {snappedPoint.Z:F3})");
                        point = snappedPoint;
                    }
                    
                    _tempPoints.Add(point);
                    OnPropertyChanged(nameof(TempPoints));

                    System.Diagnostics.Debug.WriteLine($"Point picked and added: ({point.X:F3}, {point.Y:F3}, {point.Z:F3}), total count: {_tempPoints.Count}");

                    // Raise the event
                    PointPicked?.Invoke(this, new PointPickedEventArgs(point));

                    return new MouseHandlingResult { Handled = true, NeedsRefresh = true, CaptureMouse = false };
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Failed to convert screen to world coordinates in point picking mode");
                }

                return new MouseHandlingResult { Handled = true, NeedsRefresh = false, CaptureMouse = false };
            }
            else if (IsPointPickingMode && button == MouseButton.Right)
            {
                System.Diagnostics.Debug.WriteLine("Point picking cancelled by right-click");
                // Raise a "cancelled" event
                PointPickingCancelled?.Invoke(this, EventArgs.Empty);
                return new MouseHandlingResult { Handled = true, NeedsRefresh = false, CaptureMouse = false };
            }

            // If in selection mode and left button clicked
            if (IsSelectionMode && button == MouseButton.Left)
            {
                System.Diagnostics.Debug.WriteLine($"Selection mode click: HighlightedObject={(HighlightedObject?.GetType().Name ?? "null")}, SelectedObjectsCount={_selectedObjects.Count}");
                if (HighlightedObject != null)
                {
                    // Toggle selection of the highlighted object
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
            }

            // Normal mouse handling for camera control
            _lastMousePos = mousePos;
            System.Diagnostics.Debug.WriteLine("Mouse down handled as camera control");
            return new MouseHandlingResult { Handled = false, NeedsRefresh = false, CaptureMouse = true };
        }

        /// <summary>
/// Handle mouse move event
/// </summary>
public MouseHandlingResult HandleMouseMove(Point currentPos, Vector3? worldPos, MouseButtonState middleButton, MouseButtonState rightButton, bool isShiftPressed, float panScale, out CameraOperation? cameraOp)
{
    cameraOp = null;
    double dx = currentPos.X - _lastMousePos.X;
    double dy = currentPos.Y - _lastMousePos.Y;

    // Update status bar
    if (worldPos.HasValue)
    {
        // If in point picking mode with snapping enabled, show snapped coordinates
        if (IsPointPickingMode && SnappingEnabled)
        {
            var rawPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
            var snappedPoint = SnapToGrid(rawPoint);
            
            // Update status bar with snapped coordinates
            var snappedVector = new Vector3((float)snappedPoint.X, (float)snappedPoint.Y, (float)snappedPoint.Z);
            UpdateStatusBarWithWorldCoordinates(snappedVector);
        }
        else
        {
            // Show raw world coordinates
            UpdateStatusBarWithWorldCoordinates(worldPos);
        }

        // Call preview callback during point picking AND update preview point
        if (IsPointPickingMode && _previewCallback != null)
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
    }
    else
    {
        UpdateStatusBarWithScreenCoordinates(currentPos);
    }

    // Don't do camera manipulation in point picking mode
    if (IsPointPickingMode)
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
            // Create pickbox boundary (5 pixels in each direction)
            double pickboxSize = _viewportSettings?.Crosshair?.PickboxSize ?? 5.0;
            var pickboxPoints = new[]
            {
                new Point(screenPos.X - pickboxSize, screenPos.Y - pickboxSize),
                new Point(screenPos.X + pickboxSize, screenPos.Y - pickboxSize),
                new Point(screenPos.X + pickboxSize, screenPos.Y + pickboxSize),
                new Point(screenPos.X - pickboxSize, screenPos.Y + pickboxSize),
                new Point(screenPos.X, screenPos.Y) // Center point
            };

            // Collect all drawable objects
            var drawableObjects = new List<OpenCADObject>();
            CollectDrawableObjects(ObjectToDisplay, drawableObjects);

            // Test each object against the pickbox
            foreach (var obj in drawableObjects)
            {
                if (obj is Line line)
                {
                    // Check if the line intersects the pickbox
                    foreach (var pickPoint in pickboxPoints)
                    {
                        var worldPos = screenToWorld(pickPoint);
                        if (worldPos.HasValue && IsPointNearLine(worldPos.Value, line, pickboxSize / 100.0))
                        {
                            return obj;
                        }
                    }
                }
                // Add more geometry types here as needed
            }

            return null;
        }

        /// <summary>
        /// Check if a point is near a line within a tolerance
        /// </summary>
        private bool IsPointNearLine(Vector3 point, Line line, double tolerance)
        {
            var p = new Vector3((float)point.X, (float)point.Y, (float)point.Z);
            var a = new Vector3((float)line.Start.X, (float)line.Start.Y, (float)line.Start.Z);
            var b = new Vector3((float)line.End.X, (float)line.End.Y, (float)line.End.Z);

            var ab = b - a;
            var ap = p - a;

            // Project point onto line
            var t = Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab);
            t = Math.Clamp(t, 0, 1); // Clamp to line segment

            var closest = a + t * ab;
            var distance = Vector3.Distance(p, closest);

            return distance <= tolerance;
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

        #endregion

        #region Private Methods

        private void UpdateCursor()
        {
            if (IsPointPickingMode)
            {
                CurrentCursor = Cursors.Cross;
            }
            else if (IsSelectionMode)
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
            
            // Initialize snapping from settings
            if (_viewportSettings?.Snap != null)
            {
                SnappingEnabled = _viewportSettings.Snap.SnapEnabled;
                GridSize = _viewportSettings.Snap.SnapSpacing;
            }
        }

        /// <summary>
        /// Update snapping state from settings
        /// </summary>
        public void UpdateSnappingFromSettings()
        {
            if (_viewportSettings?.Snap != null)
            {
                SnappingEnabled = _viewportSettings.Snap.SnapEnabled;
                GridSize = _viewportSettings.Snap.SnapSpacing;
                System.Diagnostics.Debug.WriteLine($"Snapping updated from settings: Enabled={SnappingEnabled}, GridSize={GridSize}");
            }
        }

        #endregion
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

    #endregion
}