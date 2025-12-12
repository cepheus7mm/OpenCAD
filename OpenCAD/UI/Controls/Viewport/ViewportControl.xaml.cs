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
        private bool _documentFullyLoaded = false;

        // Add near the top with other fields
        private readonly List<OpenCADObject> _previewObjects = new List<OpenCADObject>();

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
            _viewModel.SetViewportSettings(_viewportSettings); // Pass settings to ViewModel
                        
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

            // Subscribe to ViewModel events
            _viewModel.RefreshRequested += (s, e) => Refresh();
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
            GlWPFControl.KeyDown += OnKeyDown;
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
        public void ClearObjects() => _viewModel.ClearObjects();
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

        internal void AddPreviewObject(OpenCADObject obj)
        {
            _previewObjects.Add(obj);
            Refresh();
        }

        internal void RemovePreviewObject(OpenCADObject obj)
        {
            _previewObjects.Remove(obj);
            Refresh();
        }

        internal void ClearPreviewObjects()
        {
            _previewObjects.Clear();
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
                ITextMetricsProvider? textMetrics = null;
                if (Application.Current is UI.App app)
                {
                    textMetrics = app.Services.GetService<ITextMetricsProvider>();
                }

                _renderEngine = new RenderEngine(textMetrics);

                var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
                int pixelWidth = Math.Max(1, (int)Math.Round(GlWPFControl.ActualWidth * dpi.DpiScaleX));
                int pixelHeight = Math.Max(1, (int)Math.Round(GlWPFControl.ActualHeight * dpi.DpiScaleY));
                if (pixelWidth <= 0) pixelWidth = 800;
                if (pixelHeight <= 0) pixelHeight = 600;

                _renderEngine.Initialize(pixelWidth, pixelHeight);

                GL.Disable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);

                var error = GL.GetError();
                if (error != ErrorCode.NoError)
                {
                }

                _isInitialized = true;
                Refresh();
            }
            catch (Exception)
            {
            }
        }

        #endregion

        #region Rendering

        private void OnRender(TimeSpan delta)
        {
            if (!_isInitialized || _renderEngine == null)
                return;

            // Don't render until document is fully loaded
            if (!_documentFullyLoaded)
            {
                try
                {
                    var testLayer = _document.CurrentLayer;
                    _documentFullyLoaded = true;
                }
                catch
                {
                    return; // Still not ready
                }
            }

            try
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                RenderGrid();

                RenderSceneFlat(_document);

                RenderPostGeometry();
            }
            catch (Exception)
            {
            }
        }

        private void RenderSceneFlat(OpenCADDocument document)
        {
            if (document == null || _renderEngine == null) return;

            // Use HashSet to prevent duplicates
            var objectSet = new HashSet<OpenCADObject>();
            CollectDrawable(document, objectSet);

            // Add preview objects to the set (HashSet will ignore duplicates)
            foreach (var previewObj in _previewObjects.Where(o => o.IsDrawable))
            {
                objectSet.Add(previewObj);
            }

            // Convert to list for rendering
            var list = objectSet.ToList();

            var highlightedObjects = new List<OpenCADObject>();

            // Add single highlighted object (hover)
            if (_viewModel.HighlightedObject != null)
            {
                highlightedObjects.Add(_viewModel.HighlightedObject);
            }

            // Add window selection preview objects to highlighted list
            foreach (var previewObj in _viewModel.WindowSelectionPreviewObjects)
            {
                if (!highlightedObjects.Contains(previewObj))
                {
                    highlightedObjects.Add(previewObj);
                }
            }

            // Pass highlighting and selection information to the render engine
            _renderEngine.Render(list, highlightedObjects, _viewModel.SelectedObjects);
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

            var overlayObjects = new List<OpenCADObject>();

            // Add preview line if available (for point picking)
            var previewPoint = _viewModel.PreviewPoint;
            var tempPoints = _viewModel.TempPoints;

            if (previewPoint != null && tempPoints.Count > 0)
            {
                var lastPoint = tempPoints[tempPoints.Count - 1];
                var previewLine = new Line(_document, lastPoint, previewPoint);
                overlayObjects.Add(previewLine);
            }

            // Add window selection rectangle if in WindowSelection mode
            if (_viewModel.CurrentInputMode == ViewportViewModel.InputMode.WindowSelection &&
                _viewModel.WindowSelectionStartPoint != null &&
                _viewModel.WindowSelectionCurrentPoint != null)
            {
                // Render the filled rectangle first (so it appears behind the border lines)
                RenderWindowSelectionFill(
                    _viewModel.WindowSelectionStartPoint,
                    _viewModel.WindowSelectionCurrentPoint);
                
                // Then render the border lines on top
                var selectionRectLines = CreateWindowSelectionRectangle(
                    _viewModel.WindowSelectionStartPoint,
                    _viewModel.WindowSelectionCurrentPoint);
                overlayObjects.AddRange(selectionRectLines);
            }
            
            // Add GeoPoint glyphs if in PointPicking mode with GeoPointModes enabled
            if (_viewModel.CurrentInputMode == ViewportViewModel.InputMode.PointPicking &&
                _viewModel.GeoPointModes != GeoPointModes.None &&
                _currentMousePosDip.HasValue)
            {
                var geoPoints = _viewModel.GetGeoPointsAtCurrentMousePosition(_currentMousePosDip.Value, ScreenToWorld);
                if (geoPoints.Any())
                {
                    var worldPos = ScreenToWorld(_currentMousePosDip.Value);
                    var worldPos1 = ScreenToWorld(new Point(_currentMousePosDip.Value.X + 1, _currentMousePosDip.Value.Y));
                    if (worldPos.HasValue && worldPos1.HasValue)
                    {
                        double screenToWorldScale = Math.Abs(worldPos1.Value.X - worldPos.Value.X);
                        var geoGlyphs = _viewModel.CreateGeoPointGlyphs(geoPoints, screenToWorldScale);
                        
                        // UPDATED: Recursively collect glyph children instead of adding glyph containers
                        foreach (var glyph in geoGlyphs)
                        {
                            CollectGlyphChildren(glyph, overlayObjects);
                        }
                    }
                }
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
        private List<Line> CreateWindowSelectionRectangle(Point3D startPoint, Point3D currentPoint)
        {
            var lines = new List<Line>();

            // Calculate rectangle corners
            double minX = Math.Min(startPoint.X, currentPoint.X);
            double maxX = Math.Max(startPoint.X, currentPoint.X);
            double minY = Math.Min(startPoint.Y, currentPoint.Y);
            double maxY = Math.Max(startPoint.Y, currentPoint.Y);

            var bottomLeft = new Point3D(minX, minY, 0);
            var bottomRight = new Point3D(maxX, minY, 0);
            var topRight = new Point3D(maxX, maxY, 0);
            var topLeft = new Point3D(minX, maxY, 0);

            // Create rectangle lines with distinct styling
            lines.Add(CreateWindowSelectionLine(bottomLeft, bottomRight));
            lines.Add(CreateWindowSelectionLine(bottomRight, topRight));
            lines.Add(CreateWindowSelectionLine(topRight, topLeft));
            lines.Add(CreateWindowSelectionLine(topLeft, bottomLeft));

            return lines;
        }

        /// <summary>
        /// Creates a line for the window selection rectangle with appropriate styling
        /// </summary>
        private Line CreateWindowSelectionLine(Point3D start, Point3D end)
        {
            var line = new Line(_document, start, end);

            // Style the window selection rectangle (bright blue, dashed)
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
        /// Renders a filled semi-transparent rectangle for window selection
        /// </summary>
        private void RenderWindowSelectionFill(Point3D startPoint, Point3D currentPoint)
        {
            if (_renderEngine == null) return;

            // Calculate rectangle corners
            double minX = Math.Min(startPoint.X, currentPoint.X);
            double maxX = Math.Max(startPoint.X, currentPoint.X);
            double minY = Math.Min(startPoint.Y, currentPoint.Y);
            double maxY = Math.Max(startPoint.Y, currentPoint.Y);

            // Create the four vertices of the rectangle (counter-clockwise order)
            var vertices = new[]
            {
                new Point3D(minX, minY, 0),  // Bottom-left
                new Point3D(maxX, minY, 0),  // Bottom-right
                new Point3D(maxX, maxY, 0),  // Top-right
                new Point3D(minX, maxY, 0)   // Top-left
            };

            // Create a semi-transparent green color (30% opacity)
            var fillColor = System.Drawing.Color.FromArgb(76, 0, 255, 0); // 76 = 30% of 255

            // Use the RenderEngine's polygon renderer to draw the filled rectangle
            // Note: You may need to add this method to RenderEngine if it doesn't exist
            _renderEngine.RenderFilledPolygon(vertices, fillColor);
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
                var unsnappedPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
                centerPoint = _viewModel.SnapToGrid(unsnappedPoint);
            }
            else
            {
                centerPoint = new Point3D(worldPos.Value.X, worldPos.Value.Y, worldPos.Value.Z);
            }

            // Calculate viewport bounds in world coordinates
            var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
            
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
            double apertureboxSizePixels = _viewModel.ApertureSize > 0 ? _viewModel.ApertureSize : (_viewportSettings.Crosshair?.PickboxSize * 3.0 ?? 15.0);

            // Calculate two points offset by the pickbox size in screen space
            var screenCenter = mousePosDip;
            var screenOffset = new Point(screenCenter.X + 1, screenCenter.Y);
            
            var worldCenter = ScreenToWorld(screenCenter);
            var worldOffset = ScreenToWorld(screenOffset);

            if (worldCenter.HasValue && worldOffset.HasValue)
            {
                // Calculate the world-space distance that corresponds to pickboxSizePixels
                double screenToWorldScale = Math.Abs(worldOffset.Value.X - worldCenter.Value.X);
                var boxSizePixels = _viewModel.IsPointPickingMode && _viewModel.GeoPointModes != GeoPointModes.None ? apertureboxSizePixels : pickboxSizePixels;
                double halfBox = boxSizePixels * screenToWorldScale;
                
                if (!_viewModel.IsPointPickingMode || _viewModel.GeoPointModes != GeoPointModes.None)
                {
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
            }

            // --- Aperture rendering (use ViewModel properties directly) ---
            // Show aperture when ViewModel.GeoPointModes is not None
            //if (_viewModel.GeoPointModes != GeoPointModes.None && _viewModel.IsPointPickingMode)
            //{
            //    // Aperture radius in pixels comes from the ViewModel property (falls back to settings)
            //    double apertureRadiusPixels = _viewModel.ApertureSize > 0 ? _viewModel.ApertureSize : (_viewportSettings.Crosshair?.PickboxSize * 3.0 ?? 16.0);
            //    var apertureRadiusWorld = apertureRadiusPixels * screenToWorldScale;
            //    const int segments = 32;
            //    var screenPoints = new List<Point>(segments);
            //    var worldPoints = new List<Point3D?>(segments);
            //    for (int i = 0; i < segments; ++i)
            //    {
            //        double angle = 2.0 * Math.PI * i / segments;
            //        double sx = centerPoint.X + apertureRadiusWorld * Math.Cos(angle);
            //        double sy = centerPoint.Y + apertureRadiusWorld * Math.Sin(angle);
            //        worldPoints.Add(new Point3D(sx, sy, 0));
            //    }

            //    //foreach (var sp in screenPoints)
            //    //{
            //    //    var wp = ScreenToWorld(sp);
            //    //    if (wp.HasValue)
            //    //        worldPoints.Add(new Point3D(wp.Value.X, wp.Value.Y, wp.Value.Z));
            //    //    else
            //    //        worldPoints.Add(null);
            //    //}

            //    for (int i = 0; i < segments; ++i)
            //    {
            //        var p1 = worldPoints[i];
            //        var p2 = worldPoints[(i + 1) % segments];
            //        if (p1 != null && p2 != null)
            //        {
            //            lines.Add(CreateCrosshairLine(p1, p2));
            //        }
            //    }
            //}

            return lines;
        }

        /// <summary>
        /// Creates a crosshair line with user-defined settings
        /// </summary>
        private Line CreateCrosshairLine(Point3D start, Point3D end)
        {
            var line = new Line(_document, start, end);
            
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
            GlWPFControl?.InvalidateVisual();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isInitialized || _renderEngine == null) return;

            // Convert DIPs to physical pixels for the GL viewport/projection
            var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
            int pixelWidth = Math.Max(1, (int)Math.Round(e.NewSize.Width * dpi.DpiScaleX));
            int pixelHeight = Math.Max(1, (int)Math.Round(e.NewSize.Height * dpi.DpiScaleY));

            _renderEngine.UpdateProjection(pixelWidth, pixelHeight);

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

            float panScale = (_renderEngine.ProjectionMode == GraphicsEngine.ProjectionMode.Orthographic)
                ? _renderEngine.OrthographicScale * 0.02f
                : (_renderEngine.Camera.Position - _renderEngine.Camera.Target).Length() * 0.002f;

            // Only perform hit testing if in selection mode, NOT in point picking or window selection mode, and not dragging
            bool shouldHitTest = _viewModel.CurrentInputMode == ViewportViewModel.InputMode.Selection && 
                e.LeftButton != MouseButtonState.Pressed && 
                e.MiddleButton != MouseButtonState.Pressed && 
                e.RightButton != MouseButtonState.Pressed;

            if (shouldHitTest)
            {
                var hitObject = _viewModel.HitTest(currentPosDip, ScreenToWorld);
                
                if (_viewModel.HighlightedObject != hitObject)
                {
                    _viewModel.HighlightedObject = hitObject;
                    Refresh(); // Force a refresh when highlighting changes
                }
            }
            
            var vector3D = new Vector3D();
            if (worldPos.HasValue)
            {
                vector3D.X = worldPos.Value.X;
                vector3D.Y = worldPos.Value.Y;
                vector3D.Z = worldPos.Value.Z;
            }

            var result = _viewModel.HandleMouseMove(
                currentPosDip,
                vector3D,
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

            // Always refresh to update crosshair position and window selection rectangle
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

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"ViewportControl.OnKeyDown: Key={e.Key}, IsSelectionMode={_viewModel.IsSelectionMode}, SelectedCount={_viewModel.SelectedObjects.Count}");

            // Handle ESC key
            if (e.Key == Key.Escape)
            {
                // Priority 1: Cancel point picking mode if active
                if (_viewModel.IsPointPickingMode)
                {
                    //System.Diagnostics.Debug.WriteLine("ESC pressed - cancelling point picking mode");
                    _viewModel.CancelPointPicking();
                    e.Handled = true;
                    return;
                }

                // Priority 2: Clear selection if there are selected objects
                if (_viewModel.IsSelectionMode && _viewModel.SelectedObjects.Count > 0)
                {
                    //System.Diagnostics.Debug.WriteLine("ESC pressed - clearing selection");
                    _viewModel.ClearSelection();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                // Update status bar when Shift is pressed
                _viewModel.IsShiftKeyPressed = true;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                // Update status bar when Shift is released
                _viewModel.IsShiftKeyPressed = false;
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

                // Perspective fallback: invert PV (column-major) -> use transpose for System.Numerics.row-vector Transform
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
            catch (Exception)
            {
                return null;
            }
        }

        private Point? WorldToScreen(Point3D worldPoint)
        {
            if (_renderEngine == null) return null;

            try
            {
                var dpi = VisualTreeHelper.GetDpi(GlWPFControl);
                float widthPx = Math.Max(1, (float)Math.Round(GlWPFControl.ActualWidth * dpi.DpiScaleX));
                float heightPx = Math.Max(1, (float)Math.Round(GlWPFControl.ActualHeight * dpi.DpiScaleY));

                // Build PV as in ScreenToWorld (projection * view)
                var viewMatrix = _renderEngine.Camera.GetViewMatrix();
                var projectionMatrix = _renderEngine.GetProjectionMatrix();

                Matrix4x4 pv = Matrix4x4.Multiply(projectionMatrix, viewMatrix);

                // Use transpose to match Vector4.Transform row-vector convention (mirrors inverse used in ScreenToWorld)
                var pvRow = Matrix4x4.Transpose(pv);

                var worldV = new Vector4((float)worldPoint.X, (float)worldPoint.Y, (float)worldPoint.Z, 1f);
                var clip = Vector4.Transform(worldV, pvRow);

                if (Math.Abs(clip.W) < 1e-6f)
                    return null;

                var ndc = clip / clip.W;

                // Convert NDC [-1,1] to pixel coords (same convention as ScreenToWorld)
                float px = (ndc.X + 1.0f) * 0.5f * widthPx;
                float py = (1.0f - ndc.Y) * 0.5f * heightPx;

                // Convert pixels back to DIPs for consistency with mousePosDip
                double dipX = px / dpi.DpiScaleX;
                double dipY = py / dpi.DpiScaleY;

                return new Point(dipX, dipY);
            }
            catch
            {
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
            //System.Diagnostics.Debug.WriteLine($"ViewportControl.HandleEscapeKey: IsPointPickingMode={_viewModel.IsPointPickingMode}, IsSelectionMode={_viewModel.IsSelectionMode}, SelectedCount={_viewModel.SelectedObjects.Count}");
            
            // Priority 1: Cancel point picking mode if active
            if (_viewModel.IsPointPickingMode)
            {
                //System.Diagnostics.Debug.WriteLine("ESC handled - cancelling point picking mode");
                _viewModel.CancelPointPicking();
                return true;
            }
            
            // Priority 2: Clear selection if there are selected objects
            if (_viewModel.IsSelectionMode && _viewModel.SelectedObjects.Count > 0)
            {
                //System.Diagnostics.Debug.WriteLine("ESC handled - clearing selection");
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
                _viewModel.ClearSelection();
        
                return true;
            }
    
            return false;
        }

        internal void SetShiftKeyState(bool isShiftPressed)
        {
            _viewModel.IsShiftKeyPressed = isShiftPressed;
        }
    }
}