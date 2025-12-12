using OpenCAD.Geometry;
using OpenCAD;
using System.Numerics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace GraphicsEngine
{
    /// <summary>
    /// Modern OpenGL renderer for Arc geometry using VBOs and shaders.
    /// Tessellates arcs into line segments for rendering.
    /// </summary>
    public class ArcRenderer : IRenderer
    {
        private readonly ShaderProgram _shaderProgram;
        private int _vao; // Vertex Array Object
        private int _vbo; // Vertex Buffer Object
        private readonly bool _smokeTest = Environment.GetEnvironmentVariable("OPENCAD_SMOKE_TEST") == "1";
        private static readonly bool _debugTilt = Environment.GetEnvironmentVariable("OPENCAD_DEBUG_TILT") == "1";

        private Vector2 _viewport = new Vector2(800, 600);
        private const float THIN_LINE_THRESHOLD = 2.5f;
        private const float MIN_LINE_LENGTH = 0.0001f;

        // Tessellation quality settings
        private const int MIN_SEGMENTS = 8;
        private const int MAX_SEGMENTS = 360;
        private const float PIXELS_PER_SEGMENT = 5.0f; // Target pixels per arc segment

        public ArcRenderer(ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            if (_vao == 0 || _vbo == 0)
            {
                // Error: Failed to create VAO/VBO
            }

            // Set up VAO
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            // position (vec3) + per-vertex distance (float)
            int stride = 4 * sizeof(float);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            GLDiag.Check("ArcRenderer ctor end");
        }

        public bool CanRender(OpenCADObject obj)
        {
            return obj is Arc || obj is Circle;
        }

        public void Render(OpenCADObject obj, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            var context = new RenderContext
            {
                ViewMatrix = viewMatrix,
                ProjectionMatrix = projectionMatrix,
                IsHighlighted = false,
                IsSelected = false
            };
            Render(obj, context);
        }

        public void Render(OpenCADObject obj, RenderContext context)
        {
            Arc arc;
            if (obj is not Arc && obj is not Circle) return;
            if (obj is Circle circle)
            {
                arc = new Arc(circle.Center, circle.Radius, 0.0, 2.0 * Math.PI - (1e-12), circle.Document);
            }
            else
            {
                arc = (Arc)obj;
            }

            try
                {
                    // Validate arc parameters
                    if (arc.Center == null || !IsValidPoint(arc.Center))
                    {
                        Debug.WriteLine("[AR] Skipping arc with invalid center point");
                        return;
                    }

                    if (arc.Radius <= 0 || double.IsNaN(arc.Radius) || double.IsInfinity(arc.Radius))
                    {
                        Debug.WriteLine("[AR] Skipping arc with invalid radius");
                        return;
                    }

                    // Get effective properties
                    var effectiveColor = arc.Color;
                    var effectiveLineWeight = arc.LineWeight;
                    var effectiveLineType = arc.LineType;

                    Vector4 color = new Vector4(
                        effectiveColor.R / 255.0f,
                        effectiveColor.G / 255.0f,
                        effectiveColor.B / 255.0f,
                        effectiveColor.A / 255.0f
                    );

                    float lineWidth = effectiveLineWeight.ToOpenGLWidth();
                    int lineTypePattern = GetLineTypePattern(effectiveLineType);

                    // Override for selected objects
                    if (context.IsSelected)
                    {
                        lineTypePattern = 8; // Fine dashed pattern
                        lineWidth = Math.Max(lineWidth, 2.0f);
                    }

                    bool useThinLineRendering = lineWidth <= THIN_LINE_THRESHOLD;

                    // Orthographic detection
                    bool isOrtho = MathF.Abs(context.ProjectionMatrix.M34) < 1e-6f &&
                                  MathF.Abs(context.ProjectionMatrix.M44 - 1f) < 1e-6f;

                    float glowRadius = context.IsHighlighted ? 5.0f : 0.0f;

                    if (isOrtho)
                    {
                        RenderOrthographic(arc, context.ProjectionMatrix, color, lineWidth, lineTypePattern, useThinLineRendering, glowRadius);
                    }
                    else
                    {
                        RenderPerspective(arc, context.ViewMatrix, context.ProjectionMatrix, color, lineWidth, lineTypePattern, useThinLineRendering, glowRadius);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AR] Exception rendering arc: {ex.Message}");
                    Debug.WriteLine($"[AR] Stack trace: {ex.StackTrace}");
                }
        }

        private bool IsValidPoint(Point3D point)
        {
            return !double.IsNaN(point.X) && !double.IsNaN(point.Y) && !double.IsNaN(point.Z) &&
                   !double.IsInfinity(point.X) && !double.IsInfinity(point.Y) && !double.IsInfinity(point.Z);
        }

        private int GetLineTypePattern(LineType lineType)
        {
            return lineType switch
            {
                LineType.Continuous => 0,
                LineType.Dashed => 1,
                LineType.Dotted => 2,
                LineType.DashDot => 3,
                LineType.DashDotDot => 4,
                LineType.Center => 5,
                LineType.Hidden => 6,
                LineType.Phantom => 7,
                LineType.ByLayer => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Calculates the optimal number of segments to tessellate the arc.
        /// </summary>
        private int CalculateSegmentCount(Arc arc, float screenRadius)
        {
            double sweepAngle = arc.GetSweepAngle();
            
            // Estimate based on screen size
            float arcLengthPixels = (float)(sweepAngle * screenRadius);
            int segments = (int)Math.Ceiling(arcLengthPixels / PIXELS_PER_SEGMENT);

            // Clamp to reasonable range
            segments = Math.Max(MIN_SEGMENTS, Math.Min(MAX_SEGMENTS, segments));

            return segments;
        }

        /// <summary>
        /// Tessellates the arc into line segments.
        /// </summary>
        private float[] TessellateArc(Arc arc, int segmentCount)
        {
            double sweepAngle = arc.GetSweepAngle();
            double angleStep = sweepAngle / segmentCount;

            // Create vertices for line strip (world-space)
            float[] vertices = new float[(segmentCount + 1) * 3];
            
            for (int i = 0; i <= segmentCount; i++)
            {
                double angle = arc.StartAngle + i * angleStep;
                int idx = i * 3;
                
                vertices[idx] = (float)(arc.Center.X + arc.Radius * Math.Cos(angle));
                vertices[idx + 1] = (float)(arc.Center.Y + arc.Radius * Math.Sin(angle));
                vertices[idx + 2] = (float)arc.Center.Z;
            }

            return vertices;
        }

        private void RenderOrthographic(Arc arc, Matrix4x4 projectionMatrix, Vector4 color, 
            float lineWidth, int lineTypePattern, bool useThinLineRendering, float glowRadius)
        {
            // Extract ortho parameters from projection matrix
            float sx = projectionMatrix.M11;
            float sy = projectionMatrix.M22;
            float txRow = projectionMatrix.M41;
            float tyRow = projectionMatrix.M42;

            if (MathF.Abs(sx) > 1e-12f && MathF.Abs(sy) > 1e-12f)
            {
                // Get viewport dimensions
                int[] viewport = new int[4];
                GL.GetInteger(GetPName.Viewport, viewport);

                if (viewport[2] <= 0 || viewport[3] <= 0)
                {
                    Debug.WriteLine($"[AR] Invalid viewport dimensions: {viewport[2]}x{viewport[3]}");
                    return;
                }

                _viewport = new Vector2(viewport[2], viewport[3]);

                float halfW = 1.0f / sx;
                float halfH = 1.0f / sy;
                float centerX = -txRow / sx;
                float centerY = -tyRow / sy;

                // Calculate screen-space radius
                float worldRadius = (float)arc.Radius;
                float ndcRadius = worldRadius / halfW;
                float screenRadius = ndcRadius * 0.5f * _viewport.X;

                // Determine segment count based on screen size
                int segmentCount = CalculateSegmentCount(arc, screenRadius);

                // Tessellate arc into world-space vertices
                float[] vertices = TessellateArc(arc, segmentCount);

                // Transform vertices to NDC space (for GPU when mvp=Identity)
                float[] ndcVertices = new float[vertices.Length];
                int vertexCount = ndcVertices.Length / 3;
                for (int i = 0; i < vertices.Length; i += 3)
                {
                    float wx = vertices[i];
                    float wy = vertices[i + 1];

                    ndcVertices[i] = (wx - centerX) / halfW;
                    ndcVertices[i + 1] = (wy - centerY) / halfH;
                    ndcVertices[i + 2] = 0f;
                }

                // Compute per-vertex cumulative distances in world units (object length)
                float[] cumulative = new float[vertexCount];
                cumulative[0] = 0f;
                for (int i = 1; i < vertexCount; i++)
                {
                    int pi = (i - 1) * 3;
                    int ci = i * 3;
                    float dx = vertices[ci + 0] - vertices[pi + 0];
                    float dy = vertices[ci + 1] - vertices[pi + 1];
                    float dz = vertices[ci + 2] - vertices[pi + 2];
                    cumulative[i] = cumulative[i - 1] + MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                }

                float totalWorldLength = cumulative[vertexCount - 1];
                if (totalWorldLength < 1e-12f) totalWorldLength = 1.0f;

                // Build interleaved buffer: position (NDC) + distance (world units)
                float[] interleaved = new float[vertexCount * 4];
                for (int i = 0; i < vertexCount; i++)
                {
                    int vi = i * 3;
                    int ii = i * 4;
                    interleaved[ii + 0] = ndcVertices[vi + 0];
                    interleaved[ii + 1] = ndcVertices[vi + 1];
                    interleaved[ii + 2] = ndcVertices[vi + 2];
                    interleaved[ii + 3] = cumulative[i];
                }

                // Set up shader
                _shaderProgram.Use();
                _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity);
                _shaderProgram.SetVector4("color", color);
                _shaderProgram.SetInt("lineTypePattern", lineTypePattern);
                _shaderProgram.SetVector2("viewport", _viewport);

                // Disable shader-based distance discard for curved arcs (use GL.LineWidth for thickness)
                _shaderProgram.SetFloat("lineWidth", 10000.0f); // <- change: large to bypass shader distance test
                _shaderProgram.SetFloat("glowRadius", 0.0f);    // shader-based glow disabled for arcs

                // Pass per-object linetype scale (applies to world-unit pattern lengths)
                _shaderProgram.SetFloat("lineTypeScale", (float)arc.LinetypeScale);

                // Pass total world length as uniform (optional for shader logic)
                _shaderProgram.SetFloat("lineLength", totalWorldLength);

                // Set line start/end in screen space (thickness calculations use these)
                Vector2 arcStart = new Vector2(
                    (ndcVertices[0] * 0.5f + 0.5f) * _viewport.X,
                    (ndcVertices[1] * 0.5f + 0.5f) * _viewport.Y
                );
                Vector2 arcEnd = new Vector2(
                    (ndcVertices[ndcVertices.Length - 3] * 0.5f + 0.5f) * _viewport.X,
                    (ndcVertices[ndcVertices.Length - 2] * 0.5f + 0.5f) * _viewport.Y
                );
                
                _shaderProgram.SetVector2("lineStart", arcStart);
                _shaderProgram.SetVector2("lineEnd", arcEnd);

                // Bind vertex data (interleaved: NDC pos + world-distance)
                GL.BindVertexArray(_vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.DynamicDraw);

                // Render glow effect if highlighted
                if (glowRadius > 0.0f)
                {
                    // First pass: Render glow halo with increased line width and transparency
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    
                    // Semi-transparent glow color
                    Vector4 glowColor = new Vector4(color.X, color.Y, color.Z, 0.45f * color.W);
                    _shaderProgram.SetVector4("color", glowColor);
                    _shaderProgram.SetFloat("glowRadius", 0.0f); // Disable shader-based glow for arcs
                    
                    float glowLineWidth = lineWidth + glowRadius * 2.0f;
                    GL.LineWidth(glowLineWidth);
                    GL.DrawArrays(PrimitiveType.LineStrip, 0, vertexCount);
                    
                    // Second pass: Render solid line on top
                    _shaderProgram.SetVector4("color", color);
                    GL.LineWidth(lineWidth);
                    GL.DrawArrays(PrimitiveType.LineStrip, 0, vertexCount);
                }
                else
                {
                    // No glow: single pass rendering
                    _shaderProgram.SetFloat("glowRadius", 0.0f);
                    GL.LineWidth(lineWidth);
                    GL.DrawArrays(PrimitiveType.LineStrip, 0, vertexCount);
                }

                GL.BindVertexArray(0);
                GLDiag.Check("ArcRenderer draw end (ortho)");

                if (_debugTilt)
                {
                    Debug.WriteLine($"[AR][ORTHO] segments={segmentCount} screenRadius={screenRadius:F1}px worldLen={totalWorldLength:F3} color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} glowRadius={glowRadius:F2}");
                }
            }
        }

        private void RenderPerspective(Arc arc, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, 
            Vector4 color, float lineWidth, int lineTypePattern, bool useThinLineRendering, float glowRadius)
        {
            // Get viewport dimensions
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);

            if (viewport[2] <= 0 || viewport[3] <= 0)
            {
                Debug.WriteLine($"[AR] Invalid viewport dimensions: {viewport[2]}x{viewport[3]}");
                return;
            }

            _viewport = new Vector2(viewport[2], viewport[3]);

            // Calculate MVP matrix
            var model = Matrix4x4.Identity;
            var mvpRow = Matrix4x4.Multiply(Matrix4x4.Multiply(model, viewMatrix), projectionMatrix);

            // Transform center to clip space to estimate screen radius
            Vector4 clipCenter = Vector4.Transform(new Vector4((float)arc.Center.X, (float)arc.Center.Y, (float)arc.Center.Z, 1.0f), mvpRow);
            
            if (MathF.Abs(clipCenter.W) < 0.0001f)
            {
                Debug.WriteLine("[AR] Invalid clip space coordinates");
                return;
            }

            Vector2 ndcCenter = new Vector2(clipCenter.X / clipCenter.W, clipCenter.Y / clipCenter.W);
            
            // Estimate screen-space radius (approximate for perspective)
            Vector4 clipEdge = Vector4.Transform(new Vector4((float)(arc.Center.X + arc.Radius), (float)arc.Center.Y, (float)arc.Center.Z, 1.0f), mvpRow);
            Vector2 ndcEdge = new Vector2(clipEdge.X / clipEdge.W, clipEdge.Y / clipEdge.W);
            float ndcRadius = Vector2.Distance(ndcCenter, ndcEdge);
            float screenRadius = ndcRadius * 0.5f * _viewport.X;

            // Determine segment count
            int segmentCount = CalculateSegmentCount(arc, screenRadius);

            // Tessellate arc (world-space vertices)
            float[] vertices = TessellateArc(arc, segmentCount);

            // Compute per-vertex cumulative distances in world units
            int vertexCount = vertices.Length / 3;
            float[] cumulative = new float[vertexCount];
            cumulative[0] = 0f;
            for (int i = 1; i < vertexCount; i++)
            {
                int pi = (i - 1) * 3;
                int ci = i * 3;
                float dx = vertices[ci + 0] - vertices[pi + 0];
                float dy = vertices[ci + 1] - vertices[pi + 1];
                float dz = vertices[ci + 2] - vertices[pi + 2];
                cumulative[i] = cumulative[i - 1] + MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            float totalWorldLength = cumulative[vertexCount - 1];
            if (totalWorldLength < 1e-12f) totalWorldLength = 1.0f;

            // Build interleaved buffer: position (world) + distance (world units)
            float[] interleaved = new float[vertexCount * 4];
            for (int i = 0; i < vertexCount; i++)
            {
                int vi = i * 3;
                int ii = i * 4;
                interleaved[ii + 0] = vertices[vi + 0];
                interleaved[ii + 1] = vertices[vi + 1];
                interleaved[ii + 2] = vertices[vi + 2];
                interleaved[ii + 3] = cumulative[i];
            }

            // Set up shader
            _shaderProgram.Use();
            _shaderProgram.SetMatrix4("mvp", mvpRow);
            _shaderProgram.SetVector4("color", color);
            _shaderProgram.SetInt("lineTypePattern", lineTypePattern);
            _shaderProgram.SetVector2("viewport", _viewport);

            // Disable shader-based distance discard for curved arcs (use GL.LineWidth for thickness)
            _shaderProgram.SetFloat("lineWidth", 10000.0f); // <- change: large to bypass shader distance test
            _shaderProgram.SetFloat("glowRadius", glowRadius); // keep glow handling as appropriate

            // GL.LineWidth(lineWidth) still controls actual rasterized width below

            // Pass per-object linetype scale (applies to world-unit pattern lengths)
            _shaderProgram.SetFloat("lineTypeScale", (float)arc.LinetypeScale);

            // Pass total world length as uniform (optional)
            _shaderProgram.SetFloat("lineLength", totalWorldLength);

            // Compute screen space start/end for thickness calculations
            Vector4 clipStart = Vector4.Transform(new Vector4(interleaved[0], interleaved[1], interleaved[2], 1.0f), mvpRow);
            Vector4 clipEnd = Vector4.Transform(new Vector4(interleaved[(vertexCount - 1) * 4 + 0], interleaved[(vertexCount - 1) * 4 + 1], interleaved[(vertexCount - 1) * 4 + 2], 1.0f), mvpRow);

            Vector2 ndcStart = new Vector2(clipStart.X / clipStart.W, clipStart.Y / clipStart.W);
            Vector2 ndcEnd = new Vector2(clipEnd.X / clipEnd.W, clipEnd.Y / clipEnd.W);

            Vector2 screenStart = new Vector2(
                (ndcStart.X * 0.5f + 0.5f) * _viewport.X,
                (ndcStart.Y * 0.5f + 0.5f) * _viewport.Y
            );
            Vector2 screenEnd = new Vector2(
                (ndcEnd.X * 0.5f + 0.5f) * _viewport.X,
                (ndcEnd.Y * 0.5f + 0.5f) * _viewport.Y
            );

            _shaderProgram.SetVector2("lineStart", screenStart);
            _shaderProgram.SetVector2("lineEnd", screenEnd);

            // Bind and upload interleaved data
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.DynamicDraw);

            if (useThinLineRendering)
            {
                GL.LineWidth(lineWidth);
            }

            GL.DrawArrays(PrimitiveType.LineStrip, 0, vertexCount);

            GL.BindVertexArray(0);
            GLDiag.Check("ArcRenderer draw end (perspective)");

            if (_debugTilt)
            {
                Debug.WriteLine($"[AR][PERSP] segments={segmentCount} worldLen={totalWorldLength:F3} color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2}");
            }
        }

        private bool IsValidFloat(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}