using GraphicsEngine.Interfaces;
using OpenCAD;
using OpenCAD.Dimensions;
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
        private ICamera _camera;
        private Matrix4x4 _projectionMatrix;
        private Matrix4x4 _viewMatrix;
        private ShaderProgram? _shaderProgram;
        private FillShaderProgram? _fillShaderProgram;
        private WorldPolygonRenderer? _worldPolygonRenderer;
        private TextRenderer? _textRenderer;
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
            _camera = new OrthoCamera(40f);
            _textMetrics = textMetrics;
        }

        public Matrix4x4 ViewMatrix => _viewMatrix;
        public Matrix4x4 ProjectionMatrix => _projectionMatrix;

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
                    UpdateProjection(_viewportWidth, _viewportHeight);
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
            _fillShaderProgram = new FillShaderProgram();

            _shader = new Shader(UnifiedSegmentShaders.VertexShader, UnifiedSegmentShaders.FragmentShader);

            // Initialize polygon renderer used for filled overlays (window selection + polygon geometry)
            _worldPolygonRenderer = new WorldPolygonRenderer(_fillShaderProgram);

            // Initialize text renderer when a font provider is available
            if (_textMetrics != null)
                _textRenderer = new TextRenderer(_shaderProgram, _textMetrics);

            // Set baseline GL state
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Disable(EnableCap.CullFace); // lines don't need culling
            GL.ClearColor(0.08f, 0.08f, 0.08f, 1.0f);

            // Enable debug output and log context info
            GLDiag.LogContextInfo();

            _viewport = new Viewport(glWPFControl) { Camera = _camera };

            _camera.SetViewportSize(_viewport.PixelWidth, _viewport.PixelHeight);

            UpdateProjection(_viewport.PixelWidth, _viewport.PixelHeight);

            GLDiag.Check("Initialize end");
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

            // Also include geometry from IDrawableSource objects
            var drawableSources = objects.OfType<IDrawableSource>().ToList();

            // Collect objects that are neither ISegmentSource nor IDrawableSource
            var remainingObjects = objects
                .Where(o => o is not ISegmentSource && o is not IDrawableSource)
                .ToList();

            foreach (var source in drawableSources)
            {
                var highlighted = highlightedSet.Contains((OpenCADObject)source);
                var selected = selectedSet != null && selectedSet.Contains((OpenCADObject)source);
    
                foreach (var drawable in source.GenerateGeometry())
                {
                    if (drawable is null)
                        continue;
                    if (highlighted)
                        highlightedSet.Add((OpenCADObject)drawable);
                    if (selected)
                        selectedSet?.Add((OpenCADObject)drawable);

                    if (drawable is ISegmentSource segmentSource)
                    {
                        segmentSources.Add(segmentSource);
                        continue;
                    }
                    remainingObjects.Add((OpenCADObject)drawable);
                }
            }

            RenderSegments(segmentSources, highlightedSet, selectedSet);
            RenderObjects(remainingObjects, highlightedSet, selectedSet);

            GLDiag.Check("End of Render");
        }

        /// <summary>
        /// Render non-segment objects with highlighting and selection support
        /// </summary>
        private void RenderObjects(List<OpenCADObject> objects,
                                    HashSet<OpenCADObject> highlightedSet,
                                    HashSet<OpenCADObject>? selectedSet)
        {
            foreach (var obj in objects)
            {
                var context = new RenderContext
                {
                    ViewMatrix = _viewMatrix,
                    ProjectionMatrix = _projectionMatrix,
                    IsHighlighted = highlightedSet.Contains(obj),
                    IsSelected = selectedSet != null && selectedSet.Contains(obj),
                };
                RenderObject(obj, context);
            }
        }

        /// <summary>
        /// Render segment sources with highlighting and selection support
        /// </summary>
        private void RenderSegments(List<ISegmentSource> segmentSources,
                                    HashSet<OpenCADObject> highlightedSet,
                                    HashSet<OpenCADObject>? selectedSet)
        {
            float maxSagittaWorld = _viewport.PixelsToWorld(0.5f); // e.g. 0.5px tolerance

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
        /// Route a single OpenCADObject to its dedicated render method
        /// </summary>
        private void RenderObject(OpenCADObject obj, RenderContext context)
        {
            switch (obj)
            {
                case Polygon polygon:
                    RenderPolygon(polygon, context);
                    break;
                case SText text:
                    RenderSText(text, context);
                    break;
            }
        }

        /// <summary>
        /// Render a Polygon: filled interior via WorldPolygonRenderer, edges via SegmentRenderer
        /// </summary>
        private void RenderPolygon(Polygon polygon, RenderContext context)
        {
            var vertices = polygon.Vertices;
            if (vertices.Length < 3)
                return;

            if (!EnsureFullPolygonRenderers())
                return;

            // 1. Fill
            if (_worldPolygonRenderer != null)
            {
                var points = vertices
                    .Select(v => new Vector3((float)v.X, (float)v.Y, (float)v.Z))
                    .ToArray();

                var worldPolygon = new WorldPolygon(points, polygon.FillColor);
                _worldPolygonRenderer.Render(worldPolygon, _camera.ViewMatrix, _camera.ProjectionMatrix);
            }

            // 2. Edges
            var segments = PolygonToSegments(vertices, polygon.ColorVector);
            var lineTypeData = polygon.GetLinetypeGpuData();
            var mode = HighlightModeFromContext(context);

            _segmentRenderer.BeginFrame(_viewport, _viewport.ProjectionMatrix);
            _segmentRenderer.DrawSegments(segments, _viewport, lineTypeData, mode);
            _segmentRenderer.EndFrame();
        }

        private bool EnsureFullPolygonRenderers()
        {
            if (_fillShaderProgram == null || _segmentRenderer == null)
                return false;

            _worldPolygonRenderer ??= new WorldPolygonRenderer(_fillShaderProgram!);

            return _worldPolygonRenderer != null;
        }

        /// <summary>
        /// Render an SText object via the cached TextRenderer
        /// </summary>
        private void RenderSText(SText text, RenderContext context)
        {
            if (!EnsureTextRenderer())
                return;

            _textRenderer?.Render(text, context);
        }

        private bool EnsureTextRenderer()
        {
            if (_textMetrics == null || _shaderProgram == null)
                return false;

            _textRenderer ??= new TextRenderer(_shaderProgram!, _textMetrics);

            return _textRenderer != null;
        }

        /// <summary>
        /// Convert polygon vertices to closed-loop segments for edge rendering
        /// </summary>
        private static IEnumerable<Segment> PolygonToSegments(Point3D[] vertices, Vector4 color)
        {
            int n = vertices.Length;
            float cumDist = 0f;

            for (int i = 0; i < n; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % n];
                var va = new Vector2((float)a.X, (float)a.Y);
                var vb = new Vector2((float)b.X, (float)b.Y);

                float len = Vector2.Distance(va, vb);
                float d0 = cumDist;
                float d1 = cumDist + len;
                cumDist = d1;

                yield return new Segment(va, vb, 0f, 0f, d0, d1, 0, color);
            }
        }

        /// <summary>
        /// Derive HighlightMode from a RenderContext
        /// </summary>
        private static HighlightMode HighlightModeFromContext(RenderContext context)
        {
            return (context.IsHighlighted, context.IsSelected) switch
            {
                (true, true) => HighlightMode.HoverSelected,
                (true, false) => HighlightMode.Hover,
                (false, true) => HighlightMode.Selected,
                _ => HighlightMode.None,
            };
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
            if (_worldPolygonRenderer == null) return;
            
            // Convert OpenCAD.Geometry.Point3D array to System.Numerics.Vector3 array
            var points = vertices.Select(v => new Vector3((float)v.X, (float)v.Y, (float)v.Z)).ToArray();

            var worldPolygon = new WorldPolygon(points, fillColor);
            _worldPolygonRenderer.Render(worldPolygon, _camera.ViewMatrix, _camera.ProjectionMatrix);
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