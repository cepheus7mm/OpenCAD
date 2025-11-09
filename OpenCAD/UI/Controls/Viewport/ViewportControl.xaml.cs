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
using OpenCAD.Settings;

namespace UI.Controls.Viewport
{
    public partial class ViewportControl : UserControl
    {
        private readonly ViewportViewModel _viewModel;
        private RenderEngine? _renderEngine;
        private bool _isInitialized = false;
        private Point? _lastMousePosDip; // Track last mouse position in DIPs for delta calculation
        private Point? _currentMousePosDip; // Track current mouse position for crosshair rendering

        // Store the document directly
        private readonly OpenCADDocument _document;

        // Add a field for viewport settings
        private readonly ViewportSettings _viewportSettings;
        private bool _documentFullyLoaded = false;  // ✅ ADD THIS

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

        public ViewportControl(OpenCADDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            _document = document;
            _viewportSettings = document.CurrentViewportSettings; // Create settings instance
            
            _viewModel = new ViewportViewModel(document);
            _viewModel.SetViewportSettings(_viewportSettings); // Pass settings to ViewModel
            
            // Initialize snapping state from settings
            _viewModel.UpdateSnappingFromSettings();
            
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

            // ✅ ADD: Verify document is fully loaded
            try
            {
                var testLayer = document.CurrentLayer;
                _documentFullyLoaded = true;
                System.Diagnostics.Debug.WriteLine($"ViewportControl: Document fully loaded with current layer: {testLayer?.Name ?? "null"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ViewportControl: Document NOT fully loaded: {ex.Message}");
                _documentFullyLoaded = false;
            }
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
        
        /// <summary>
        /// Gets the document being displayed in this viewport
        /// </summary>
        public OpenCADDocument Document => _document;
        
        /// <summary>
        /// Gets the object to display (the document)
        /// </summary>
        public OpenCADObject ObjectToDisplay => _viewModel.ObjectToDisplay;
        
        public GLWpfControl GlControl => GlWPFControl;

        /// <summary>
        /// Gets the viewport settings for this viewport
        /// </summary>
        public ViewportSettings GetViewportSettings() => _viewportSettings;

        /// <summary>
        /// Update snapping state from settings (call when settings change)
        /// </summary>
        public void UpdateSnappingFromSettings() => _viewModel.UpdateSnappingFromSettings();

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
            //System.Diagnostics.Debug.WriteLine($"*** OnRender called at {DateTime.Now:HH:mm:ss.fff} ***");
    
            if (!_isInitialized || _renderEngine == null)
                return;

            // ✅ ADD: Don't render until document is fully loaded
            if (!_documentFullyLoaded)
            {
                System.Diagnostics.Debug.WriteLine("OnRender: Document not fully loaded, skipping render");
                
                // Try to check again
                try
                {
                    var testLayer = _document.CurrentLayer;
                    _documentFullyLoaded = true;
                    System.Diagnostics.Debug.WriteLine("OnRender: Document is now fully loaded!");
                }
                catch
                {
                    return; // Still not ready
                }
            }

            try
            {
                // Clear the framebuffer ONCE at the start of the frame
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                // Render grid first (background layer)
                RenderGrid();

                // Then render scene objects
                RenderSceneFlat(_document);

                // Finally render overlay (crosshair, preview lines, etc.)
                RenderPreviewGeometry();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! EXCEPTION during render: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void RenderSceneFlat(OpenCADDocument document)
        {
            if (document == null || _renderEngine == null) return;

            var list = new List<OpenCADObject>();
            CollectDrawable(document, list);
            
            // Pass highlighting and selection information to the render engine
            _renderEngine.Render(list, _viewModel.HighlightedObject, _viewModel.SelectedObjects);
        }

        private void CollectDrawable(OpenCADObject parent, List<OpenCADObject> list)
        {
            var children = parent.GetChildren();
            foreach (var child in children)
            {
                if (child.IsDrawable)
                {
                    // Check if the object is on a visible layer
                    bool shouldRender = true;
                    
                    // Get the layer ID for this object
                    var layerId = child.GetLayerId();
                    if (layerId.HasValue)
                    {
                        // Resolve the layer from the document
                        var layer = _document.GetLayer(layerId.Value);
                        if (layer != null && !layer.IsVisible)
                        {
                            shouldRender = false;
                        }
                    }
                    
                    if (shouldRender)
                    {
                        list.Add(child);
                    }
                }

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

            //System.Diagnostics.Debug.WriteLine($"RenderPreviewGeometry: previewPoint={(previewPoint != null ? "SET" : "null")}, tempPoints.Count={tempPoints.Count}");

            if (previewPoint != null && tempPoints.Count > 0)
            {
                var lastPoint = tempPoints[tempPoints.Count - 1];
                var previewLine = new Line(_document, lastPoint, previewPoint);

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

            // Create the center point - apply snapping if in point picking mode
            Point3D centerPoint;
            if (_viewModel.IsPointPickingMode && _viewModel.SnappingEnabled)
            {
                // Snap the crosshair position to grid
                var unsnappedPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
                centerPoint = _viewModel.SnapToGrid(unsnappedPoint);
            }
            else
            {
                // No snapping - use raw world coordinates
                centerPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
            }

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
                lines.Add(CreateCrosshairLine(horizontalStart, horizontalEnd));

                // Vertical crosshair line (extends to viewport edges)
                var verticalStart = new Point3D(centerPoint.X, topLeft.Value.Y, 0);
                var verticalEnd = new Point3D(centerPoint.X, bottomLeft.HasValue ? bottomLeft.Value.Y : bottomRight.Value.Y, 0);
                lines.Add(CreateCrosshairLine(verticalStart, verticalEnd));
            }

            // Get pickbox size from settings (in pixels)
            double pickboxSizePixels = _viewportSettings.Crosshair?.PickboxSize ?? 5.0;
            
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
                lines.Add(CreateCrosshairLine(
                    new Point3D(centerPoint.X - halfBox, centerPoint.Y - halfBox, 0),
                    new Point3D(centerPoint.X + halfBox, centerPoint.Y - halfBox, 0)
                ));
                
                // Right edge
                lines.Add(CreateCrosshairLine(
                    new Point3D(centerPoint.X + halfBox, centerPoint.Y - halfBox, 0),
                    new Point3D(centerPoint.X + halfBox, centerPoint.Y + halfBox, 0)
                ));
                
                // Top edge
                lines.Add(CreateCrosshairLine(
                    new Point3D(centerPoint.X + halfBox, centerPoint.Y + halfBox, 0),
                    new Point3D(centerPoint.X - halfBox, centerPoint.Y + halfBox, 0)
                ));
                
                // Left edge
                lines.Add(CreateCrosshairLine(
                    new Point3D(centerPoint.X - halfBox, centerPoint.Y + halfBox, 0),
                    new Point3D(centerPoint.X - halfBox, centerPoint.Y - halfBox, 0)
                ));
            }

            return lines;
        }

        /// <summary>
        /// Creates a crosshair line with user-defined settings
        /// </summary>
        private Line CreateCrosshairLine(Point3D start, Point3D end)
        {
            // Crosshair lines have proper document context
            var line = new Line(_document, start, end);
            
            // Apply user-defined crosshair visual settings
            var crosshairSettings = _viewportSettings.Crosshair;
            if (crosshairSettings != null)
            {
                line.Color = crosshairSettings.Color;
                line.LineType = crosshairSettings.LineType;
                line.LineWeight = crosshairSettings.LineWeight;
            }
            
            return line;
        }

        /// <summary>
        /// Renders the grid based on viewport settings
        /// </summary>
        private void RenderGrid()
        {
            if (_renderEngine == null) return;

            var gridSettings = _viewportSettings.Grid;
            if (gridSettings == null || !gridSettings.ShowGrid)
                return;

            var gridLines = CreateGridLines();
            if (gridLines.Count > 0)
            {
                _renderEngine.RenderOverlay(gridLines);
            }
        }

        /// <summary>
        /// Creates grid lines based on viewport settings and current view bounds
        /// </summary>
        private List<Line> CreateGridLines()
        {
            if (_renderEngine == null)
                return new List<Line>();

            var lines = new List<Line>();
            var gridSettings = _viewportSettings.Grid;
            if (gridSettings == null)
                return lines;

            // Get viewport bounds in world coordinates
            var topLeft = ScreenToWorld(new Point(0, 0));
            var bottomRight = ScreenToWorld(new Point(GlWPFControl.ActualWidth, GlWPFControl.ActualHeight));

            if (!topLeft.HasValue || !bottomRight.HasValue)
                return lines;

            double minX = topLeft.Value.X;
            double maxX = bottomRight.Value.X;
            double minY = bottomRight.Value.Y; // Note: Y is inverted in screen space
            double maxY = topLeft.Value.Y;

            // Get grid spacing
            double majorSpacing = gridSettings.MajorSpacing;
            double minorSpacing = gridSettings.MinorSpacing;

            // Calculate grid line positions
            // We'll draw minor grid lines
            double startX = Math.Floor(minX / minorSpacing) * minorSpacing;
            double startY = Math.Floor(minY / minorSpacing) * minorSpacing;

            // Vertical grid lines
            for (double x = startX; x <= maxX; x += minorSpacing)
            {
                bool isMajor = Math.Abs(x % majorSpacing) < 0.001;
                var line = CreateGridLine(
                    new Point3D(x, minY, 0),
                    new Point3D(x, maxY, 0),
                    isMajor
                );
                lines.Add(line);
            }

            // Horizontal grid lines
            for (double y = startY; y <= maxY; y += minorSpacing)
            {
                bool isMajor = Math.Abs(y % majorSpacing) < 0.001;
                var line = CreateGridLine(
                    new Point3D(minX, y, 0),
                    new Point3D(maxX, y, 0),
                    isMajor
                );
                lines.Add(line);
            }

            return lines;
        }

        /// <summary>
        /// Creates a single grid line with appropriate styling
        /// </summary>
        private Line CreateGridLine(Point3D start, Point3D end, bool isMajor)
        {
            var line = new Line(_document, start, end);
            var gridSettings = _viewportSettings.Grid;

            if (gridSettings != null)
            {
                // Make minor grid lines more subtle (50% transparency)
                var color = gridSettings.Color;
                if (!isMajor)
                {
                    color = System.Drawing.Color.FromArgb(
                        (int)(color.A * 0.5), // 50% alpha
                        color.R,
                        color.G,
                        color.B
                    );
                }

                line.Color = color;
                line.LineType = LineType.Continuous;
                line.LineWeight = isMajor ? LineWeight.LineWeight015 : LineWeight.Hairline;
            }

            return line;
        }
        public void Refresh()
        {
            //System.Diagnostics.Debug.WriteLine("*** Refresh() -> InvalidateVisual() called ***");
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
            // DON'T steal focus - let command input keep it
            // GlWPFControl.Focus();  // REMOVED

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

            // Handle ESC key
            if (e.Key == Key.Escape)
            {
                // Priority 1: Cancel point picking mode if active
                if (_viewModel.IsPointPickingMode)
                {
                    System.Diagnostics.Debug.WriteLine("ESC pressed - cancelling point picking mode");
                    _viewModel.CancelPointPicking();
                    e.Handled = true;
                    return;
                }

                // Priority 2: Clear selection if there are selected objects
                if (_viewModel.IsSelectionMode && _viewModel.SelectedObjects.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("ESC pressed - clearing selection");
                    _viewModel.ClearSelection();
                    e.Handled = true;
                    return;
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

        /// <summary>
        /// Handle ESC key press (called from MainWindow PreviewKeyDown)
        /// </summary>
        /// <returns>True if ESC was handled, false otherwise</returns>
        public bool HandleEscapeKey()
        {
            System.Diagnostics.Debug.WriteLine($"ViewportControl.HandleEscapeKey: IsPointPickingMode={_viewModel.IsPointPickingMode}, IsSelectionMode={_viewModel.IsSelectionMode}, SelectedCount={_viewModel.SelectedObjects.Count}");
            
            // Priority 1: Cancel point picking mode if active
            if (_viewModel.IsPointPickingMode)
            {
                System.Diagnostics.Debug.WriteLine("ESC handled - cancelling point picking mode");
                _viewModel.CancelPointPicking();
                return true;
            }
            
            // Priority 2: Clear selection if there are selected objects
            if (_viewModel.IsSelectionMode && _viewModel.SelectedObjects.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("ESC handled - clearing selection");
                _viewModel.ClearSelection();
                return true;
            }
            
            return false;
        }
        /// <summary>
        /// Handle Delete key press to erase selected objects (called from MainWindow PreviewKeyDown)
        /// </summary>
        /// <returns>True if Delete was handled (objects were deleted), false otherwise</returns>
        public bool HandleDeleteKey()
        {
            // Only delete if in selection mode with selected objects
            if (_viewModel.IsSelectionMode && _viewModel.SelectedObjects.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Delete key pressed - erasing {_viewModel.SelectedObjects.Count} selected object(s)");
        
                // Create a list to hold the objects to delete (to avoid modifying collection during iteration)
                var objectsToDelete = _viewModel.SelectedObjects.ToList();
        
                // Remove each selected object
                foreach (var obj in objectsToDelete)
                {
                    _viewModel.RemoveObject(obj);
                    System.Diagnostics.Debug.WriteLine($"  Deleted: {obj.GetType().Name} (ID: {obj.ID})");
                }
        
                // Clear the selection after deletion
                _viewModel.ClearSelection();
        
                return true;
            }
    
            return false;
        }
    }
}