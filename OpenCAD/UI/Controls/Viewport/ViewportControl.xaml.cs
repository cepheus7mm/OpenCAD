using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using OpenCAD;
using OpenCAD.Geometry;
using GraphicsEngine;
using OpenTK.Windowing.Common;
using UI.Controls.MainWindow;
using System.Numerics;
using System.Collections.Generic;

namespace UI.Controls.Viewport
{
    public partial class ViewportControl : UserControl
    {
        private RenderEngine? _renderEngine;
        private bool _isInitialized = false;
        private Point _lastMousePos;
        private bool _firstRender = true;
        private StatusBarControl? _statusBar;
        private readonly OpenCADObject _objectToDisplay;
        private bool _isPointPickingMode = false;
        private List<Point3D> _tempPoints = new();
        private bool _snappingEnabled = false;
        private double _gridSize = 1.0;

        // Event for point picking
        public event EventHandler<PointPickedEventArgs>? PointPicked;
        public event EventHandler? PointPickingCancelled;

        private Action<Point3D>? _previewCallback;
        private Point3D? _previewPoint;

        public List<Point3D> TempPoints => _tempPoints;

        public ViewportControl(OpenCADObject objectToDisplay)
        {
            if (objectToDisplay == null)
                throw new ArgumentNullException(nameof(objectToDisplay));

            _objectToDisplay = objectToDisplay;

            InitializeComponent();

            System.Diagnostics.Debug.WriteLine("=== ViewportControl constructor called ===");

            // GlControl is already defined in XAML with x:Name="GlControl", so no need to find it
            if (GlWPFControl == null)
                throw new InvalidOperationException("GlControl not found. Make sure it is defined in XAML with x:Name=\"GlWPFControl\".");

            var settings = new GLWpfControlSettings
            {
                MajorVersion = 3,
                MinorVersion = 3,
                RenderContinuously = false, // Only render on-demand
            };

            GlWPFControl.Start(settings);
            System.Diagnostics.Debug.WriteLine("GLWpfControl.Start() called with RenderContinuously = false");

            // Subscribe to events
            GlWPFControl.Render += OnRender;
            GlWPFControl.SizeChanged += OnSizeChanged;
            GlWPFControl.Ready += OnGLControlReady;
            this.Loaded += ViewportControl_Loaded;

            // Mouse events
            GlWPFControl.MouseDown += OnMouseDown;
            GlWPFControl.MouseMove += OnMouseMove;
            GlWPFControl.MouseWheel += OnMouseWheel;
            GlWPFControl.MouseUp += OnMouseUp;
            GlWPFControl.MouseEnter += OnMouseEnter;
            GlWPFControl.MouseLeave += OnMouseLeave;

            System.Diagnostics.Debug.WriteLine("=== ViewportControl constructor completed ===");
        }

        /// <summary>
        /// Enable point picking mode
        /// </summary>
        public void EnablePointPickingMode()
        {
            _isPointPickingMode = true;
            // Don't clear temp points here - let the command manage them
            GlWPFControl.Cursor = Cursors.Cross;
            System.Diagnostics.Debug.WriteLine($"Point picking mode ENABLED, _tempPoints.Count={_tempPoints.Count}");
        }

        /// <summary>
        /// Disable point picking mode
        /// </summary>
        public void DisablePointPickingMode()
        {
            _isPointPickingMode = false;
            _tempPoints.Clear();  // Clear temp points when picking is done
            GlWPFControl.Cursor = Cursors.Arrow;
            System.Diagnostics.Debug.WriteLine("Point picking mode DISABLED, temp points cleared");
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
            _previewPoint = null;
            System.Diagnostics.Debug.WriteLine("Preview mode DISABLED");
        }

        /// <summary>
        /// Set the preview point for rendering
        /// </summary>
        public void SetPreviewPoint(Point3D? point)
        {
            _previewPoint = point;
            Refresh();
        }

        /// <summary>
        /// Set the status bar control to display mouse coordinates
        /// </summary>
        public void SetStatusBar(StatusBarControl statusBar)
        {
            _statusBar = statusBar;
        }

        private void ViewportControl_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== ViewportControl.Loaded event fired ===");

            // Initialize OpenGL if not already initialized
            if (!_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("Initializing from Loaded event");
                InitializeOpenGL();
            }
        }

        private void OnGLControlReady()
        {
            System.Diagnostics.Debug.WriteLine("=== GLWpfControl.Ready event fired ===");

            // Check OpenGL version and capabilities
            try
            {
                string version = GL.GetString(StringName.Version);
                string vendor = GL.GetString(StringName.Vendor);
                string renderer = GL.GetString(StringName.Renderer);
                string glslVersion = GL.GetString(StringName.ShadingLanguageVersion);

                System.Diagnostics.Debug.WriteLine($"OpenGL Version: {version}");
                System.Diagnostics.Debug.WriteLine($"Vendor: {vendor}");
                System.Diagnostics.Debug.WriteLine($"Renderer: {renderer}");
                System.Diagnostics.Debug.WriteLine($"GLSL Version: {glslVersion}");

                // Check if we have OpenGL 3.3 or higher
                var versionParts = version.Split('.', ' ');
                if (versionParts.Length >= 2)
                {
                    int major = int.Parse(versionParts[0]);
                    int minor = int.Parse(versionParts[1]);
                    System.Diagnostics.Debug.WriteLine($"Parsed version: {major}.{minor}");

                    if (major < 3 || (major == 3 && minor < 3))
                    {
                        System.Diagnostics.Debug.WriteLine("WARNING: OpenGL 3.3 or higher is recommended");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting OpenGL info: {ex.Message}");
            }

            InitializeOpenGL();
        }

        private void InitializeOpenGL()
        {
            if (_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("OpenGL already initialized, skipping");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("--- Starting OpenGL initialization ---");
                
                _renderEngine = new RenderEngine();
                var width = (int)GlWPFControl.ActualWidth;
                var height = (int)GlWPFControl.ActualHeight;
                
                // Ensure we have valid dimensions
                if (width <= 0) width = 800;
                if (height <= 0) height = 600;
                
                System.Diagnostics.Debug.WriteLine($"Viewport dimensions: {width}x{height}");
                
                _renderEngine.Initialize(width, height);
                
                // TEMPORARILY DISABLE DEPTH TEST TO DEBUG
                GL.Disable(EnableCap.DepthTest);
                
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
                
                // Check for any OpenGL errors during initialization
                var error = GL.GetError();
                if (error != ErrorCode.NoError)
                {
                    System.Diagnostics.Debug.WriteLine($"OpenGL Error during initialization: {error}");
                }
                
                _isInitialized = true;
                
                // Force a refresh to render any objects that were added before initialization
                System.Diagnostics.Debug.WriteLine("--- Forcing initial refresh ---");
                Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EXCEPTION during OpenGL initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private void OnRender(TimeSpan delta)
        {
            if (!_isInitialized || _renderEngine == null)
            {
                return;
            }

            try
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                RenderOpenCADObjects(ObjectToDisplay);
                
                // ADD THIS LINE - Render preview geometry
                RenderPreviewGeometry();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! EXCEPTION during render: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void RenderOpenCADObjects(OpenCADObject? objectToDisplay)
        {
            if (objectToDisplay != null)
            {
                var children = objectToDisplay.GetChildren();
                foreach (var child in children)
                {
                    RenderOpenCADObjects(child);
                }
                _renderEngine?.Render(children);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnSizeChanged: {e.NewSize.Width}x{e.NewSize.Height}");

            if (!_isInitialized || _renderEngine == null) return;

            var width = (int)e.NewSize.Width;
            var height = (int)e.NewSize.Height;

            if (width > 0 && height > 0)
            {
                GL.Viewport(0, 0, width, height);
                _renderEngine.UpdateProjection(width, height);
                System.Diagnostics.Debug.WriteLine($"Viewport resized to {width}x{height}");

                // Trigger a render after resize
                Refresh();
            }
        }

        private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnMouseDown: Button={e.ChangedButton}, PickMode={_isPointPickingMode}");

            // If in point picking mode and left button clicked
            if (_isPointPickingMode && e.ChangedButton == MouseButton.Left)
            {
                var mousePos = e.GetPosition(GlWPFControl);
                var worldPos = ScreenToWorld(mousePos);

                System.Diagnostics.Debug.WriteLine($"Point picking: screen=({mousePos.X:F2}, {mousePos.Y:F2})");
                
                if (worldPos.HasValue)
                {
                    var point = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
                    _tempPoints.Add(point);
                    Refresh(); // Show the marker

                    System.Diagnostics.Debug.WriteLine($"Point picked: ({point.X:F3}, {point.Y:F3}, {point.Z:F3})");
                    
                    // Raise the event
                    PointPicked?.Invoke(this, new PointPickedEventArgs(point));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to convert screen to world coordinates");
                }

                e.Handled = true;
                return;
            }
            else if (_isPointPickingMode && e.ChangedButton == MouseButton.Right)
            {
                // Raise a "cancelled" event
                PointPickingCancelled?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            // Normal mouse handling for camera control
            _lastMousePos = e.GetPosition(GlWPFControl);
            GlWPFControl.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_renderEngine == null) return;

            Point currentPos = e.GetPosition(GlWPFControl);
            double dx = currentPos.X - _lastMousePos.X;
            double dy = currentPos.Y - _lastMousePos.Y;

            // Convert screen coordinates to world coordinates (on Z=0 plane)
            var worldPos = ScreenToWorld(currentPos);
            if (worldPos.HasValue)
            {
                _statusBar?.UpdatePositionText($"X: {worldPos.Value.X:F3}  Y: {worldPos.Value.Y:F3}  Z: {worldPos.Value.Z:F3}");
                
                // ADD THIS BLOCK - Call preview callback during point picking
                if (_isPointPickingMode && _previewCallback != null)
                {
                    var previewPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
                    
                    // Apply snapping if enabled
                    if (_snappingEnabled)
                    {
                        previewPoint = SnapToGrid(previewPoint);
                    }
                    
                    _previewCallback(previewPoint);
                }
            }
            else
            {
                // Fallback to screen coordinates if unprojection fails
                _statusBar?.UpdatePositionText($"Screen: {currentPos.X:F0}, {currentPos.Y:F0}");
            }

            // Don't do camera manipulation in point picking mode
            if (_isPointPickingMode)
            {
                _lastMousePos = currentPos;
                return;
            }

            bool needsRefresh = false;

            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                // Orbit
                _renderEngine.Camera.Orbit((float)dx * 0.01f, (float)dy * 0.01f);
                needsRefresh = true;
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                // Pan
                _renderEngine.Camera.Pan((float)-dx * 0.01f, (float)dy * 0.01f);
                needsRefresh = true;
            }

            _lastMousePos = currentPos;

            // Only refresh if the camera actually moved
            if (needsRefresh)
            {
                Refresh();
            }
        }

        /// <summary>
        /// Convert screen coordinates to world coordinates by projecting onto the Z=0 plane
        /// </summary>
        public Vector3? ScreenToWorld(Point screenPos)
        {
            if (_renderEngine == null) return null;

            try
            {
                // Get viewport dimensions
                float width = (float)GlWPFControl.ActualWidth;
                float height = (float)GlWPFControl.ActualHeight;

                if (width <= 0 || height <= 0) return null;

                // Convert screen coordinates to normalized device coordinates (NDC)
                // Screen: (0,0) top-left, (width,height) bottom-right
                // NDC: (-1,-1) bottom-left, (1,1) top-right
                float ndcX = ((float)screenPos.X / width) * 2.0f - 1.0f;
                float ndcY = 1.0f - ((float)screenPos.Y / height) * 2.0f; // Flip Y

                // Get camera matrices
                var viewMatrix = _renderEngine.Camera.GetViewMatrix();
                var projectionMatrix = GetProjectionMatrix();

                // Combine view and projection
                Matrix4x4.Invert(viewMatrix * projectionMatrix, out var invViewProj);

                // Unproject near and far points
                var nearPoint = Vector4.Transform(new Vector4(ndcX, ndcY, -1, 1), invViewProj);
                var farPoint = Vector4.Transform(new Vector4(ndcX, ndcY, 1, 1), invViewProj);

                // Perspective divide
                nearPoint /= nearPoint.W;
                farPoint /= farPoint.W;

                // Create ray
                var rayOrigin = new Vector3(nearPoint.X, nearPoint.Y, nearPoint.Z);
                var rayEnd = new Vector3(farPoint.X, farPoint.Y, farPoint.Z);
                var rayDir = Vector3.Normalize(rayEnd - rayOrigin);

                // Intersect ray with Z=0 plane
                // Ray equation: P = rayOrigin + t * rayDir
                // Plane equation: Z = 0
                // Solve: rayOrigin.Z + t * rayDir.Z = 0
                if (Math.Abs(rayDir.Z) > 0.0001f) // Avoid division by zero
                {
                    float t = -rayOrigin.Z / rayDir.Z;
                    if (t >= 0) // Only consider intersections in front of camera
                    {
                        var intersection = rayOrigin + t * rayDir;
                        return intersection;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ScreenToWorld: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the current projection matrix from the render engine
        /// </summary>
        private Matrix4x4 GetProjectionMatrix()
        {
            // Create the same projection matrix as RenderEngine.UpdateProjection
            float width = (float)GlWPFControl.ActualWidth;
            float height = (float)GlWPFControl.ActualHeight;

            if (width <= 0) width = 800;
            if (height <= 0) height = 600;

            float aspectRatio = width / height;
            return Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI / 4, // 45 degrees FOV
                aspectRatio,
                0.1f,         // Near plane
                1000.0f       // Far plane
            );
        }

        private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (_renderEngine == null) return;

            float delta = e.Delta > 0 ? 0.5f : -0.5f;
            _renderEngine.Camera.Zoom(delta);

            // Trigger a render after zoom
            Refresh();
        }

        private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            GlWPFControl.ReleaseMouseCapture();
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            // Optional: could show a message when entering viewport
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            // Clear coordinates when mouse leaves viewport
            _statusBar?.UpdatePositionText("--");
        }

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

            // Trigger a render when objects are added
            Refresh();
        }

        /// <summary>
        /// Remove an object from the scene
        /// </summary>
        public void RemoveObject(OpenCADObject obj)
        {
            // Trigger a render when objects are removed
            Refresh();
        }

        /// <summary>
        /// Clear all objects from the scene
        /// </summary>
        public void ClearObjects()
        {
            System.Diagnostics.Debug.WriteLine("Cleared all objects");

            // Trigger a render after clearing
            Refresh();
        }

        /// <summary>
        /// Force a refresh of the viewport (triggers a single render)
        /// </summary>
        public void Refresh()
        {
            GlWPFControl?.InvalidateVisual();
        }

        /// <summary>
        /// Gets the OpenCAD object being displayed in this viewport
        /// </summary>
        public OpenCADObject ObjectToDisplay => _objectToDisplay;

        /// <summary>
        /// Gets the GLWpfControl
        /// </summary>
        public GLWpfControl GlControl => GlWPFControl;

        /// <summary>
        /// Enable or disable snapping to grid
        /// </summary>
        public void EnableSnapping(bool enabled, double gridSize = 1.0)
        {
            _snappingEnabled = enabled;
            _gridSize = gridSize;
        }

        /// <summary>
        /// Snap a point to the nearest grid intersection
        /// </summary>
        private Point3D SnapToGrid(Point3D point)
        {
            return new Point3D(
                Math.Round(point.X / _gridSize) * _gridSize,
                Math.Round(point.Y / _gridSize) * _gridSize,
                Math.Round(point.Z / _gridSize) * _gridSize
            );
        }

        /// <summary>
        /// Render temporary preview geometry (like rubber band lines)
        /// </summary>
        private void RenderPreviewGeometry()
        {
            System.Diagnostics.Debug.WriteLine($"RenderPreviewGeometry: _previewPoint={(_previewPoint != null ? "SET" : "null")}, _tempPoints.Count={_tempPoints.Count}");
            
            if (_previewPoint != null && _tempPoints.Count > 0)
            {
                // Draw a line from the last temp point to the preview point
                var lastPoint = _tempPoints[_tempPoints.Count - 1];
                var previewLine = new Line(lastPoint, _previewPoint);
                
                System.Diagnostics.Debug.WriteLine($"  Rendering preview line from ({lastPoint.X:F3}, {lastPoint.Y:F3}, {lastPoint.Z:F3}) to ({_previewPoint.X:F3}, {_previewPoint.Y:F3}, {_previewPoint.Z:F3})");
                
                // Render preview line
                _renderEngine?.Render(new[] { previewLine });
            }
            else
            {
                if (_previewPoint == null)
                    System.Diagnostics.Debug.WriteLine("  Preview skipped: _previewPoint is null");
                if (_tempPoints.Count == 0)
                    System.Diagnostics.Debug.WriteLine("  Preview skipped: _tempPoints is empty");
            }
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
}