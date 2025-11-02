using OpenCAD;
using System.Numerics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace GraphicsEngine
{
    /// <summary>
    /// Main graphics engine for rendering OpenCADObjects using OpenGL
    /// </summary>
    public class RenderEngine
    {
        private readonly List<IRenderer> _renderers = new();
        private Camera _camera;
        private Matrix4x4 _projectionMatrix;
        private Matrix4x4 _viewMatrix;
        private ShaderProgram? _shaderProgram;
        private ProjectionMode _projectionMode = ProjectionMode.Orthographic; // Default to orthographic for CAD
        private int _viewportWidth;
        private int _viewportHeight;
        private float _orthographicScale = 10.0f; // How many world units visible in viewport height
        private float _orthoCenterX = 0f;
        private float _orthoCenterY = 0f;

        public RenderEngine()
        {
            _camera = new Camera();
        }

        /// <summary>
        /// Gets or sets the current projection mode
        /// </summary>
        public ProjectionMode ProjectionMode
        {
            get => _projectionMode;
            set
            {
                if (_projectionMode != value)
                {
                    _projectionMode = value;

                    // Lock axes when entering orthographic
                    if (_projectionMode == ProjectionMode.Orthographic)
                        AlignCameraForOrthographic();

                    UpdateProjection(_viewportWidth, _viewportHeight);
                    System.Diagnostics.Debug.WriteLine($"Projection mode changed to: {_projectionMode}");
                }
            }
        }

        /// <summary>
        /// Gets or sets the orthographic scale (world units visible in viewport height)
        /// </summary>
        public float OrthographicScale
        {
            get => _orthographicScale;
            set
            {
                if (Math.Abs(_orthographicScale - value) > 0.0001f)
                {
                    _orthographicScale = Math.Max(0.1f, value); // Prevent zero or negative scale
                    if (_projectionMode == ProjectionMode.Orthographic)
                    {
                        UpdateProjection(_viewportWidth, _viewportHeight);
                    }
                }
            }
        }

        /// <summary>
        /// Initialize the rendering engine
        /// </summary>
        public void Initialize(int width, int height)
        {
            // Make sure a valid and current GL context exists before this point.

            // Create shader program
            _shaderProgram = new ShaderProgram();

            // Set baseline GL state
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Disable(EnableCap.CullFace); // lines don't need culling
            GL.ClearColor(0.08f, 0.08f, 0.08f, 1.0f);

            // Enable debug output and log context info
            GLDiag.TryEnableDebugOutput();
            GLDiag.LogContextInfo();

            UpdateProjection(width, height);
            RegisterDefaultRenderers();

            GLDiag.Check("Initialize end");
            System.Diagnostics.Debug.WriteLine($"RenderEngine initialized with {_projectionMode} projection");
        }

        /// <summary>
        /// Register default renderers for geometry types
        /// </summary>
        private void RegisterDefaultRenderers()
        {
            if (_shaderProgram == null)
                throw new InvalidOperationException("ShaderProgram must be initialized before registering renderers");

            _renderers.Add(new LineRenderer(_shaderProgram));
            System.Diagnostics.Debug.WriteLine($"Registered {_renderers.Count} renderer(s)");
        }

        /// <summary>
        /// Render a collection of OpenCADObjects
        /// </summary>
        public void Render(IEnumerable<OpenCADObject> objects)
        {
            // Clear per frame to avoid accumulation/smearing
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Force a pure orthographic pipeline: no camera view in ortho
            _viewMatrix = (_projectionMode == ProjectionMode.Orthographic)
                ? Matrix4x4.Identity
                : _camera.GetViewMatrix();

            if (_projectionMode != ProjectionMode.Orthographic && _viewMatrix.IsIdentity)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: View matrix is IDENTITY in perspective!");
                System.Diagnostics.Debug.WriteLine($"Camera - Position: {_camera.Position}, Target: {_camera.Target}, Up: {_camera.Up}");
            }

            int count = 0;
            var drawableObjects = objects.Where(o => o.IsDrawable).ToList();
            foreach (var obj in drawableObjects)
            {
                count++;
                RenderObject(obj);
            }

            if (count == 0)
                System.Diagnostics.Debug.WriteLine("Render called with 0 objects.");

            GLDiag.Check("End of Render");
        }

        /// <summary>
        /// Render a collection of OpenCADObjects with optional highlighting
        /// </summary>
        public void Render(IEnumerable<OpenCADObject> objects, OpenCADObject? highlightedObject = null, IEnumerable<OpenCADObject>? selectedObjects = null)
        {
            // Clear per frame to avoid accumulation/smearing
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Force a pure orthographic pipeline: no camera view in ortho
            _viewMatrix = (_projectionMode == ProjectionMode.Orthographic)
                ? Matrix4x4.Identity
                : _camera.GetViewMatrix();

            if (_projectionMode != ProjectionMode.Orthographic && _viewMatrix.IsIdentity)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: View matrix is IDENTITY in perspective!");
                System.Diagnostics.Debug.WriteLine($"Camera - Position: {_camera.Position}, Target: {_camera.Target}, Up: {_camera.Up}");
            }

            // Create a set for fast lookup of selected objects
            var selectedSet = selectedObjects != null ? new HashSet<OpenCADObject>(selectedObjects) : new HashSet<OpenCADObject>();

            int count = 0;
            var drawableObjects = objects.Where(o => o.IsDrawable).ToList();
            foreach (var obj in drawableObjects)
            {
                count++;
                
                // Create render context with highlighting information
                var context = new RenderContext
                {
                    ViewMatrix = _viewMatrix,
                    ProjectionMatrix = _projectionMatrix,
                    IsHighlighted = obj == highlightedObject,
                    IsSelected = selectedSet.Contains(obj)
                };
                
                RenderObject(obj, context);
            }

            if (count == 0)
                System.Diagnostics.Debug.WriteLine("Render called with 0 objects.");

            GLDiag.Check("End of Render");
        }

        public void RenderOverlay(IEnumerable<OpenCADObject> overlayObjects)
        {
            // Match the main pass: in orthographic, force identity view to avoid mixed conventions
            _viewMatrix = (_projectionMode == ProjectionMode.Orthographic)
                ? Matrix4x4.Identity
                : _camera.GetViewMatrix();

            // Save states
            bool depthWasEnabled = GL.IsEnabled(EnableCap.DepthTest);
            bool blendWasEnabled = GL.IsEnabled(EnableCap.Blend);

            // Overlays should render on top without depth fighting, allow alpha
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            int count = 0;
            var drawable = overlayObjects.Where(o => o.IsDrawable).ToList();
            foreach (var obj in drawable)
            {
                count++;
                RenderObject(obj);
            }
            if (count == 0)
                System.Diagnostics.Debug.WriteLine("RenderOverlay called with 0 objects.");

            // Restore states
            if (!blendWasEnabled) GL.Disable(EnableCap.Blend);
            if (depthWasEnabled) GL.Enable(EnableCap.DepthTest);

            GLDiag.Check("End of RenderOverlay");
        }

        /// <summary>
        /// Render a single OpenCADObject
        /// </summary>
        private void RenderObject(OpenCADObject obj)
        {
            var renderer = _renderers.FirstOrDefault(r => r.CanRender(obj));
            if (renderer != null)
            {
                renderer.Render(obj, _viewMatrix, _projectionMatrix);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"No renderer found for type {obj.GetType().FullName}");
            }
        }

        /// <summary>
        /// Render a single OpenCADObject with context
        /// </summary>
        private void RenderObject(OpenCADObject obj, RenderContext context)
        {
            var renderer = _renderers.FirstOrDefault(r => r.CanRender(obj));
            if (renderer != null)
            {
                renderer.Render(obj, context);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"No renderer found for type {obj.GetType().FullName}");
            }
        }

        /// <summary>
        /// Update projection matrix when viewport size changes
        /// </summary>
        public void UpdateProjection(int width, int height)
        {
            if (width <= 0 || height <= 0) return;

            _viewportWidth = width;
            _viewportHeight = height;

            GL.Viewport(0, 0, width, height);

            float aspectRatio = (float)width / height;

            if (_projectionMode == ProjectionMode.Orthographic)
            {
                // Use centered ortho window to support pan without touching the camera
                _projectionMatrix = CreateOrthographicGL(
                    _orthographicScale, aspectRatio, 0.1f, 1000.0f,
                    _orthoCenterX, _orthoCenterY);

                // DEBUG: dump the matrix shape we expect for a pure ortho (no shear/tilt)
                var m = _projectionMatrix;
                Debug.WriteLine($"[RE] Ortho: center=({_orthoCenterX:F4},{_orthoCenterY:F4}) scale={_orthographicScale:F4} aspect={aspectRatio:F4} size=({(_orthographicScale*aspectRatio):F4}x{_orthographicScale:F4})");
                Debug.WriteLine($"[RE] Ortho M2x2=[[{m.M11:F6},{m.M12:F6}],[{m.M21:F6},{m.M22:F6}]]  T=({m.M41:F6},{m.M42:F6})  M34={m.M34:F6} M44={m.M44:F6}");
                if (MathF.Abs(m.M12) > 1e-6f || MathF.Abs(m.M21) > 1e-6f)
                    Debug.WriteLine("[RE][WARN] Ortho off-diagonal != 0 (shear/tilt) in projection.");
            }
            else
            {
                _projectionMatrix = CreatePerspectiveFieldOfViewGL(
                    MathF.PI / 4f,
                    aspectRatio,
                    0.1f,
                    1000.0f
                );
            }

            // Log viewport sanity
            int[] vp = new int[4];
            GL.GetInteger(GetPName.Viewport, vp);
            System.Diagnostics.Debug.WriteLine($"Projection updated ({_projectionMode}): {width}x{height}, GL viewport: {vp[2]}x{vp[3]} at ({vp[0]},{vp[1]})");

            GLDiag.Check("UpdateProjection");
        }

        /// <summary>
        /// Zoom the orthographic view (adjust scale)
        /// </summary>
        public void ZoomOrthographic(float delta)
        {
            if (_projectionMode == ProjectionMode.Orthographic)
            {
                // Decrease scale to zoom in, increase to zoom out
                OrthographicScale += delta;
                System.Diagnostics.Debug.WriteLine($"Orthographic scale: {_orthographicScale:F2}");
            }
        }

        /// <summary>
        /// Gets the current projection matrix
        /// </summary>
        public Matrix4x4 GetProjectionMatrix()
        {
            return _projectionMatrix;
        }

        // OpenGL (RH) perspective matrix with clip depth -1..1
        private static Matrix4x4 CreatePerspectiveFieldOfViewGL(float fovy, float aspect, float zNear, float zFar)
        {
            if (fovy <= 0 || fovy >= MathF.PI) throw new ArgumentOutOfRangeException(nameof(fovy));
            if (aspect <= 0) throw new ArgumentOutOfRangeException(nameof(aspect));
            if (zNear <= 0 || zFar <= 0 || zNear >= zFar) throw new ArgumentOutOfRangeException(nameof(zNear));

            float f = 1f / MathF.Tan(fovy / 2f);

            Matrix4x4 m = new Matrix4x4();
            m.M11 = f / aspect;
            m.M22 = f;
            m.M33 = (zFar + zNear) / (zNear - zFar);
            m.M34 = -1f;
            m.M43 = (2f * zFar * zNear) / (zNear - zFar);
            m.M44 = 0f;
            return m;
        }

        // OpenGL (RH) orthographic matrix with clip depth -1..1
        private static Matrix4x4 CreateOrthographicGL(float scale, float aspect, float zNear, float zFar)
            => CreateOrthographicGL(scale, aspect, zNear, zFar, 0f, 0f);

        // Centered ortho: panning is just changing (centerX, centerY)
        private static Matrix4x4 CreateOrthographicGL(float scale, float aspect, float zNear, float zFar, float centerX, float centerY)
        {
            if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            if (aspect <= 0) throw new ArgumentOutOfRangeException(nameof(aspect));
            if (zNear >= zFar) throw new ArgumentOutOfRangeException(nameof(zNear));

            float height = scale;
            float width = height * aspect;

            float left = centerX - width / 2f;
            float right = centerX + width / 2f;
            float bottom = centerY - height / 2f;
            float top = centerY + height / 2f;

            Matrix4x4 m = new Matrix4x4();
            m.M11 = 2f / (right - left);
            m.M22 = 2f / (top - bottom);
            m.M33 = -2f / (zFar - zNear);
            m.M41 = -(right + left) / (right - left);
            m.M42 = -(top + bottom) / (top - bottom);
            m.M43 = -(zFar + zNear) / (zFar - zNear);
            m.M44 = 1f;
            return m;
        }

        public Camera Camera => _camera;

        private void AlignCameraForOrthographic()
        {
            // Keep camera looking straight down -Z with +Y up, preserve radius
            float radius = (_camera.Position - _camera.Target).Length();
            if (radius < 0.0001f) radius = 10f;

            _camera.Up = Vector3.UnitY;
            _camera.Position = new Vector3(_camera.Target.X, _camera.Target.Y, _camera.Target.Z + radius);

            // Keep ortho window centered on the target when switching modes
            _orthoCenterX = _camera.Target.X;
            _orthoCenterY = _camera.Target.Y;
        }

        /// <summary>
        /// Pan the orthographic view by adjusting the camera position
        /// </summary>
        public void PanOrthoPixels(float deltaXpx, float deltaYpx)
        {
            if (_projectionMode != ProjectionMode.Orthographic)
            {
                _camera.Pan(deltaXpx, deltaYpx);
                return;
            }

            // Ignore tiny/no movement to avoid needless projection rebuilds
            if (MathF.Abs(deltaXpx) < 0.001f && MathF.Abs(deltaYpx) < 0.001f)
                return;

            if (_viewportWidth == 0 || _viewportHeight == 0) return;

            float worldHeight = _orthographicScale;
            float worldWidth = worldHeight * ((float)_viewportWidth / _viewportHeight);

            float worldPerPixelX = worldWidth / _viewportWidth;
            float worldPerPixelY = worldHeight / _viewportHeight;

            float dxWorld = -deltaXpx * worldPerPixelX;
            float dyWorld = -deltaYpx * worldPerPixelY;

            _orthoCenterX += dxWorld;
            _orthoCenterY -= dyWorld;

            Debug.WriteLine($"[RE] PanOrthoPixels px=({deltaXpx:F3},{deltaYpx:F3}) world=({dxWorld:F3},{dyWorld:F3}) center=({_orthoCenterX:F3},{_orthoCenterY:F3})");

            UpdateProjection(_viewportWidth, _viewportHeight);
        }

        /// <summary>
        /// Convert framebuffer pixel coordinates to world coordinates for orthographic top view.
        /// Returns Z=worldZ (default 0). Requires ProjectionMode == Orthographic.
        /// </summary>
        public Vector3 ScreenToWorldOrthoPixels(float xPx, float yPx, float worldZ = 0f)
        {
            if (_projectionMode != ProjectionMode.Orthographic)
                throw new InvalidOperationException("ScreenToWorldOrthoPixels is valid only in Orthographic mode.");

            if (_viewportWidth <= 0 || _viewportHeight <= 0)
                return new Vector3(_orthoCenterX, _orthoCenterY, worldZ);

            float aspect = (float)_viewportWidth / _viewportHeight;

            // Current ortho window dimensions in world units
            float worldHeight = _orthographicScale;
            float worldWidth = worldHeight * aspect;

            // Convert pixels -> NDC
            float ndcX = (xPx / _viewportWidth) * 2f - 1f;
            float ndcY = 1f - (yPx / _viewportHeight) * 2f; // top=+1

            // Map NDC -> world using the centered ortho window
            float halfW = worldWidth * 0.5f;
            float halfH = worldHeight * 0.5f;

            float worldX = _orthoCenterX + ndcX * halfW;
            float worldY = _orthoCenterY + ndcY * halfH;

            return new Vector3(worldX, worldY, worldZ);
        }
    }
}