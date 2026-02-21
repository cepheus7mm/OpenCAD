using GraphicsEngine.Interfaces;
using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using OpenCAD.SegmentSource;
using OpenCAD.TextRendering;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Windows.Controls;

namespace GraphicsEngine
{
    public enum RenderStyle
    {
        Normal,
        Highlighted,
        Selected,
        Preview
    }
    /// <summary>
    /// Main graphics engine for rendering OpenCADObjects using OpenGL
    /// </summary>
    public class RenderEngine
    {
        private readonly List<IRenderer> _renderers = new();
        //private ICamera _camera;
        private ICamera _camera;
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

        private Shader? _shader;
        private readonly SegmentRenderer _segmentRenderer = new SegmentRenderer(new Shader(UnifiedSegmentShaders.VertexShader, UnifiedSegmentShaders.FragmentShader));
        private Viewport _viewport;

        // Optional injected font provider (from UI host)
        private readonly ITextMetricsProvider? _textMetrics;

        public RenderEngine(ITextMetricsProvider? textMetrics = null)
        {
            //_camera = new Camera();
            _camera = new OrthoCamera(40f);
            _textMetrics = textMetrics;
        }

        public Matrix4x4 ViewMatrix => _viewMatrix;
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;

        //public ICamera Camera => _camera;

        public Viewport Viewport => _viewport;

        public ICamera Camera => _camera;

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
                    //if (_projectionMode == ProjectionMode.Orthographic)
                    //    AlignCameraForOrthographic();

                    UpdateProjection(_viewportWidth, _viewportHeight);
                    //System.Diagnostics.Debug.WriteLine($"Projection mode changed to: {_projectionMode}");
                }
            }
        }
        public static Vector2 WorldToNdc(Vector2 p, Matrix4x4 viewProj)
        {
            Vector4 v = Vector4.Transform(new Vector4(p, 0, 1), viewProj);
            return new Vector2(v.X / v.W, v.Y / v.W);
        }

        /// <summary>
        /// Initialize the rendering engine
        /// </summary>
        public void Initialize(GLWpfControl glWPFControl)
        {
            // Make sure a valid and current GL context exists before this point.

            // Create shader program
            _shaderProgram = new ShaderProgram();
            _polylineShaderProgram = new PolylineShaderProgram();
            _fillShaderProgram = new FillShaderProgram();

            _shader = new Shader(UnifiedSegmentShaders.VertexShader, UnifiedSegmentShaders.FragmentShader);

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

            _viewport = new Viewport(glWPFControl) { Camera = _camera };

            _camera.SetViewportSize(_viewport.PixelWidth, _viewport.PixelHeight);

            UpdateProjection(_viewport.PixelWidth, _viewport.PixelHeight);
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
                            HashSet<OpenCADObject> highlightedSet, 
                            HashSet<OpenCADObject>? selectedSet = null,
                            RenderStyle renderStyle = RenderStyle.Normal)
        {


            var segmentSources = objects.OfType<ISegmentSource>().ToList();
            
            float maxSagittaWorld = _viewport.PixelsToWorld(0.5f); // e.g. 0.5px tolerance

            // -------------------------------
            // PASS 1: Normal geometry
            // -------------------------------
            _segmentRenderer.BeginFrame(_viewport, _viewport.ProjectionMatrix);
            foreach (var source in segmentSources)
            {
                var segments = source.GetSegments(maxSagittaWorld);
                if (segments == null || !segments.Any())
                    continue;
                var lineTypeData = source.GetLinetypeGpuData();

                var mode = ComputeSegmentHighlightMode(source, highlightedSet, selectedSet);

                _segmentRenderer.DrawSegments(segments, _viewport, lineTypeData, mode);
            }
            _segmentRenderer.EndFrame();

            GLDiag.Check("End of Render");
        }

        private HighlightMode ComputeSegmentHighlightMode(ISegmentSource source, HashSet<OpenCADObject> highlightedSet, HashSet<OpenCADObject> selectedSet)
        {
            return highlightedSet.Contains((OpenCADObject)source) &&
                selectedSet.Contains((OpenCADObject)source)
                    ? HighlightMode.HoverSelected
                : highlightedSet.Contains((OpenCADObject)source)
                    ? HighlightMode.Hover
                : selectedSet.Contains((OpenCADObject)source)
                    ? HighlightMode.Selected
                : HighlightMode.None;
        }

        public void RenderOverlay(IEnumerable<OpenCADObject> overlayObjects)
        {
            _viewMatrix = _camera.ViewMatrix;
            _projectionMatrix = _camera.ProjectionMatrix;

            // Save states
            bool depthWasEnabled = GL.IsEnabled(EnableCap.DepthTest);
            bool blendWasEnabled = GL.IsEnabled(EnableCap.Blend);

            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            var sources = overlayObjects.Where(o => o is ISegmentSource).ToList();
            foreach (ISegmentSource source in sources)
            {
                var segments = source.GetSegments();
                _segmentRenderer.BeginFrame(_viewport, _viewport.ProjectionMatrix);
                _segmentRenderer.DrawSegments(segments, _viewport, source.GetLinetypeGpuData(), HighlightMode.None);
                _segmentRenderer.EndFrame();
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

            _camera.UpdateProjection((float)width / height);
            _viewMatrix = _camera.ViewMatrix;
            _projectionMatrix = _camera.ProjectionMatrix;

            GLDiag.Check("UpdateProjection");
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
                _camera.Zoom(zoomFactor);
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

            _worldPolygonRenderer?.Render(worldPolygon, _camera.ViewMatrix, _camera.ProjectionMatrix);
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
            _viewMatrix =  _camera.ViewMatrix;
        }

    }
}