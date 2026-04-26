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
using Microsoft.Extensions.DependencyInjection;
using OpenCAD.TextRendering;
using OpenCAD.Geometry.Helpers;
using System.Diagnostics;
using OpenCAD.Styles.LineTypes;
using OpenCAD.Geometry.Helpers.GeoPoints;

namespace UI.Controls.Viewport
{
    public partial class ViewportControl : UserControl
    {
        // Cache world-space viewport bounds per frame to avoid recomputation in crosshair
        private (Point3D topLeft, Point3D topRight, Point3D bottomLeft, Point3D bottomRight)? _cachedWorldBounds;

        private readonly ViewportViewModel _viewModel;
        private RenderEngine? _renderEngine;
        private bool _isInitialized = false;
        private Point? _lastMousePosDip; // Track last mouse position in DIPs for delta calculation
        private Point? _currentMousePosDip; // Track current mouse position for crosshair rendering
#if DEBUG
        private bool _showDebugTooltip = true; // Toggle this as needed
#else
private bool _showDebugTooltip = false;
#endif

        // Store the document directly
        private readonly OpenCADDocument _document;

        // Add a field for viewport settings
        private readonly ViewportSettings _viewportSettings;
        private bool _documentFullyLoaded = false;

        private readonly HashSet<OpenCADObject> _visibleObjects = new();


        // Lazy text metrics cache
        private ITextMetricsProvider? _lazyTextMetrics;

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
            _viewportSettings = document.GetViewportSettings(); // Create settings instance
            
            _viewModel = new ViewportViewModel(document);
                        
            DataContext = _viewModel;

            InitializeComponent();

            if (GlWPFControl == null)
                throw new InvalidOperationException("GlControl not found. Make sure it is defined in XAML with x:Name=\"GlWPFControl\".");

            var settings = new GLWpfControlSettings
            {
                MajorVersion = 3,
                MinorVersion = 3,
                RenderContinuously = false,
            };

            GlWPFControl.Start(settings);

            // Subscribe to ViewModel events — always dispatch to UI thread so this
            // handler is safe to invoke from background threads (e.g. async continuations).
            _viewModel.RefreshRequested += (s, e) => Dispatcher.InvokeAsync(Refresh);
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewportViewModel.CurrentCursor))
                {
                    // Don't update cursor here - we'll handle it separately
                }
            };

            _viewModel.GeoPointModesOverrideContextMenuRequested += OnGeoPointModesOverrideContextMenuRequested;

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
            //GlWPFControl.KeyDown += OnKeyDown;
            GlWPFControl.Focusable = true; // Make sure the control can receive keyboard focus

            // ✅ ADD: Verify document is fully loaded
            try
            {
                var testLayer = document.CurrentLayer;
                _documentFullyLoaded = true;
            }
            catch (Exception)
            {
                _documentFullyLoaded = false;
            }
        }

        private void OnGeoPointModesOverrideContextMenuRequested(object? sender, Point mousePos)
        {
            // Use Dispatcher to delay menu opening until after mouse event completes
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var contextMenu = new ContextMenu();

                foreach (GeoPointModes mode in Enum.GetValues(typeof(GeoPointModes)))
                {
                    if (mode == GeoPointModes.None)
                        continue;
                    var menuItem = new MenuItem
                    {
                        Header = mode.ToString(),
                        IsCheckable = false,
                    };
                    menuItem.Click += (s, e) =>
                    {
                        _viewportSettings.GeoPointModeOverride = mode;
                        contextMenu.IsOpen = false;
                    };
                    contextMenu.Items.Add(menuItem);
                }

                contextMenu.PlacementTarget = GlWPFControl;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
                contextMenu.HorizontalOffset = mousePos.X;
                contextMenu.VerticalOffset = mousePos.Y;

                contextMenu.IsOpen = true;
            }));
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
        public void ClearSelection() => _viewModel.SelectionManager.ClearSelection();

        /// <summary>
        /// Gets the active camera for this viewport, or null if not yet initialized.
        /// </summary>
        public OpenCAD.Interfaces.ICamera? Camera => _renderEngine?.Camera;

        /// <summary>
        /// Gets the document being displayed in this viewport
        /// </summary>
        public OpenCADDocument Document => _viewModel.Document;
        
       
        public GLWpfControl GlControl => GlWPFControl;

        /// <summary>
        /// Gets the viewport settings for this viewport
        /// </summary>
        public ViewportSettings GetViewportSettings() => _viewportSettings;

        /// <summary>
        /// Update snapping state from settings (call when settings change)
        /// </summary>
        public void UpdateSnappingFromSettings() => _viewModel.UpdateSnappingFromSettings();

        //internal void AddPreviewObject(OpenCADObject obj)
        //{
        //    _viewModel.PreviewManager.ShowPreview(obj);
        //    Refresh();
        //}

        //internal void RemovePreviewObject(OpenCADObject obj)
        //{
        //    _previewObjects.Remove(obj);
        //    Refresh();
        //}

        internal void ClearPreviewObjects()
        {
            _viewModel.PreviewManager.Clear();
            Refresh();
        }

        #endregion

        #region OpenGL Initialization

        public new void InvalidateVisual()
        {
            base.InvalidateVisual();
            Refresh();
            _viewModel.UpdateStatusBarButtons();
        }


        private void ViewportControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                InitializeOpenGL();
            }
        }

        private void OnGLControlReady()
        {
            try
            {
                string version = GL.GetString(StringName.Version);
                var versionParts = version.Split('.', ' ');
                if (versionParts.Length >= 2)
                {
                    int major = int.Parse(versionParts[0]);
                    int minor = int.Parse(versionParts[1]);

                    if (major < 3 || (major == 3 && minor < 3))
                    {
                        // warning
                    }
                }
            }
            catch (Exception)
            {
            }

            InitializeOpenGL();
        }

        private void InitializeOpenGL()
        {
            if (_isInitialized)
                return;

            try
            {
                // Defer text metrics creation: do not resolve at startup
                _renderEngine = new RenderEngine(new TextMetricsProvider(new FontProvider()));

                _renderEngine.Initialize(GlWPFControl);
                _viewModel.Initialize(_renderEngine.Camera);

                GL.Disable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);

                var error = GL.GetError();
                if (error != ErrorCode.NoError)
                {
                }

                _viewModel.SetRenderEngine(_renderEngine);

                // 🔹 First DPI + pixel sync
                _renderEngine.Viewport.UpdateDpi();
                _renderEngine.Viewport.UpdatePixelSize();

                int w = _renderEngine.Viewport.PixelWidth;
                int h = _renderEngine.Viewport.PixelHeight;

                _renderEngine.ResizeViewport(w, h);
                _renderEngine.Camera.SetViewportSize(w, h);

                _isInitialized = true;
            }
            catch (Exception)
            {
            }
        }

        private void EnsureTextMetricsInitialized()
        {
            if (_lazyTextMetrics == null)
            {
                if (Application.Current is UI.App app)
                {
                    _lazyTextMetrics = app.Services.GetService<ITextMetricsProvider>();
                }

                if (_lazyTextMetrics != null && _renderEngine != null)
                {
                    //_renderEngine.SetTextMetrics(_lazyTextMetrics);
                }
            }
        }

        #endregion

        #region Rendering

        private void OnRender(TimeSpan delta)
        {
            if (!_isInitialized || _renderEngine == null)
                return;

            if (!_documentFullyLoaded)
            {
                try
                {
                    var testLayer = _document.CurrentLayer;
                    _documentFullyLoaded = true;
                }
                catch
                {
                    return;
                }
            }

            try
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                
                // Update mouse position at render time to avoid event/render timing drift (crosshair lag)
                var mousePosDip = Mouse.GetPosition(GlWPFControl);
                if (mousePosDip.X >= 0 &&
                    mousePosDip.Y >= 0 &&
                    mousePosDip.X <= GlWPFControl.ActualWidth &&
                    mousePosDip.Y <= GlWPFControl.ActualHeight)
                {
                    _currentMousePosDip = mousePosDip;
                }
                else
                {
                    _currentMousePosDip = null;
                }

                // Cache viewport bounds in world coords once per frame for overlay use
                _cachedWorldBounds = ComputeViewportWorldBounds();

                RenderGrid();

                RenderSceneFlat(_document);

                _viewModel.RenderGripPreviewObjects(_renderEngine);

                _viewModel.RenderGrips();

                RenderPostGeometry();

                UpdateDebugTooltip();
            }
            catch (Exception)
            {
            }
        }

        private (Point3D topLeft, Point3D topRight, Point3D bottomLeft, Point3D bottomRight)? ComputeViewportWorldBounds()
        {
            if (_renderEngine == null) return null;

            try
            {
                int w = _renderEngine.Viewport.PixelWidth;
                int h = _renderEngine.Viewport.PixelHeight;

                var tl = ScreenToWorld(new Point(0, 0));
                var tr = ScreenToWorld(new Point(w, 0));
                var bl = ScreenToWorld(new Point(0, h));
                var br = ScreenToWorld(new Point(w, h));

                if (tl.HasValue && tr.HasValue && bl.HasValue && br.HasValue)
                {
                    return (
                        new Point3D(tl.Value.X, tl.Value.Y, tl.Value.Z),
                        new Point3D(tr.Value.X, tr.Value.Y, tr.Value.Z),
                        new Point3D(bl.Value.X, bl.Value.Y, bl.Value.Z),
                        new Point3D(br.Value.X, br.Value.Y, br.Value.Z)
                    );
                }
            }
            catch { }

            return null;
        }

        private void RenderSceneFlat(OpenCADDocument document)
        {
            if (document == null || _renderEngine == null) return;

            // Use HashSet to prevent duplicates
            var objectSet = _viewModel.VisibleObjects;

            //// Add preview objects to the set (HashSet will ignore duplicates)
            //foreach (var previewObj in _viewModel.PreviewManager.PreviewObjects.Where(o => o.IsDrawable))
            //{
            //    objectSet.Add(previewObj);
            //}

            // Convert to list for rendering
            var list = objectSet.ToList();

            var highlightedObjects = new HashSet<OpenCADObject>();

            // Add single highlighted object (hover)
            if (_viewModel.HighlightedObject != null)
            {
                highlightedObjects.Add(_viewModel.HighlightedObject);
            }

            // Add window selection preview objects to highlighted list
            foreach (var previewObj in _viewModel.WindowSelectionPreviewObjects)
            {
                highlightedObjects.Add(previewObj);
            }
            var selectedSet = new HashSet<OpenCADObject>(_viewModel.SelectedObjects);

            // Pass highlighting and selection information to the render engine
            _renderEngine.Render(list, highlightedObjects, selectedSet);
        }

        private void CollectDrawable(OpenCADObject parent, HashSet<OpenCADObject> objectSet)
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
                        // HashSet.Add returns false if the object is already in the set
                        bool wasAdded = objectSet.Add(child);
                        if (!wasAdded)
                        {
                            System.Diagnostics.Debug.WriteLine($"[VC] Duplicate object detected: {child.GetType().Name} (ID={child.ID})");
                        }
                    }
                }

                // Recurse to gather all nested drawables
                CollectDrawable(child, objectSet);
            }
        }

        private void RenderPostGeometry()
        {
            if (_renderEngine == null) return;

            //var frameSw = Stopwatch.StartNew();
            var overlayObjects = new List<OpenCADObject>();

            // Add preview line if available (for point picking).
            // Suppress it when a rectangle preview is active — the rectangle replaces the drag line.
            var previewPoint = _viewModel.PreviewPoint;
            var tempPoints = _viewModel.TempPoints;

            if (previewPoint != null && tempPoints.Count > 0 && _viewModel.RectanglePreviewStartPoint == null)
            {
                var lastPoint = tempPoints[tempPoints.Count - 1];
                var previewLine = new Line(lastPoint, previewPoint.Value, _document);
                overlayObjects.Add(previewLine);
            }

            // Window selection rectangle timings
            //var windowSelSw = Stopwatch.StartNew();
            if (_viewModel.CurrentInputMode == ViewportViewModel.InputMode.WindowSelection &&
                _viewModel.WindowSelectionStartPoint != null &&
                _viewModel.WindowSelectionCurrentPoint != null)
            {
                var selEdgeColor = _viewportSettings.Crosshair?.Color ?? System.Drawing.Color.LightBlue;
                var selFillColor = System.Drawing.Color.FromArgb(76, 0, 255, 0); // 30% green

                RenderWindowSelectionFill(
                    _viewModel.WindowSelectionStartPoint.Value,
                    _viewModel.WindowSelectionCurrentPoint.Value,
                    selFillColor);

                var selectionRectLines = CreateWindowSelectionRectangle(
                    _viewModel.WindowSelectionStartPoint.Value,
                    _viewModel.WindowSelectionCurrentPoint.Value,
                    selEdgeColor);
                overlayObjects.AddRange(selectionRectLines);
            }

            if (_viewModel.RectanglePreviewStartPoint != null &&
                _viewModel.RectanglePreviewCurrentPoint != null)
            {
                RenderWindowSelectionFill(
                    _viewModel.RectanglePreviewStartPoint.Value,
                    _viewModel.RectanglePreviewCurrentPoint.Value,
                    _viewModel.RectanglePreviewFillColor);

                var rectLines = CreateWindowSelectionRectangle(
                    _viewModel.RectanglePreviewStartPoint.Value,
                    _viewModel.RectanglePreviewCurrentPoint.Value,
                    _viewModel.RectanglePreviewEdgeColor);
                overlayObjects.AddRange(rectLines);
            }
            //windowSelSw.Stop();

            // GeoPoint glyph timings
            //var geoGlyphSw = Stopwatch.StartNew();
            if (_viewModel.CurrentInputMode == ViewportViewModel.InputMode.PointPicking &&
                _viewModel.GeoPointModes != GeoPointModes.None &&
                _currentMousePosDip.HasValue)
            {
                EnsureTextMetricsInitialized();
                var screenPos = new Point(_currentMousePosDip.Value.X * _renderEngine.Viewport.DpiScaleX,
                    _currentMousePosDip.Value.Y * _renderEngine.Viewport.DpiScaleY);

                var geoPoint = _viewModel.GetGeoPointAtCurrentMousePosition(screenPos, ScreenToWorld2);
                if (geoPoint != null)
                {
                    var worldPos = ScreenToWorld2(screenPos);
                    var worldPos1 = ScreenToWorld2(new Point(screenPos.X + 1, screenPos.Y));
                    if (worldPos.HasValue && worldPos1.HasValue)
                    {
                        double screenToWorldScale = Math.Abs(worldPos1.Value.X - worldPos.Value.X);
                        var geoGlyph = _viewModel.CreateGlyph(geoPoint, screenToWorldScale);
                        //foreach (var glyph in geoGlyphs)
                        //{
                            CollectGlyphChildren(geoGlyph, overlayObjects);
                        //}
                        //overlayObjects.Add(geoGlyph);
                    }
                }
            }
            //geoGlyphSw.Stop();

            // Crosshair timings
            var crosshairSw = Stopwatch.StartNew();
            if (_currentMousePosDip.HasValue)
            {
                var crosshairLines = CreateCrosshairLines(_currentMousePosDip.Value);
                overlayObjects.AddRange(crosshairLines);
            }
            //crosshairSw.Stop();

            // RenderOverlay timings
            var overlayRenderSw = Stopwatch.StartNew();
            if (overlayObjects.Count > 0)
            {
                _renderEngine.RenderOverlay(overlayObjects);
            }
            //overlayRenderSw.Stop();
            //frameSw.Stop();

            // Emit per-frame timing logs (ms) for diagnostics
            //Debug.WriteLine($"[Overlay] frame={frameSw.Elapsed.TotalMilliseconds:F2}ms, windowSel={windowSelSw.Elapsed.TotalMilliseconds:F2}ms, geoGlyphs={geoGlyphSw.Elapsed.TotalMilliseconds:F2}ms, crosshair={crosshairSw.Elapsed.TotalMilliseconds:F2}ms, render={overlayRenderSw.Elapsed.TotalMilliseconds:F2}ms, count={overlayObjects.Count}");
        }

        /// <summary>
        /// Recursively collects all drawable children from a glyph container
        /// </summary>
        private void CollectGlyphChildren(OpenCADObject glyph, List<OpenCADObject> drawableList)
        {
            var children = glyph.GetChildren();
            foreach (var child in children)
            {
                if (child.IsDrawable)
                {
                    drawableList.Add(child);
                }
                
                // Recurse for nested children
                CollectGlyphChildren(child, drawableList);
            }
        }

        /// <summary>
        /// Creates the visual rectangle for window selection
        /// </summary>
        private List<Line> CreateWindowSelectionRectangle(Point3D startPoint, Point3D currentPoint, System.Drawing.Color edgeColor)
        {
            var lines = new List<Line>();

            double minX = Math.Min(startPoint.X, currentPoint.X);
            double maxX = Math.Max(startPoint.X, currentPoint.X);
            double minY = Math.Min(startPoint.Y, currentPoint.Y);
            double maxY = Math.Max(startPoint.Y, currentPoint.Y);

            var bottomLeft  = new Point3D(minX, minY, 0);
            var bottomRight = new Point3D(maxX, minY, 0);
            var topRight    = new Point3D(maxX, maxY, 0);
            var topLeft     = new Point3D(minX, maxY, 0);

            lines.Add(CreateWindowSelectionLine(bottomLeft,  bottomRight, edgeColor));
            lines.Add(CreateWindowSelectionLine(bottomRight, topRight,    edgeColor));
            lines.Add(CreateWindowSelectionLine(topRight,    topLeft,     edgeColor));
            lines.Add(CreateWindowSelectionLine(topLeft,     bottomLeft,  edgeColor));

            return lines;
        }

        /// <summary>
        /// Creates a line for a selection rectangle with the given color.
        /// </summary>
        private Line CreateWindowSelectionLine(Point3D start, Point3D end, System.Drawing.Color edgeColor)
        {
            var line = new Line(start, end, _document);
            line.Color = edgeColor;

            var crosshairSettings = _viewportSettings.Crosshair;
            if (crosshairSettings != null)
            {
                line.LineTypeID  = crosshairSettings.LineTypeID;
                line.LineWeight  = crosshairSettings.LineWeight;
            }

            return line;
        }

        /// <summary>
        /// Renders a filled semi-transparent rectangle.
        /// </summary>
        private void RenderWindowSelectionFill(Point3D startPoint, Point3D currentPoint, System.Drawing.Color fillColor)
        {
            if (_renderEngine == null) return;

            double minX = Math.Min(startPoint.X, currentPoint.X);
            double maxX = Math.Max(startPoint.X, currentPoint.X);
            double minY = Math.Min(startPoint.Y, currentPoint.Y);
            double maxY = Math.Max(startPoint.Y, currentPoint.Y);

            var vertices = new[]
            {
                new Point3D(minX, minY, 0),
                new Point3D(maxX, minY, 0),
                new Point3D(maxX, maxY, 0),
                new Point3D(minX, maxY, 0)
            };

            _renderEngine.RenderFilledPolygon(vertices, fillColor);
        }

        private List<Line> CreateCrosshairLines(Point mousePosDip)
        {
            if (_renderEngine == null)
                return new List<Line>();

            var lines = new List<Line>();

            // Convert mouse position to world coordinates
            var worldPos = ScreenToWorld(mousePosDip);
            if (!worldPos.HasValue)
                return lines;

            // Create the center point - apply snapping if in point picking mode
            var centerPoint = //_viewModel.IsPointPickingMode && _viewModel.SnappingEnabled
                 _viewModel.SnapToGrid(new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z));
                //: new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);

            var bounds = _cachedWorldBounds.HasValue ? _cachedWorldBounds : ComputeViewportWorldBounds();
            
            if (bounds.HasValue)
            {
                var tl = bounds.Value.topLeft;
                var tr = bounds.Value.topRight;
                var bl = bounds.Value.bottomLeft;
                var br = bounds.Value.bottomRight;

                // Horizontal crosshair line (extends to viewport edges)
                lines.Add(CreateCrosshairLine(new Point3D(tl.X, centerPoint.Y, 0),
                                              new Point3D(tr.X, centerPoint.Y, 0)));

                // Vertical crosshair line (extends to viewport edges)
                lines.Add(CreateCrosshairLine(new Point3D(centerPoint.X, tl.Y, 0),
                                              new Point3D(centerPoint.X, bl.Y, 0)));
            }

            // Pickbox/aperture square around center
            // Compute screen-to-world scale using ortho fast path when available
            double screenToWorldScale = 1.0;
            var worldCenter = worldPos;
            var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
            var offsetDip = new Point(mousePosDip.X + 1, mousePosDip.Y);
            var worldOffset = ScreenToWorld(offsetDip);
            if (worldCenter.HasValue && worldOffset.HasValue)
            {
                screenToWorldScale = Math.Abs(worldOffset.Value.X - worldCenter.Value.X);
            }

            int pickboxSizePixels = _viewportSettings.Crosshair?.PickboxSize ?? 5;
            int apertureboxSizePixels = _viewModel.ApertureSize > 0 ? _viewModel.ApertureSize : (_viewportSettings.Crosshair?.PickboxSize * 3 ?? 15);
            var boxSizePixels = _viewModel.IsPointPickingMode && _viewModel.GeoPointModes != GeoPointModes.None ? apertureboxSizePixels : pickboxSizePixels;
            double halfBox = boxSizePixels * screenToWorldScale;

            if (!_viewModel.IsPointPickingMode || _viewModel.GeoPointModes != GeoPointModes.None)
            {
                lines.Add(CreateCrosshairLine(new Point3D(centerPoint.X - halfBox, centerPoint.Y - halfBox, 0),
                                              new Point3D(centerPoint.X + halfBox, centerPoint.Y - halfBox, 0)));
                lines.Add(CreateCrosshairLine(new Point3D(centerPoint.X + halfBox, centerPoint.Y - halfBox, 0),
                                              new Point3D(centerPoint.X + halfBox, centerPoint.Y + halfBox, 0)));
                lines.Add(CreateCrosshairLine(new Point3D(centerPoint.X + halfBox, centerPoint.Y + halfBox, 0),
                                              new Point3D(centerPoint.X - halfBox, centerPoint.Y + halfBox, 0)));
                lines.Add(CreateCrosshairLine(new Point3D(centerPoint.X - halfBox, centerPoint.Y + halfBox, 0),
                                              new Point3D(centerPoint.X - halfBox, centerPoint.Y - halfBox, 0)));
            }

            return lines;
        }

        /// <summary>
        /// Creates a crosshair line with user-defined settings
        /// </summary>
        private Line CreateCrosshairLine(Point3D start, Point3D end)
        {
            var line = new Line(start, end, _document);
            
            var crosshairSettings = _viewportSettings.Crosshair;
            if (crosshairSettings != null)
            {
                line.Color = crosshairSettings.Color;
                line.LineTypeID = crosshairSettings.LineTypeID;
                line.LineWeight = crosshairSettings.LineWeight;
            }
            
            return line;
        }

        /// <summary>
        /// Updates the debug tooltip showing grip state
        /// </summary>
        private void UpdateDebugTooltip()
        {
            if (!_showDebugTooltip || !_currentMousePosDip.HasValue)
            {
                DebugTooltip.Visibility = Visibility.Collapsed;
                return;
            }

            DebugTooltip.Visibility = Visibility.Visible;

            // Position 10 pixels to the right and below cursor
            Canvas.SetLeft(DebugTooltip, _currentMousePosDip.Value.X + 10);
            Canvas.SetTop(DebugTooltip, _currentMousePosDip.Value.Y + 10);
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
            var line = new Line(start, end, _document);
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
                line.LineTypeID = OpenCADDocument.ContinuousLineTypeID;
                line.LineWeight = isMajor ? LineWeight.LineWeight053 : LineWeight.Hairline;
            }

            return line;
        }
        public void Refresh()
        {
            GlWPFControl?.InvalidateVisual();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isInitialized || _renderEngine == null)
                return;

            // 1. Update DPI + pixel size in the Viewport
            _renderEngine.Viewport.UpdateDpi();
            _renderEngine.Viewport.UpdatePixelSize();

            int w = _renderEngine.Viewport.PixelWidth;
            int h = _renderEngine.Viewport.PixelHeight;

            // 2. Resize GL + camera using *physical pixels*
            _renderEngine.ResizeViewport(w, h);
            _renderEngine.Camera.SetViewportSize(w, h);

            Refresh();
        }
        #endregion

        #region Mouse Event Handlers

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var mousePos = e.GetPosition(GlWPFControl);
            _lastMousePosDip = mousePos; // start delta tracking

            var worldPos = ScreenToWorld(mousePos);
            var result = _viewModel.HandleMouseDown(e.ChangedButton, mousePos, worldPos);

            if (result.Handled)
                e.Handled = true;

            // Ensure we capture for panning with middle/right even if VM didn't request it
            if (result.CaptureMouse ||
                e.ChangedButton == MouseButton.Middle)
            {
                GlWPFControl.CaptureMouse();
            }

            if (result.NeedsRefresh)
                Refresh();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_renderEngine == null) return;

            Point currentPosDip = e.GetPosition(GlWPFControl);


            if (e.MiddleButton == MouseButtonState.Pressed && _lastMousePosDip.HasValue)
            {
                //var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
                float dxPx = (float)((currentPosDip.X - _lastMousePosDip.Value.X) * _renderEngine.Viewport.DpiScaleX);
                float dyPx = (float)((currentPosDip.Y - _lastMousePosDip.Value.Y) * _renderEngine.Viewport.DpiScaleY);
                _renderEngine.Camera.Pan(new Vector2(-dxPx, -dyPx));
                _lastMousePosDip = currentPosDip; // update last position for next delta
                Refresh();
                return;
            }

            // Update current mouse position for crosshair rendering
            _currentMousePosDip = currentPosDip;
            
            var worldPos = ScreenToWorld(currentPosDip);

            float panScale = _renderEngine.Camera.PanScale;
            
            Vector3D? vector3D;
            if (worldPos.HasValue)
            {
                vector3D = new Vector3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
            }
            else
            {
                return; // Cannot proceed without valid world position
            }
            var currentPosPx = new System.Drawing.Point((int)(currentPosDip.X * _renderEngine.Viewport.DpiScaleX),
                                                  (int)(currentPosDip.Y * _renderEngine.Viewport.DpiScaleY));
#if DEBUG
            // Set diagnostic tooltip with viewport bounds information
            var upperLeft = new Point(0, 0);
            var lowerRight = new Point(GlWPFControl.ActualWidth, GlWPFControl.ActualHeight);
            var center = new Point(GlWPFControl.ActualWidth / 2, GlWPFControl.ActualHeight / 2);

            _viewModel.DiagnosticToolTip = $"UL: ({upperLeft.X:F0}, {upperLeft.Y:F0}) | " +
                                            $"LR: ({lowerRight.X:F0}, {lowerRight.Y:F0}) | " +
                                            $"Center: ({center.X:F0}, {center.Y:F0})" + Environment.NewLine;
            _viewModel.DiagnosticToolTip += $"ScreenPos: ({currentPosPx.X:F2}, {currentPosPx.Y:F2})" + Environment.NewLine;
            _viewModel.DiagnosticToolTip += worldPos.HasValue ? $"WorldPos: ({worldPos.Value.X:F2}, {worldPos.Value.Y:F2}, {worldPos.Value.Z:F2})" + Environment.NewLine : "WorldPos: null" + Environment.NewLine;
#endif

            var result = _viewModel.HandleMouseMove(new System.Windows.Point(currentPosPx.X, currentPosPx.Y), vector3D, e.MiddleButton, e.RightButton, panScale, out var cameraOp);

            Refresh();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_renderEngine == null)
                return;

            // Convert WPF mouse position to world BEFORE zoom
            Point mousePos = e.GetPosition(GlWPFControl);
            var worldBefore = ScreenToWorld(mousePos);

            if (_renderEngine.ProjectionMode == ProjectionMode.Orthographic)
            {
                // Zoom factor: wheel up = zoom in, wheel down = zoom out
                float zoomFactor = e.Delta > 0 ? 0.9f : 1.1f;

                // Apply zoom
                _renderEngine.Camera.Zoom(zoomFactor);

                // Convert mouse position to world AFTER zoom
                var worldAfter = ScreenToWorld(mousePos);

                if (worldBefore.HasValue && worldAfter.HasValue)
                {
                    // Pan camera so the point under the mouse stays fixed
                    Vector3 delta = worldBefore.Value - worldAfter.Value;
                    _renderEngine.Camera.Pan(new Vector2(delta.X, delta.Y));
                }
            }
            else
            {
                // Perspective zoom = dolly
                float zoomDelta = e.Delta > 0 ? -1f : 1f;
                _renderEngine.Camera.Zoom(zoomDelta);
            }
            _renderEngine.UpdateViewAndProjection();
            UpdateCameraInfoInStatusBar();
            Refresh();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            var mousePos = e.GetPosition(GlWPFControl);
            var worldPos = ScreenToWorld(mousePos);

            // Let the ViewModel handle mouse up (for window selection completion)
            var result = _viewModel.HandleMouseUp(e.ChangedButton, mousePos, worldPos);

            GlWPFControl.ReleaseMouseCapture();
            _lastMousePosDip = null; // stop delta tracking

            if (result.NeedsRefresh)
                Refresh();
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

        #endregion

    #region Coordinate Conversion
        private Vector3? ScreenToWorld(System.Drawing.Point screenPosPx)
        {
            return ScreenToWorld(new Point(screenPosPx.X, screenPosPx.Y));
        }

        public Vector3? ScreenToWorld(Point screenPosDip)
        {
            if (_renderEngine == null)
                return null;

            // Convert DIPs → physical pixels
            float px = (float)(screenPosDip.X * _renderEngine.Viewport.DpiScaleX);
            float py = (float)(screenPosDip.Y * _renderEngine.Viewport.DpiScaleY);

            // Use the pixel-based ScreenToWorld
            var v = _renderEngine.Camera.ScreenToWorld(new System.Drawing.Point((int)px, (int)py));
            return v;
        }

        /// <summary>
        /// Converts a previously scaled screen position
        /// </summary>
        /// <param name="screenPosDip"></param>
        /// <returns></returns>
        public Vector3? ScreenToWorld2(Point screenPosDip)
        {
            if (_renderEngine == null)
                return null;

            // Convert DIPs → physical pixels
            float px = (float)(screenPosDip.X);
            float py = (float)(screenPosDip.Y);

            // Use the pixel-based ScreenToWorld
            var v = _renderEngine.Camera.ScreenToWorld(new System.Drawing.Point((int)px, (int)py));
            return v;
        }        
    
    #endregion

        /// <summary>
        /// Handle ESC key press (called from MainWindow PreviewKeyDown)
        /// </summary>
        /// <returns>True if ESC was handled, false otherwise</returns>
        public bool HandleEscapeKey()
        {
            var result = _viewModel.HandleEscapeKey();
            Refresh();
            return result;
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
                //System.Diagnostics.Debug.WriteLine($"Delete key pressed - erasing {_viewModel.SelectedObjects.Count} selected object(s)");
        
                // Create a list to hold the objects to delete (to avoid modifying collection during iteration)
                var objectsToDelete = _viewModel.SelectedObjects.ToList();
        
                // Remove each selected object
                foreach (var obj in objectsToDelete)
                {
                    _viewModel.RemoveObject(obj);
                    //System.Diagnostics.Debug.WriteLine($"  Deleted: {obj.GetType().Name} (ID: {obj.ID})");
                }
        
                // Clear the selection after deletion
                _viewModel.SelectionManager.ClearSelection();
        
                return true;
            }
    
            return false;
        }

        internal void SetShiftKeyState(bool isShiftPressed)
        {
            _viewModel.IsShiftKeyPressed = isShiftPressed;
        }

        internal void SetCtrlKeyState(bool isCtrlPressed)
        {
            _viewModel.IsCtrlKeyPressed = isCtrlPressed;
        }

        /// <summary>
        /// Updates the camera position and target information in the status bar
        /// </summary>
        private void UpdateCameraInfoInStatusBar()
        {
            if (_renderEngine?.Camera != null)
            {
                _viewModel.UpdateCameraStatus(
                    _renderEngine.Camera.Position,
                    _renderEngine.Camera.Target
                );
            }
        }

    }
}