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

namespace UI.Controls.Viewport
{
    public partial class ViewportControl : UserControl
    {
        private RenderEngine? _renderEngine;
        private readonly List<OpenCADObject> _objects = new();
        private bool _isInitialized = false;
        private Point _lastMousePos;
        private bool _firstRender = true;
        private StatusBarControl? _statusBar;

        public ViewportControl()
        {
            InitializeComponent();

            System.Diagnostics.Debug.WriteLine("=== ViewportControl constructor called ===");

            // GlControl is already defined in XAML with x:Name="GlControl", so no need to find it
            if (GlControl == null)
                throw new InvalidOperationException("GlControl not found. Make sure it is defined in XAML with x:Name=\"GlControl\".");

            var settings = new GLWpfControlSettings
            {
                MajorVersion = 3,
                MinorVersion = 3,
                RenderContinuously = false, // Only render on-demand
            };

            GlControl.Start(settings);
            System.Diagnostics.Debug.WriteLine("GLWpfControl.Start() called with RenderContinuously = false");

            // Subscribe to events
            GlControl.Render += OnRender;
            GlControl.SizeChanged += OnSizeChanged;
            GlControl.Ready += OnGLControlReady;
            this.Loaded += ViewportControl_Loaded;

            // Mouse events
            GlControl.MouseDown += OnMouseDown;
            GlControl.MouseMove += OnMouseMove;
            GlControl.MouseWheel += OnMouseWheel;
            GlControl.MouseUp += OnMouseUp;
            GlControl.MouseEnter += OnMouseEnter;
            GlControl.MouseLeave += OnMouseLeave;

            System.Diagnostics.Debug.WriteLine("=== ViewportControl constructor completed ===");
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
                var versionParts = version.Split('.');
                if (versionParts.Length >= 2)
                {
                    int major = int.Parse(versionParts[0]);
                    int minor = int.Parse(versionParts[1].Split(' ')[0]);
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
                var width = (int)GlControl.ActualWidth;
                var height = (int)GlControl.ActualHeight;
                
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
                
                System.Diagnostics.Debug.WriteLine($"OpenGL initialized successfully");
                System.Diagnostics.Debug.WriteLine($"Objects in scene: {_objects.Count}");
                
                // Log all existing objects
                foreach (var obj in _objects)
                {
                    System.Diagnostics.Debug.WriteLine($"  Existing object: {obj.GetType().Name}");
                    if (obj is Line line)
                    {
                        System.Diagnostics.Debug.WriteLine($"    Line: ({line.Start.X}, {line.Start.Y}, {line.Start.Z}) -> ({line.End.X}, {line.End.Y}, {line.End.Z})");
                    }
                }
                
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

                // Log only the first render and when explicitly requested
                if (_firstRender)
                {
                    System.Diagnostics.Debug.WriteLine($">>> First render: {_objects.Count} objects");
                    _firstRender = false;
                }

                _renderEngine.Render(_objects);

                // Check for OpenGL errors (only on first render or when debugging)
                if (!_firstRender)
                {
                    var error = GL.GetError();
                    if (error != ErrorCode.NoError)
                    {
                        System.Diagnostics.Debug.WriteLine($"!!! OpenGL Error during render: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"!!! EXCEPTION during render: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
            _lastMousePos = e.GetPosition(GlControl);
            GlControl.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_renderEngine == null) return;

            Point currentPos = e.GetPosition(GlControl);
            double dx = currentPos.X - _lastMousePos.X;
            double dy = currentPos.Y - _lastMousePos.Y;

            // Convert screen coordinates to world coordinates (on Z=0 plane)
            var worldPos = ScreenToWorld(currentPos);
            if (worldPos.HasValue)
            {
                _statusBar?.UpdatePositionText($"X: {worldPos.Value.X:F3}  Y: {worldPos.Value.Y:F3}  Z: {worldPos.Value.Z:F3}");
            }
            else
            {
                // Fallback to screen coordinates if unprojection fails
                _statusBar?.UpdatePositionText($"Screen: {currentPos.X:F0}, {currentPos.Y:F0}");
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
        private Vector3? ScreenToWorld(Point screenPos)
        {
            if (_renderEngine == null) return null;

            try
            {
                // Get viewport dimensions
                float width = (float)GlControl.ActualWidth;
                float height = (float)GlControl.ActualHeight;

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
            float width = (float)GlControl.ActualWidth;
            float height = (float)GlControl.ActualHeight;

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
            GlControl.ReleaseMouseCapture();
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
            _objects.Add(obj);
            System.Diagnostics.Debug.WriteLine($"Added object: {obj.GetType().Name}. Total objects: {_objects.Count}");

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
            _objects.Remove(obj);
            System.Diagnostics.Debug.WriteLine($"Removed object. Total objects: {_objects.Count}");

            // Trigger a render when objects are removed
            Refresh();
        }

        /// <summary>
        /// Clear all objects from the scene
        /// </summary>
        public void ClearObjects()
        {
            _objects.Clear();
            System.Diagnostics.Debug.WriteLine("Cleared all objects");

            // Trigger a render after clearing
            Refresh();
        }

        /// <summary>
        /// Get all objects in the scene
        /// </summary>
        public IReadOnlyList<OpenCADObject> GetObjects() => _objects.AsReadOnly();

        /// <summary>
        /// Force a refresh of the viewport (triggers a single render)
        /// </summary>
        public void Refresh()
        {
            GlControl?.InvalidateVisual();
        }
    }
}