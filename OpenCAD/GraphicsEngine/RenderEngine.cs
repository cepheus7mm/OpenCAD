using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.TextRendering;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;

namespace GraphicsEngine
{
    /// <summary>
    /// Main graphics engine for rendering OpenCADObjects using OpenGL
    /// </summary>
    public class RenderEngine
    {
        private readonly List<IRenderer> _renderers = new();
        private Camera _camera;
        private OrthoCamera _orthoCamera;
        private Matrix4x4 _projectionMatrix;
        private Matrix4x4 _viewMatrix;
        private ShaderProgram? _shaderProgram;
        private PolylineShaderProgram? _polylineShaderProgram;
        private FillShaderProgram? _fillShaderProgram;
        private PolygonRenderer? _polygonRenderer;  // ADD THIS LINE
        private WorldPolygonRenderer? _worldPolygonRenderer;
        private ProjectionMode _projectionMode = ProjectionMode.Orthographic; // Default to orthographic for CAD
        private int _viewportWidth;
        private int _viewportHeight;

        // Optional injected font provider (from UI host)
        private readonly ITextMetricsProvider? _textMetrics;

        public RenderEngine(ITextMetricsProvider? textMetrics = null)
        {
            _camera = new Camera();
            _orthoCamera = new OrthoCamera();
            _textMetrics = textMetrics;
        }

        public Matrix4x4 ViewMatrix => _viewMatrix;
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;

        public Camera Camera => _camera;

        public OrthoCamera OrthoCamera => _orthoCamera;

        public float AspectRatio => _viewportHeight > 0 ? (float)_viewportWidth / _viewportHeight : 1f;

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
                    //System.Diagnostics.Debug.WriteLine($"Projection mode changed to: {_projectionMode}");
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
            _polylineShaderProgram = new PolylineShaderProgram();
            _fillShaderProgram = new FillShaderProgram();

            // Initialize polygon renderer used for filled overlays (window selection)
            // This was missing previously which caused RenderFilledPolygon to be a no-op.
            _polygonRenderer = new PolygonRenderer(_fillShaderProgram);
            _worldPolygonRenderer = new WorldPolygonRenderer(_fillShaderProgram);



            // Set baseline GL state
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Disable(EnableCap.CullFace); // lines don't need culling
            GL.ClearColor(0.08f, 0.08f, 0.08f, 1.0f);

            // Enable debug output and log context info
           // GLDiag.TryEnableDebugOutput();
            GLDiag.LogContextInfo();

            UpdateProjection(width, height);
            RegisterDefaultRenderers();

            GLDiag.Check("Initialize end");
            //System.Diagnostics.Debug.WriteLine($"RenderEngine initialized with {_projectionMode} projection");
        }

        /// <summary>
        /// Register default renderers for geometry types
        /// </summary>
        private void RegisterDefaultRenderers()
        {
            if (_shaderProgram == null || _polylineShaderProgram == null)
                throw new InvalidOperationException("ShaderProgram must be initialized before registering renderers");

            _renderers.Clear();
            _renderers.Add(new LineRenderer(_shaderProgram));
            _renderers.Add(new ArcRenderer(_shaderProgram));
            _renderers.Add(new PolylineRenderer(_polylineShaderProgram));

            // Register TextRenderer when metrics are available (lazy)
            if (_textMetrics != null)
            {
                _renderers.Add(new TextRenderer(_shaderProgram, _textMetrics));
            }

            System.Diagnostics.Debug.WriteLine($"Registered {_renderers.Count} renderer(s)");
        }

        /// <summary>
        /// Render a collection of OpenCADObjects with optional highlighting and selection
        /// </summary>
        public void Render(IEnumerable<OpenCADObject> objects, 
                          IEnumerable<OpenCADObject>? highlightedObjects = null, 
                          IEnumerable<OpenCADObject>? selectedObjects = null)
        {
            // Compute correct camera matrices
            float aspect = (float)_viewportWidth / _viewportHeight;

            if (_projectionMode == ProjectionMode.Orthographic)
            {
                _viewMatrix = _orthoCamera.GetViewMatrix();
                _projectionMatrix = _orthoCamera.GetProjectionMatrix(aspect);
            }
            else
            {
                _viewMatrix = _camera.GetViewMatrix();
                _projectionMatrix = _camera.GetProjectionMatrix(aspect);
            }


            // Create sets for fast lookup
            var highlightedSet = highlightedObjects != null 
                ? new HashSet<OpenCADObject>(highlightedObjects) 
                : new HashSet<OpenCADObject>();
            var selectedSet = selectedObjects != null 
                ? new HashSet<OpenCADObject>(selectedObjects) 
                : new HashSet<OpenCADObject>();

            int count = 0;
            var drawableObjects = objects.Where(o => o.IsDrawable).ToList();
            int[] vp = new int[4];
            GL.GetInteger(GetPName.Viewport, vp);

            foreach (var obj in drawableObjects)
            {
                count++;
                
                // Create context with highlighting/selection state
                var context = new RenderContext
                {
                    ViewMatrix = _viewMatrix,
                    ProjectionMatrix = _projectionMatrix,
                    Viewport = new Vector2(vp[2], vp[3]),
                    IsHighlighted = highlightedSet.Contains(obj),
                    IsSelected = selectedSet.Contains(obj)
                };
                
                // Render with context
                var renderer = _renderers.FirstOrDefault(r => r.CanRender(obj));
                if (renderer != null)
                {
                    renderer.Render(obj, context);
                }
            }

            GLDiag.Check("End of Render");
        }

        public void RenderOverlay(IEnumerable<OpenCADObject> overlayObjects)
        {
            float aspect = (float)_viewportWidth / _viewportHeight;

            if (_projectionMode == ProjectionMode.Orthographic)
            {
                _viewMatrix = _orthoCamera.GetViewMatrix();
                _projectionMatrix = _orthoCamera.GetProjectionMatrix(aspect);
            }
            else
            {
                _viewMatrix = _camera.GetViewMatrix();
                _projectionMatrix = _camera.GetProjectionMatrix(aspect);
            }

            // Save states
            bool depthWasEnabled = GL.IsEnabled(EnableCap.DepthTest);
            bool blendWasEnabled = GL.IsEnabled(EnableCap.Blend);

            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            var drawable = overlayObjects.Where(o => o.IsDrawable).ToList();
            foreach (var obj in drawable)
            {
                var context = new RenderContext
                {
                    ViewMatrix = _viewMatrix,
                    ProjectionMatrix = _projectionMatrix,
                    IsHighlighted = false,  // Overlays are never highlighted
                    IsSelected = false       // Overlays are never selected
                };
                
                var renderer = _renderers.FirstOrDefault(r => r.CanRender(obj));
                if (renderer != null)
                {
                    renderer.Render(obj, context);
                }
            }

            // Restore states
            if (!blendWasEnabled) GL.Disable(EnableCap.Blend);
            if (depthWasEnabled) GL.Enable(EnableCap.DepthTest);

            GLDiag.Check("End of RenderOverlay");
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
                //System.Diagnostics.Debug.WriteLine($"No renderer found for type {obj.GetType().FullName}");
            }
        }

        /// <summary>
        /// Update projection matrix when viewport size changes
        /// </summary>
        public void UpdateProjection(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            _viewportWidth = width;
            _viewportHeight = height;
            try
            {
                GL.Viewport(0, 0, width, height);
            }
            catch (Exception)
            {
            }

            float aspect = (float)width / height;

            if (_projectionMode == ProjectionMode.Orthographic)
            {
                // Use the new OrthoCamera
                _projectionMatrix = _orthoCamera.GetProjectionMatrix(aspect);
            }
            else
            {
                // Use your existing perspective camera
                _projectionMatrix = _camera.GetProjectionMatrix(aspect);
            }

            //GLDiag.Check("UpdateProjection");
            //if (width <= 0 || height <= 0) return;

            //_viewportWidth = width;
            //_viewportHeight = height;

            //GL.Viewport(0, 0, width, height);

            //float aspectRatio = (float)width / height;

            //if (_projectionMode == ProjectionMode.Orthographic)
            //{
            //    // Use centered ortho window to support pan without touching the camera
            //    _projectionMatrix = CreateOrthographicGL(
            //        _orthographicScale, aspectRatio, 0.1f, 1000.0f,
            //        _orthoCenterX, _orthoCenterY);

            //    // DEBUG: dump the matrix shape we expect for a pure ortho (no shear/tilt)
            //    var m = _projectionMatrix;
            //    //Debug.WriteLine($"[RE] Ortho: center=({_orthoCenterX:F4},{_orthoCenterY:F4}) scale={_orthographicScale:F4} aspect={aspectRatio:F4} size=({(_orthographicScale*aspectRatio):F4}x{_orthographicScale:F4})");
            //    //Debug.WriteLine($"[RE] Ortho M2x2=[[{m.M11:F6},{m.M12:F6}],[{m.M21:F6},{m.M22:F6}]]  T=({m.M41:F6},{m.M42:F6})  M34={m.M34:F6} M44={m.M44:F6}");
            //    if (MathF.Abs(m.M12) > 1e-6f || MathF.Abs(m.M21) > 1e-6f)
            //        Debug.WriteLine("[RE][WARN] Ortho off-diagonal != 0 (shear/tilt) in projection.");
            //}
            //else
            //{
            //    _projectionMatrix = CreatePerspectiveFieldOfViewGL(
            //        MathF.PI / 4f,
            //        aspectRatio,
            //        0.1f,
            //        1000.0f
            //    );
            //}

            //// Log viewport sanity
            //int[] vp = new int[4];
            //GL.GetInteger(GetPName.Viewport, vp);
            ////System.Diagnostics.Debug.WriteLine($"Projection updated ({_projectionMode}): {width}x{height}, GL viewport: {vp[2]}x{vp[3]} at ({vp[0]},{vp[1]})");

            //GLDiag.Check("UpdateProjection");
        }

        /// <summary>
        /// Zoom the orthographic view (adjust scale)
        /// </summary>
        public void ZoomOrthographic(float delta)
        {
            if (_projectionMode == ProjectionMode.Orthographic)
            {
                // Convert delta into a zoom factor
                // delta > 0 = zoom in, delta < 0 = zoom out
                float zoomFactor = 1.0f + delta;

                // Use the new OrthoCamera zoom
                _orthoCamera.Zoom(zoomFactor);
            }
            //if (_projectionMode == ProjectionMode.Orthographic)
            //{
            //    // Decrease scale to zoom in, increase to zoom out
            //    OrthographicScale += delta;
            //    //System.Diagnostics.Debug.WriteLine($"Orthographic scale: {_orthographicScale:F2}");
            //}
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
        //private static Matrix4x4 CreateOrthographicGL(float scale, float aspect, float zNear, float zFar)
        //    => CreateOrthographicGL(scale, aspect, zNear, zFar, 0f, 0f);

        //// Centered ortho: panning is just changing (centerX, centerY)
        //private static Matrix4x4 CreateOrthographicGL(float scale, float aspect, float zNear, float zFar, float centerX, float centerY)
        //{
        //    if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        //    if (aspect <= 0) throw new ArgumentOutOfRangeException(nameof(aspect));
        //    if (zNear >= zFar) throw new ArgumentOutOfRangeException(nameof(zNear));

        //    float height = scale;
        //    float width = height * aspect;

        //    float left = centerX - width / 2f;
        //    float right = centerX + width / 2f;
        //    float bottom = centerY - height / 2f;
        //    float top = centerY + height / 2f;

        //    Matrix4x4 m = new Matrix4x4();
        //    m.M11 = 2f / (right - left);
        //    m.M22 = 2f / (top - bottom);
        //    m.M33 = -2f / (zFar - zNear);
        //    m.M41 = -(right + left) / (right - left);
        //    m.M42 = -(top + bottom) / (top - bottom);
        //    m.M43 = -(zFar + zNear) / (zFar - zNear);
        //    m.M44 = 1f;
        //    return m;
        //}

        private void AlignCameraForOrthographic()
        {
            // Reset the orthographic camera to a clean top-down view
            _orthoCamera.Up = Vector3.UnitY;

            // Look straight down the Z axis
            _orthoCamera.Position = new Vector3(0, 0, 10);
            _orthoCamera.Target = new Vector3(0, 0, 0);

            // Set a reasonable default zoom level (world units across horizontally)
            _orthoCamera.OrthoWidth = 100f;
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

            if (_viewportWidth == 0 || _viewportHeight == 0)
                return;

            // Use the new OrthoCamera pan logic
            _orthoCamera.Pan(
                new Vector2(deltaXpx, deltaYpx),
                _viewportWidth,
                _viewportHeight
            );

            //if (_projectionMode != ProjectionMode.Orthographic)
            //{
            //    _camera.Pan(deltaXpx, deltaYpx);
            //    return;
            //}

            //// Ignore tiny/no movement to avoid needless projection rebuilds
            //if (MathF.Abs(deltaXpx) < 0.001f && MathF.Abs(deltaYpx) < 0.001f)
            //    return;

            //if (_viewportWidth == 0 || _viewportHeight == 0) return;

            //float worldHeight = _orthographicScale;
            //float worldWidth = worldHeight * ((float)_viewportWidth / _viewportHeight);

            //float worldPerPixelX = worldWidth / _viewportWidth;
            //float worldPerPixelY = worldHeight / _viewportHeight;

            //float dxWorld = -deltaXpx * worldPerPixelX;
            //float dyWorld = -deltaYpx * worldPerPixelY;

            //_orthoCenterX += dxWorld;
            //_orthoCenterY -= dyWorld;

            ////Debug.WriteLine($"[RE] PanOrthoPixels px=({deltaXpx:F3},{deltaYpx:F3}) world=({dxWorld:F3},{dyWorld:F3}) center=({_orthoCenterX:F3},{_orthoCenterY:F3})");

            //UpdateProjection(_viewportWidth, _viewportHeight);
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
                return Vector3.Zero;

            // 1. Convert screen pixel → NDC
            float ndcX = (xPx / _viewportWidth) * 2f - 1f;
            float ndcY = 1f - (yPx / _viewportHeight) * 2f;

            // 2. Build clip-space points at near and far
            Vector4 clipNear = new Vector4(ndcX, ndcY, -1f, 1f);
            Vector4 clipFar = new Vector4(ndcX, ndcY, 1f, 1f);

            // 3. Unproject using inverse MVP
            Matrix4x4 mvp = _viewMatrix * _projectionMatrix;
            Matrix4x4.Invert(mvp, out Matrix4x4 invMvp);

            Vector4 worldNear4 = Vector4.Transform(clipNear, invMvp);
            Vector4 worldFar4 = Vector4.Transform(clipFar, invMvp);

            Vector3 worldNear = new Vector3(worldNear4.X / worldNear4.W,
                                            worldNear4.Y / worldNear4.W,
                                            worldNear4.Z / worldNear4.W);

            Vector3 worldFar = new Vector3(worldFar4.X / worldFar4.W,
                                           worldFar4.Y / worldFar4.W,
                                           worldFar4.Z / worldFar4.W);

            // 4. Ray-plane intersection with Z = worldZ
            Vector3 rayDir = Vector3.Normalize(worldFar - worldNear);

            if (Math.Abs(rayDir.Z) < 1e-6f)
                return worldNear; // ray parallel to plane

            float t = (worldZ - worldNear.Z) / rayDir.Z;
            return worldNear + rayDir * t;
        }

        /// <summary>
        /// Renders a filled polygon with the specified color (for window selection)
        /// </summary>
        public void RenderFilledPolygon(Point3D[] vertices, System.Drawing.Color fillColor)
        {
            if (_polygonRenderer == null) return;
            
            // Convert OpenCAD.Geometry.Point3D array to System.Numerics.Vector3 array
            var points = vertices.Select(v => new Vector3((float)v.X, (float)v.Y, (float)v.Z)).ToArray();


            // Render as a filled polygon
            //_polygonRenderer.RenderFilled(points, fillColor, _viewMatrix, _projectionMatrix);
            var worldPolygon = new WorldPolygon(points, fillColor);

            _worldPolygonRenderer?.Render(worldPolygon, _orthoCamera.GetViewMatrix(), _orthoCamera.GetProjectionMatrix(AspectRatio));
        }

        public void ResizeViewport(int width, int height)
        {
            UpdateProjection(width, height);
            UpdateViewMatrix();
        }

        public void UpdateViewAndProjection()
        {
            UpdateProjection(_viewportWidth, _viewportHeight);
            UpdateViewMatrix();
        }

        public void UpdateViewMatrix()
        {
            _viewMatrix = (_projectionMode == ProjectionMode.Orthographic)
                ? _orthoCamera.GetViewMatrix()
                : _camera.GetViewMatrix();
        }
    }
}