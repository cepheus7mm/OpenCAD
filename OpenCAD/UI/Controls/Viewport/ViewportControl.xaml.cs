using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using OpenCAD;
using OpenCAD.Geometry;
using GraphicsEngine;
using System.Numerics;
using UI.Controls.MainWindow;
using System.Windows.Media;

namespace UI.Controls.Viewport
{
    public partial class ViewportControl : UserControl
    {
        private readonly ViewportViewModel _viewModel;
        private RenderEngine? _renderEngine;
        private bool _isInitialized = false;
        private Point? _lastMousePosDip; // Track last mouse position in DIPs for delta calculation
        private Point? _currentMousePosDip; // Track current mouse position for crosshair rendering
        private System.Drawing.Color _crosshairColor = System.Drawing.Color.White; // Adjustable crosshair color
        private const double PICKBOX_SIZE = 5.0; // Pickbox size in world units (adjust as needed)

        // Forward events from ViewModel
        public event EventHandler<PointPickedEventArgs>? PointPicked
        {
            add => _viewModel.PointPicked += value;
            remove => _viewModel.PointPicked -= value;
        }

        public event EventHandler? PointPickingCancelled
        {
            add => _viewModel.PointPickingCancelled += value;
            remove => _viewModel.PointPickingCancelled -= value;
        }

        /// <summary>
        /// Gets the mutable temp points list for commands to add points directly
        /// </summary>
        public List<Point3D> TempPoints => _viewModel.TempPointsMutable;

        /// <summary>
        /// Gets or sets the crosshair color (default: White)
        /// </summary>
        public System.Drawing.Color CrosshairColor
        {
            get => _crosshairColor;
            set
            {
                _crosshairColor = value;
                Refresh();
            }
        }

        public ViewportControl(OpenCADObject objectToDisplay)
        {
            if (objectToDisplay == null)
                throw new ArgumentNullException(nameof(objectToDisplay));

            _viewModel = new ViewportViewModel(objectToDisplay);
            DataContext = _viewModel;

            InitializeComponent();

            System.Diagnostics.Debug.WriteLine("=== ViewportControl constructor called ===");

            if (GlWPFControl == null)
                throw new InvalidOperationException("GlControl not found. Make sure it is defined in XAML with x:Name=\"GlWPFControl\".");

            var settings = new GLWpfControlSettings
            {
                MajorVersion = 3,
                MinorVersion = 3,
                RenderContinuously = false,
            };

            GlWPFControl.Start(settings);
            System.Diagnostics.Debug.WriteLine("GLWpfControl.Start() called with RenderContinuously = false");

            // Subscribe to ViewModel events
            _viewModel.RefreshRequested += (s, e) => Refresh();
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewportViewModel.CurrentCursor))
                {
                    // Don't update cursor here - we'll handle it separately
                }
            };

            // Subscribe to control events
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

            // Keyboard events
            GlWPFControl.KeyDown += OnKeyDown;
            GlWPFControl.Focusable = true; // Make sure the control can receive keyboard focus

            System.Diagnostics.Debug.WriteLine("=== ViewportControl constructor completed ===");
        }

        #region Public API (delegates to ViewModel)

        public void EnablePointPickingMode() => _viewModel.EnablePointPickingMode();
        public void DisablePointPickingMode() => _viewModel.DisablePointPickingMode();
        public void EnablePreviewMode(Action<Point3D> previewCallback) => _viewModel.EnablePreviewMode(previewCallback);
        public void DisablePreviewMode() => _viewModel.DisablePreviewMode();
        public void SetPreviewPoint(Point3D? point) => _viewModel.SetPreviewPoint(point);
        public void SetStatusBar(StatusBarControl statusBar) => _viewModel.SetStatusBar(statusBar);
        public void AddObject(OpenCADObject obj) => _viewModel.AddObject(obj);
        public void RemoveObject(OpenCADObject obj) => _viewModel.RemoveObject(obj);
        public void ClearObjects() => _viewModel.ClearObjects();
        public void EnableSnapping(bool enabled, double gridSize = 1.0) => _viewModel.EnableSnapping(enabled, gridSize);
        public void EnableSelectionMode() => _viewModel.EnableSelectionMode();
        public void DisableSelectionMode() => _viewModel.DisableSelectionMode();
        public void ClearSelection() => _viewModel.ClearSelection();
        public OpenCADObject ObjectToDisplay => _viewModel.ObjectToDisplay;
        public GLWpfControl GlControl => GlWPFControl;

        #endregion

        #region OpenGL Initialization

        private void ViewportControl_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== ViewportControl.Loaded event fired ===");

            if (!_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("Initializing from Loaded event");
                InitializeOpenGL();
            }
        }

        private void OnGLControlReady()
        {
            System.Diagnostics.Debug.WriteLine("=== GLWpfControl.Ready event fired ===");

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

                // Use framebuffer pixel size, not DIPs
                var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
                int pixelWidth = Math.Max(1, (int)Math.Round(GlWPFControl.ActualWidth * dpi.DpiScaleX));
                int pixelHeight = Math.Max(1, (int)Math.Round(GlWPFControl.ActualHeight * dpi.DpiScaleY));
                if (pixelWidth <= 0) pixelWidth = 800;
                if (pixelHeight <= 0) pixelHeight = 600;

                System.Diagnostics.Debug.WriteLine($"Viewport framebuffer (pixels): {pixelWidth}x{pixelHeight}");

                _renderEngine.Initialize(pixelWidth, pixelHeight);

                GL.Disable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);

                var error = GL.GetError();
                if (error != ErrorCode.NoError)
                {
                    System.Diagnostics.Debug.WriteLine($"OpenGL Error during initialization: {error}");
                }

                _isInitialized = true;

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

        #endregion

        #region Rendering

        private void OnRender(TimeSpan delta)
        {
            System.Diagnostics.Debug.WriteLine($"*** OnRender called at {DateTime.Now:HH:mm:ss.fff} ***");
    
            if (!_isInitialized || _renderEngine == null)
                return;

            try
            {
                // Do not clear here; RenderEngine.Render clears once per frame
                RenderSceneFlat(ObjectToDisplay);
                RenderPreviewGeometry();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! EXCEPTION during render: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void RenderSceneFlat(OpenCADObject? root)
        {
            if (root == null || _renderEngine == null) return;

            var list = new List<OpenCADObject>();
            CollectDrawable(root, list);
            
            // Pass highlighting and selection information to the render engine
            _renderEngine.Render(list, _viewModel.HighlightedObject, _viewModel.SelectedObjects);
        }

        private void CollectDrawable(OpenCADObject parent, List<OpenCADObject> list)
        {
            var children = parent.GetChildren();
            foreach (var child in children)
            {
                if (child.IsDrawable)
                    list.Add(child);

                // Recurse to gather all nested drawables
                CollectDrawable(child, list);
            }
        }

        private void RenderPreviewGeometry()
        {
            if (_renderEngine == null) return;

            var overlayObjects = new List<OpenCADObject>();

            // Add preview line if available
            var previewPoint = _viewModel.PreviewPoint;
            var tempPoints = _viewModel.TempPoints;

            System.Diagnostics.Debug.WriteLine($"RenderPreviewGeometry: previewPoint={(previewPoint != null ? "SET" : "null")}, tempPoints.Count={tempPoints.Count}");

            if (previewPoint != null && tempPoints.Count > 0)
            {
                var lastPoint = tempPoints[tempPoints.Count - 1];
                var previewLine = new Line(lastPoint, previewPoint);

                System.Diagnostics.Debug.WriteLine($"  Rendering preview line from ({lastPoint.X:F3}, {lastPoint.Y:F3}, {lastPoint.Z:F3}) to ({previewPoint.X:F3}, {previewPoint.Y:F3}, {previewPoint.Z:F3})");

                overlayObjects.Add(previewLine);
            }

            // Add crosshair if mouse is in viewport
            if (_currentMousePosDip.HasValue)
            {
                var crosshairLines = CreateCrosshairLines(_currentMousePosDip.Value);
                overlayObjects.AddRange(crosshairLines);
            }

            // Render all overlay objects at once
            if (overlayObjects.Count > 0)
            {
                _renderEngine.RenderOverlay(overlayObjects);
            }
        }

        private List<Line> CreateCrosshairLines(Point mousePosDip)
        {
            if (_renderEngine == null)
                return new List<Line>();

            var lines = new List<Line>();

            // Convert mouse position to world coordinates
            var worldPos = ScreenToWorld(mousePosDip);
            if (worldPos == null)
                return lines;

            var centerPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);

            // Calculate viewport bounds in world coordinates
            var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
            
            // Get the four corners of the viewport in world space
            var topLeft = ScreenToWorld(new Point(0, 0));
            var topRight = ScreenToWorld(new Point(GlWPFControl.ActualWidth, 0));
            var bottomLeft = ScreenToWorld(new Point(0, GlWPFControl.ActualHeight));
            var bottomRight = ScreenToWorld(new Point(GlWPFControl.ActualWidth, GlWPFControl.ActualHeight));

            if (topLeft.HasValue && bottomRight.HasValue)
            {
                // Horizontal crosshair line (extends to viewport edges)
                var horizontalStart = new Point3D(topLeft.Value.X, centerPoint.Y, 0);
                var horizontalEnd = new Point3D(topRight.HasValue ? topRight.Value.X : bottomRight.Value.X, centerPoint.Y, 0);
                lines.Add(new Line(horizontalStart, horizontalEnd));

                // Vertical crosshair line (extends to viewport edges)
                var verticalStart = new Point3D(centerPoint.X, topLeft.Value.Y, 0);
                var verticalEnd = new Point3D(centerPoint.X, bottomLeft.HasValue ? bottomLeft.Value.Y : bottomRight.Value.Y, 0);
                lines.Add(new Line(verticalStart, verticalEnd));
            }

            // Calculate pickbox size in world units based on screen pixels
            // PICKBOX_SIZE is in pixels, we need to convert to world units at current zoom
            double pickboxSizePixels = PICKBOX_SIZE;
            
            // Calculate two points offset by the pickbox size in screen space
            var screenCenter = mousePosDip;
            var screenOffset = new Point(screenCenter.X + pickboxSizePixels, screenCenter.Y);
            
            var worldCenter = ScreenToWorld(screenCenter);
            var worldOffset = ScreenToWorld(screenOffset);
            
            if (worldCenter.HasValue && worldOffset.HasValue)
            {
                // Calculate the world-space distance that corresponds to pickboxSizePixels
                double worldPickboxSize = Math.Abs(worldOffset.Value.X - worldCenter.Value.X);
                double halfBox = worldPickboxSize;
                
                // Bottom edge
                lines.Add(new Line(
                    new Point3D(centerPoint.X - halfBox, centerPoint.Y - halfBox, 0),
                    new Point3D(centerPoint.X + halfBox, centerPoint.Y - halfBox, 0)
                ));
                
                // Right edge
                lines.Add(new Line(
                    new Point3D(centerPoint.X + halfBox, centerPoint.Y - halfBox, 0),
                    new Point3D(centerPoint.X + halfBox, centerPoint.Y + halfBox, 0)
                ));
                
                // Top edge
                lines.Add(new Line(
                    new Point3D(centerPoint.X + halfBox, centerPoint.Y + halfBox, 0),
                    new Point3D(centerPoint.X - halfBox, centerPoint.Y + halfBox, 0)
                ));
                
                // Left edge
                lines.Add(new Line(
                    new Point3D(centerPoint.X - halfBox, centerPoint.Y + halfBox, 0),
                    new Point3D(centerPoint.X - halfBox, centerPoint.Y - halfBox, 0)
                ));
            }

            return lines;
        }

        public void Refresh()
        {
            System.Diagnostics.Debug.WriteLine("*** Refresh() -> InvalidateVisual() called ***");
            GlWPFControl?.InvalidateVisual();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnSizeChanged: {e.NewSize.Width}x{e.NewSize.Height}");

            if (!_isInitialized || _renderEngine == null) return;

            // Convert DIPs to physical pixels for the GL viewport/projection
            var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
            int pixelWidth = Math.Max(1, (int)Math.Round(e.NewSize.Width * dpi.DpiScaleX));
            int pixelHeight = Math.Max(1, (int)Math.Round(e.NewSize.Height * dpi.DpiScaleY));

            _renderEngine.UpdateProjection(pixelWidth, pixelHeight);
            System.Diagnostics.Debug.WriteLine($"Framebuffer resized to {pixelWidth}x{pixelHeight} (pixels)");

            Refresh();
        }

        #endregion

        #region Mouse Event Handlers

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Give focus to the control so it can receive keyboard events
            GlWPFControl.Focus();
            
            var mousePos = e.GetPosition(GlWPFControl);
            _lastMousePosDip = mousePos; // start delta tracking

            var worldPos = ScreenToWorld(mousePos);

            var result = _viewModel.HandleMouseDown(e.ChangedButton, mousePos, worldPos);

            if (result.Handled)
                e.Handled = true;

            // Ensure we capture for panning with middle/right even if VM didn't request it
            if (result.CaptureMouse || e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right)
                GlWPFControl.CaptureMouse();

            if (result.NeedsRefresh)
                Refresh();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_renderEngine == null) return;

            Point currentPosDip = e.GetPosition(GlWPFControl);
            
            // Update current mouse position for crosshair rendering
            _currentMousePosDip = currentPosDip;
            
            var worldPos = ScreenToWorld(currentPosDip);

            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // Keep existing panScale for VM consumers
            float panScale = (_renderEngine.ProjectionMode == GraphicsEngine.ProjectionMode.Orthographic)
                ? _renderEngine.OrthographicScale * 0.02f
                : (_renderEngine.Camera.Position - _renderEngine.Camera.Target).Length() * 0.002f;

            // Only perform hit testing if in selection mode, NOT in point picking mode, and not dragging
            if (_viewModel.IsSelectionMode && !_viewModel.IsPointPickingMode && 
                e.LeftButton != MouseButtonState.Pressed && 
                e.MiddleButton != MouseButtonState.Pressed && 
                e.RightButton != MouseButtonState.Pressed)
            {
                var hitObject = _viewModel.HitTest(currentPosDip, ScreenToWorld);
                if (_viewModel.HighlightedObject != hitObject)
                {
                    // Update highlighted object directly (now that it has internal setter)
                    _viewModel.HighlightedObject = hitObject;
                }
            }

            var result = _viewModel.HandleMouseMove(
                currentPosDip,
                worldPos,
                e.MiddleButton,
                e.RightButton,
                isShiftPressed,
                panScale,
                out var cameraOp);

            bool didPan = false;

            // Compute framebuffer pixel delta once
            if (_lastMousePosDip.HasValue)
            {
                var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
                float dxPx = (float)((currentPosDip.X - _lastMousePosDip.Value.X) * dpi.DpiScaleX);
                float dyPx = (float)((currentPosDip.Y - _lastMousePosDip.Value.Y) * dpi.DpiScaleY);

                // Preferred path: VM asked to pan -> use pixel-based ortho pan
                if (cameraOp?.Type == CameraOperationType.Pan)
                {
                    if (_renderEngine.ProjectionMode == GraphicsEngine.ProjectionMode.Orthographic)
                    {
                        _renderEngine.PanOrthoPixels(dxPx, dyPx);
                        didPan = true;
                    }
                    else
                    {
                        _renderEngine.Camera.Pan(cameraOp.DeltaX, cameraOp.DeltaY);
                        didPan = true;
                    }
                }
                // Fallback: if VM didn't emit a pan op but the user is dragging with middle/right in ortho, pan anyway
                else if (_renderEngine.ProjectionMode == GraphicsEngine.ProjectionMode.Orthographic &&
                         (e.MiddleButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed))
                {
                    _renderEngine.PanOrthoPixels(dxPx, dyPx);
                    didPan = true;
                }
            }

            _lastMousePosDip = currentPosDip;

            // Always refresh to update crosshair position
            Refresh();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_renderEngine == null) return;

            // Handle zoom based on projection mode
            if (_renderEngine.ProjectionMode == GraphicsEngine.ProjectionMode.Orthographic)
            {
                // Orthographic zoom adjusts the scale
                float scaleDelta = e.Delta > 0 ? -0.5f : 0.5f;
                _renderEngine.ZoomOrthographic(scaleDelta);
            }
            else
            {
                // Perspective zoom moves camera position
                float zoomDelta = e.Delta > 0 ? 0.5f : -0.5f;
                _renderEngine.Camera.Zoom(zoomDelta);
            }

            Refresh();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            GlWPFControl.ReleaseMouseCapture();
            _lastMousePosDip = null; // stop delta tracking
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            // Hide the system cursor when entering the viewport
            GlWPFControl.Cursor = Cursors.None;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _viewModel.ClearStatusBar();
            
            // Clear current mouse position so crosshair isn't rendered
            _currentMousePosDip = null;
            
            // Restore default cursor when leaving
            GlWPFControl.Cursor = Cursors.Arrow;
            
            Refresh();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"ViewportControl.OnKeyDown: Key={e.Key}, IsSelectionMode={_viewModel.IsSelectionMode}, SelectedCount={_viewModel.SelectedObjects.Count}");
            
            // Handle ESC key to clear selection
            if (e.Key == Key.Escape)
            {
                if (_viewModel.IsSelectionMode && _viewModel.SelectedObjects.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("ESC pressed - clearing selection");
                    _viewModel.ClearSelection();
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Coordinate Conversion

        public Vector3? ScreenToWorld(Point screenPos)
        {
            if (_renderEngine == null) return null;

            try
            {
                var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
                float widthPx  = (float)Math.Max(1, Math.Round(GlWPFControl.ActualWidth  * dpi.DpiScaleX));
                float heightPx = (float)Math.Max(1, Math.Round(GlWPFControl.ActualHeight * dpi.DpiScaleY));
                float mouseXpx = (float)(screenPos.X * dpi.DpiScaleX);
                float mouseYpx = (float)(screenPos.Y * dpi.DpiScaleY);

                // Fast, exact path for orthographic top view: no matrix inversion, no ambiguity
                if (_renderEngine.ProjectionMode == GraphicsEngine.ProjectionMode.Orthographic)
                {
                    return _renderEngine.ScreenToWorldOrthoPixels(mouseXpx, mouseYpx, worldZ: 0f);
                }

                // Perspective fallback: invert PV (column-major) -> use transpose for System.Numerics row-vector Transform
                float ndcX = (mouseXpx / widthPx) * 2.0f - 1.0f;
                float ndcY = 1.0f - (mouseYpx / heightPx) * 2.0f;

                var viewMatrix = _renderEngine.Camera.GetViewMatrix();
                var projectionMatrix = _renderEngine.GetProjectionMatrix();

                Matrix4x4 pv = Matrix4x4.Multiply(projectionMatrix, viewMatrix);
                if (!Matrix4x4.Invert(pv, out var invPv))
                    return null;

                var invRow = Matrix4x4.Transpose(invPv);

                var nearClip = new Vector4(ndcX, ndcY, -1, 1);
                var farClip  = new Vector4(ndcX, ndcY,  1, 1);

                var nearPoint = Vector4.Transform(nearClip, invRow);
                var farPoint  = Vector4.Transform(farClip,  invRow);

                nearPoint /= nearPoint.W;
                farPoint  /= farPoint.W;

                var rayOrigin = new Vector3(nearPoint.X, nearPoint.Y, nearPoint.Z);
                var rayEnd    = new Vector3(farPoint.X,  farPoint.Y,  farPoint.Z);
                var rayDir    = Vector3.Normalize(rayEnd - rayOrigin);

                if (Math.Abs(rayDir.Z) > 0.0001f)
                {
                    float t = -rayOrigin.Z / rayDir.Z;
                    if (t >= 0)
                        return rayOrigin + t * rayDir;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ScreenToWorld: {ex.Message}");
                return null;
            }
        }

        #endregion
    }
}