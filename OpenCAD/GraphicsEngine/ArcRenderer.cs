using OpenCAD.Geometry;
using OpenCAD;
using System.Numerics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using GraphicsEngine.Interfaces;
using OpenCAD.Styles.LineTypes;

namespace GraphicsEngine
{
    /// <summary>
    /// Modern OpenGL renderer for Arc geometry using VBOs and shaders.
    /// Tessellates arcs into line segments for rendering.
    /// Uses simple line rendering for thin lines and quad rendering for thick lines.
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
        private const float MIN_ARC_SEGMENT_LENGTH = 0.0001f;

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

            // Set up VAO once in constructor - this state is preserved
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            // Interleaved layout: vec3 position + float distance (in world units)
            int stride = 4 * sizeof(float);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            // location 1 = per-vertex cumulative distance (in world units)
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
                var effectiveLineType = arc.LineTypeID;

                // Convert System.Drawing.Color to Vector4 (normalized RGBA with alpha)
                Vector4 color = new Vector4(
                    effectiveColor.R / 255.0f,
                    effectiveColor.G / 255.0f,
                    effectiveColor.B / 255.0f,
                    effectiveColor.A / 255.0f
                );

                // Convert LineWeight to OpenGL width
                float lineWidth = effectiveLineWeight.ToOpenGLWidth();

                // Convert LineType to pattern index for shader
                int lineTypePattern = 0;// GetLineTypePattern(effectiveLineType);

                // Override for selected objects
                if (context.IsSelected)
                {
                    // Selected objects: use original color with fine dashed pattern (pattern 8)
                    lineTypePattern = 8;
                    lineWidth = Math.Max(lineWidth, 2.0f); // Make selected lines at least 2px thick
                }

                // Determine rendering method based on line width
                bool useThinLineRendering = lineWidth <= THIN_LINE_THRESHOLD;

                // Orthographic detection: no perspective divide
                bool isOrtho = MathF.Abs(context.ProjectionMatrix.M34) < 1e-6f &&
                              MathF.Abs(context.ProjectionMatrix.M44 - 1f) < 1e-6f;

                float glowRadius = context.IsHighlighted ? 5.0f : 0.0f;

                if (isOrtho)
                {
                    RenderOrthographic(arc, context.ViewMatrix, context.ProjectionMatrix, color, lineWidth, lineTypePattern, useThinLineRendering, glowRadius);
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

        /// <summary>
        /// Validates that a point contains valid coordinate values
        /// </summary>
        private bool IsValidPoint(Point3D point)
        {
            return !double.IsNaN(point.X) && !double.IsNaN(point.Y) && !double.IsNaN(point.Z) &&
                   !double.IsInfinity(point.X) && !double.IsInfinity(point.Y) && !double.IsInfinity(point.Z);
        }

        /// <summary>
        /// Converts a LineType enum to a shader pattern index
        /// </summary>
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
        /// Tessellates the arc into line segments in world space.
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

        /// <summary>
        /// Creates quad geometry (triangle strip) for arc segments with perpendicular width.
        /// Unlike straight lines, arcs require calculating tangent at each vertex.
        /// </summary>
        /// <param name="ndcVertices">Arc vertices in NDC space (centerline)</param>
        /// <param name="vertexCount">Number of vertices in the arc</param>
        /// <param name="halfWidthNDC">Half-width of the quad in NDC space</param>
        /// <param name="cumulativeDistances">Cumulative distance for each centerline vertex (for line patterns)</param>
        /// <param name="quadDistances">Output: Per-vertex cumulative distances for quad vertices</param>
        /// <returns>Interleaved triangle vertices, or null if arc is degenerate</returns>
        private float[]? CreateArcQuads(float[] ndcVertices, int vertexCount, float halfWidthNDC,
            float[] cumulativeDistances, out float[] quadDistances)
        {
            quadDistances = Array.Empty<float>();

            if (vertexCount < 2)
            {
                Debug.WriteLine("[AR] Arc too short for quad generation");
                return null;
            }

            // Pre-calculate tangent directions for each vertex
            Vector2[] tangents = new Vector2[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                Vector2 tangent;

                if (i == 0)
                {
                    // First vertex: use forward difference
                    Vector2 curr = new Vector2(ndcVertices[i * 3], ndcVertices[i * 3 + 1]);
                    Vector2 next = new Vector2(ndcVertices[(i + 1) * 3], ndcVertices[(i + 1) * 3 + 1]);
                    Vector2 diff = next - curr;
                    float len = diff.Length();
                    tangent = len > MIN_ARC_SEGMENT_LENGTH ? diff / len : Vector2.UnitX;
                }
                else if (i == vertexCount - 1)
                {
                    // Last vertex: use backward difference
                    Vector2 curr = new Vector2(ndcVertices[i * 3], ndcVertices[i * 3 + 1]);
                    Vector2 prev = new Vector2(ndcVertices[(i - 1) * 3], ndcVertices[(i - 1) * 3 + 1]);
                    Vector2 diff = curr - prev;
                    float len = diff.Length();
                    tangent = len > MIN_ARC_SEGMENT_LENGTH ? diff / len : Vector2.UnitX;
                }
                else
                {
                    // Middle vertices: average of forward and backward tangents (smoother)
                    Vector2 curr = new Vector2(ndcVertices[i * 3], ndcVertices[i * 3 + 1]);
                    Vector2 prev = new Vector2(ndcVertices[(i - 1) * 3], ndcVertices[(i - 1) * 3 + 1]);
                    Vector2 next = new Vector2(ndcVertices[(i + 1) * 3], ndcVertices[(i + 1) * 3 + 1]);

                    Vector2 tangent1 = curr - prev;
                    Vector2 tangent2 = next - curr;

                    // Average and normalize
                    Vector2 avgTangent = tangent1 + tangent2;
                    float len = avgTangent.Length();
                    tangent = len > MIN_ARC_SEGMENT_LENGTH ? avgTangent / len : Vector2.UnitX;
                }

                tangents[i] = tangent;
            }

            // Build triangle strip: for N centerline vertices, create 2N quad vertices
            // Triangle strip order: outer0, inner0, outer1, inner1, outer2, inner2, ...
            List<float> quadVertices = new List<float>((vertexCount * 2) * 3);
            List<float> distancesList = new List<float>(vertexCount * 2);

            for (int i = 0; i < vertexCount; i++)
            {
                Vector2 center = new Vector2(ndcVertices[i * 3], ndcVertices[i * 3 + 1]);
                float z = ndcVertices[i * 3 + 2];

                // Perpendicular direction (90° rotation of tangent)
                Vector2 perpDir = new Vector2(-tangents[i].Y, tangents[i].X);
                Vector2 offset = perpDir * halfWidthNDC;

                // Outer vertex (+ offset)
                Vector2 outer = center + offset;
                quadVertices.Add(outer.X);
                quadVertices.Add(outer.Y);
                quadVertices.Add(z);

                // Inner vertex (- offset)
                Vector2 inner = center - offset;
                quadVertices.Add(inner.X);
                quadVertices.Add(inner.Y);
                quadVertices.Add(z);

                // Both vertices at this position have the same cumulative distance
                float dist = cumulativeDistances[i];
                distancesList.Add(dist); // Outer vertex
                distancesList.Add(dist); // Inner vertex
            }

            quadDistances = distancesList.ToArray();
            return quadVertices.ToArray();
        }

        private void RenderOrthographic(Arc arc, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Vector4 color,
            float lineWidth, int lineTypePattern, bool useThinLineRendering, float glowRadius)
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

            // Use proper view+projection matrix multiplication (like LineRenderer)
            Matrix4x4 vp = viewMatrix * projectionMatrix;

            // Estimate screen-space radius by transforming arc center and edge point
            Vector4 worldCenter = new Vector4((float)arc.Center.X, (float)arc.Center.Y, (float)arc.Center.Z, 1f);
            Vector4 worldEdge = new Vector4((float)(arc.Center.X + arc.Radius), (float)arc.Center.Y, (float)arc.Center.Z, 1f);

            Vector4 clipCenter = Vector4.Transform(worldCenter, vp);
            Vector4 clipEdge = Vector4.Transform(worldEdge, vp);

            // Validate clip coordinates
            if (MathF.Abs(clipCenter.W) < 0.0001f || MathF.Abs(clipEdge.W) < 0.0001f)
            {
                Debug.WriteLine("[AR] Invalid clip space W coordinate");
                return;
            }

            // Perspective divide to NDC
            Vector2 ndcCenter = new Vector2(clipCenter.X / clipCenter.W, clipCenter.Y / clipCenter.W);
            Vector2 ndcEdge = new Vector2(clipEdge.X / clipEdge.W, clipEdge.Y / clipEdge.W);

            // Calculate screen-space radius
            float ndcRadius = Vector2.Distance(ndcCenter, ndcEdge);
            float screenRadius = ndcRadius * 0.5f * _viewport.X;

            // Determine segment count based on screen size
            int segmentCount = CalculateSegmentCount(arc, screenRadius);

            // Tessellate arc into world-space vertices
            float[] vertices = TessellateArc(arc, segmentCount);
            int vertexCount = vertices.Length / 3;

            // Transform all vertices from world space to NDC space
            float[] ndcVertices = new float[vertices.Length];
            for (int i = 0; i < vertexCount; i++)
            {
                int vi = i * 3;
                Vector4 worldPos = new Vector4(vertices[vi], vertices[vi + 1], vertices[vi + 2], 1f);
                Vector4 clipPos = Vector4.Transform(worldPos, vp);

                if (MathF.Abs(clipPos.W) < 0.0001f)
                {
                    Debug.WriteLine($"[AR] Invalid clip W at vertex {i}");
                    continue;
                }

                ndcVertices[vi] = clipPos.X / clipPos.W;
                ndcVertices[vi + 1] = clipPos.Y / clipPos.W;
                ndcVertices[vi + 2] = clipPos.Z / clipPos.W;
            }

            // Validate NDC coordinates
            if (!IsValidFloat(ndcVertices[0]) || !IsValidFloat(ndcVertices[1]))
            {
                Debug.WriteLine("[AR] Invalid NDC coordinates");
                return;
            }

            // Compute per-vertex cumulative distances in world units
            float[] cumulative = new float[vertexCount];
            cumulative[0] = 0f;
            for (int i = 1; i < vertexCount; i++)
            {
                int pi = (i - 1) * 3;
                int ci = i * 3;
                float dx = vertices[ci] - vertices[pi];
                float dy = vertices[ci + 1] - vertices[pi + 1];
                float dz = vertices[ci + 2] - vertices[pi + 2];
                cumulative[i] = cumulative[i - 1] + MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            float totalWorldLength = cumulative[vertexCount - 1];
            if (totalWorldLength < 1e-12f) totalWorldLength = 1.0f;

            // Convert NDC to screen space for shader uniforms
            Vector2 screenStart = new Vector2(
                (ndcVertices[0] * 0.5f + 0.5f) * _viewport.X,
                (ndcVertices[1] * 0.5f + 0.5f) * _viewport.Y
            );
            Vector2 screenEnd = new Vector2(
                (ndcVertices[(vertexCount - 1) * 3] * 0.5f + 0.5f) * _viewport.X,
                (ndcVertices[(vertexCount - 1) * 3 + 1] * 0.5f + 0.5f) * _viewport.Y
            );

            // Set up shader uniforms
            _shaderProgram.Use();
            _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity); // Vertices already in NDC
            _shaderProgram.SetVector4("color", color);
            _shaderProgram.SetInt("lineTypePattern", lineTypePattern);
            _shaderProgram.SetVector2("viewport", _viewport);
            _shaderProgram.SetVector2("lineStart", screenStart);
            _shaderProgram.SetVector2("lineEnd", screenEnd);

            // CRITICAL FIX: Bypass shader distance checks for curved arcs
            // The shader's perpendicular distance logic is designed for straight lines
            // For arcs: hardware rasterizer (GL.LineWidth) or quad geometry handles thickness
            _shaderProgram.SetFloat("lineWidth", 10000.0f);
            _shaderProgram.SetFloat("glowRadius", 0.0f);

            _shaderProgram.SetFloat("lineTypeScale", (float)arc.LinetypeScale);
            _shaderProgram.SetFloat("lineLength", totalWorldLength);

            // Bind VAO (vertex attributes already configured in constructor)
            GL.BindVertexArray(_vao);

            // Implement thin vs thick rendering with quad support
            if (glowRadius > 0.0f)
            {
                // GLOW PATH: Force quad rendering to cover glow area
                useThinLineRendering = false;

                // Expand the quad to cover the glow radius
                float totalWidth = lineWidth + glowRadius * 2.0f;
                float halfWidthNDC = (totalWidth * 0.5f) / (_viewport.X * 0.5f);

                // For glow, use REAL lineWidth so shader can calculate distance-based alpha falloff
                _shaderProgram.SetFloat("lineWidth", lineWidth);  // ✅ REAL VALUE for glow fadeout
                _shaderProgram.SetFloat("glowRadius", glowRadius);

                float[]? quadVerts = CreateArcQuads(ndcVertices, vertexCount, halfWidthNDC, cumulative, out float[] quadDistances);

                if (quadVerts != null)
                {
                    // Build interleaved buffer: position (NDC) + distance (world units)
                    int quadVertCount = quadVerts.Length / 3;
                    float[] interleaved = new float[quadVertCount * 4];
                    for (int i = 0; i < quadVertCount; i++)
                    {
                        int vi = i * 3;
                        int ii = i * 4;
                        interleaved[ii] = quadVerts[vi];
                        interleaved[ii + 1] = quadVerts[vi + 1];
                        interleaved[ii + 2] = quadVerts[vi + 2];
                        interleaved[ii + 3] = quadDistances[i];
                    }

                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.DynamicDraw);

                    // Enable blending for glow transparency
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                    GL.DrawArrays(PrimitiveType.TriangleStrip, 0, quadVertCount);

                    if (_debugTilt)
                    {
                        Debug.WriteLine($"[AR][ORTHO-GLOW] segments={segmentCount} screenRadius={screenRadius:F1}px worldLen={totalWorldLength:F3} color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} glowRadius={glowRadius:F2}");
                    }
                }
            }
            else
            {
                // NO GLOW: Bypass shader distance checks for curved arcs
                _shaderProgram.SetFloat("lineWidth", 10000.0f);
                _shaderProgram.SetFloat("glowRadius", 0.0f);

                if (useThinLineRendering)
                {
                    // THIN LINE PATH: Simple line rendering with hardware rasterizer
                    float[] interleaved = new float[vertexCount * 4];
                    for (int i = 0; i < vertexCount; i++)
                    {
                        int vi = i * 3;
                        int ii = i * 4;
                        interleaved[ii] = ndcVertices[vi];
                        interleaved[ii + 1] = ndcVertices[vi + 1];
                        interleaved[ii + 2] = ndcVertices[vi + 2];
                        interleaved[ii + 3] = cumulative[i];
                    }

                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.DynamicDraw);

                    GL.LineWidth(lineWidth);
                    GL.DrawArrays(PrimitiveType.LineStrip, 0, vertexCount);

                    if (_debugTilt)
                    {
                        Debug.WriteLine($"[AR][ORTHO-THIN] segments={segmentCount} screenRadius={screenRadius:F1}px worldLen={totalWorldLength:F3} color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} pattern={lineTypePattern}");
                    }
                }
                else
                {
                    // THICK LINE PATH: Quad rendering with geometry-based thickness
                    float halfWidthNDC = (lineWidth * 0.5f) / (_viewport.X * 0.5f);

                    float[]? quadVerts = CreateArcQuads(ndcVertices, vertexCount, halfWidthNDC, cumulative, out float[] quadDistances);

                    if (quadVerts != null)
                    {
                        // Build interleaved buffer: position (NDC) + distance (world units)
                        int quadVertCount = quadVerts.Length / 3;
                        float[] interleaved = new float[quadVertCount * 4];
                        for (int i = 0; i < quadVertCount; i++)
                        {
                            int vi = i * 3;
                            int ii = i * 4;
                            interleaved[ii] = quadVerts[vi];
                            interleaved[ii + 1] = quadVerts[vi + 1];
                            interleaved[ii + 2] = quadVerts[vi + 2];
                            interleaved[ii + 3] = quadDistances[i];
                        }

                        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                        GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.DynamicDraw);

                        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, quadVertCount);

                        if (_debugTilt)
                        {
                            Debug.WriteLine($"[AR][ORTHO-QUAD] segments={segmentCount} screenRadius={screenRadius:F1}px worldLen={totalWorldLength:F3} color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} pattern={lineTypePattern}");
                        }
                    }
                    else
                    {
                        // Fallback to simple line rendering if quad creation failed
                        Debug.WriteLine("[AR] Falling back to simple line rendering for degenerate thick arc");
                        float[] interleavedFallback = new float[vertexCount * 4];
                        for (int i = 0; i < vertexCount; i++)
                        {
                            int vi = i * 3;
                            int ii = i * 4;
                            interleavedFallback[ii] = ndcVertices[vi];
                            interleavedFallback[ii + 1] = ndcVertices[vi + 1];
                            interleavedFallback[ii + 2] = ndcVertices[vi + 2];
                            interleavedFallback[ii + 3] = cumulative[i];
                        }

                        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                        GL.BufferData(BufferTarget.ArrayBuffer, interleavedFallback.Length * sizeof(float), interleavedFallback, BufferUsageHint.DynamicDraw);

                        GL.LineWidth(lineWidth);
                        GL.DrawArrays(PrimitiveType.LineStrip, 0, vertexCount);
                    }
                }
            }

            GL.BindVertexArray(0);
            GLDiag.Check("ArcRenderer draw end (ortho)");
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

            // Calculate MVP matrix (like LineRenderer)
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
                float dx = vertices[ci] - vertices[pi];
                float dy = vertices[ci + 1] - vertices[pi + 1];
                float dz = vertices[ci + 2] - vertices[pi + 2];
                cumulative[i] = cumulative[i - 1] + MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            float totalWorldLength = cumulative[vertexCount - 1];
            if (totalWorldLength < 1e-12f) totalWorldLength = 1.0f;

            // Compute screen space start/end for shader uniforms
            Vector4 clipStart = Vector4.Transform(new Vector4(vertices[0], vertices[1], vertices[2], 1.0f), mvpRow);
            Vector4 clipEnd = Vector4.Transform(new Vector4(vertices[(vertexCount - 1) * 3], vertices[(vertexCount - 1) * 3 + 1], vertices[(vertexCount - 1) * 3 + 2], 1.0f), mvpRow);

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

            // Set up shader
            _shaderProgram.Use();
            _shaderProgram.SetVector4("color", color);
            _shaderProgram.SetInt("lineTypePattern", lineTypePattern);
            _shaderProgram.SetVector2("viewport", _viewport);
            _shaderProgram.SetVector2("lineStart", screenStart);
            _shaderProgram.SetVector2("lineEnd", screenEnd);

            // CRITICAL FIX: Bypass shader distance checks for curved arcs
            _shaderProgram.SetFloat("lineWidth", 10000.0f);
            _shaderProgram.SetFloat("glowRadius", 0.0f);

            _shaderProgram.SetFloat("lineTypeScale", (float)arc.LinetypeScale);

            // Bind VAO (vertex attributes already configured in constructor)
            GL.BindVertexArray(_vao);

            // Implement thin vs thick rendering
            if (glowRadius > 0.0f)
            {
                // GLOW PATH: Force quad rendering
                useThinLineRendering = false;

                // Transform to NDC on CPU for quad generation
                float[] ndcVertices = new float[vertices.Length];
                for (int i = 0; i < vertexCount; i++)
                {
                    int vi = i * 3;
                    Vector4 worldPos = new Vector4(vertices[vi], vertices[vi + 1], vertices[vi + 2], 1f);
                    Vector4 clipPos = Vector4.Transform(worldPos, mvpRow);

                    if (MathF.Abs(clipPos.W) < 0.0001f)
                    {
                        Debug.WriteLine($"[AR] Invalid clip W at vertex {i} in perspective");
                        continue;
                    }

                    ndcVertices[vi] = clipPos.X / clipPos.W;
                    ndcVertices[vi + 1] = clipPos.Y / clipPos.W;
                    ndcVertices[vi + 2] = clipPos.Z / clipPos.W;
                }

                float totalWidth = lineWidth + glowRadius * 2.0f;
                float halfWidthNDC = (totalWidth * 0.5f) / (_viewport.X * 0.5f);

                // For glow, use REAL lineWidth so shader can calculate distance-based alpha falloff
                _shaderProgram.SetFloat("lineWidth", lineWidth);  // ✅ REAL VALUE for glow fadeout
                _shaderProgram.SetFloat("glowRadius", glowRadius);

                float[]? quadVerts = CreateArcQuads(ndcVertices, vertexCount, halfWidthNDC, cumulative, out float[] quadDistances);

                if (quadVerts != null)
                {
                    int quadVertCount = quadVerts.Length / 3;
                    float[] interleaved = new float[quadVertCount * 4];
                    for (int i = 0; i < quadVertCount; i++)
                    {
                        int vi = i * 3;
                        int ii = i * 4;
                        interleaved[ii] = quadVerts[vi];
                        interleaved[ii + 1] = quadVerts[vi + 1];
                        interleaved[ii + 2] = quadVerts[vi + 2];
                        interleaved[ii + 3] = quadDistances[i];
                    }

                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.DynamicDraw);

                    _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity); // Already in NDC

                    // Enable blending for glow transparency
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                    GL.DrawArrays(PrimitiveType.TriangleStrip, 0, quadVertCount);

                    if (_debugTilt)
                    {
                        Debug.WriteLine($"[AR][PERSP-GLOW] segments={segmentCount} worldLen={totalWorldLength:F3} color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} glowRadius={glowRadius:F2}");
                    }
                }
            }
            else
            {
                // NO GLOW: Bypass shader distance checks
                _shaderProgram.SetFloat("lineWidth", 10000.0f);
                _shaderProgram.SetFloat("glowRadius", 0.0f);

                // THIN LINE PATH: Upload world vertices, let GPU transform
                if (useThinLineRendering)
                {
                    float[] interleaved = new float[vertexCount * 4];
                    for (int i = 0; i < vertexCount; i++)
                    {
                        int vi = i * 3;
                        int ii = i * 4;
                        interleaved[ii] = vertices[vi];
                        interleaved[ii + 1] = vertices[vi + 1];
                        interleaved[ii + 2] = vertices[vi + 2];
                        interleaved[ii + 3] = cumulative[i];
                    }

                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.DynamicDraw);

                    _shaderProgram.SetMatrix4("mvp", mvpRow); // GPU transforms world → clip → NDC

                    GL.LineWidth(lineWidth);
                    GL.DrawArrays(PrimitiveType.LineStrip, 0, vertexCount);

                    if (_debugTilt)
                    {
                        Debug.WriteLine($"[AR][PERSP-THIN] segments={segmentCount} worldLen={totalWorldLength:F3} color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} pattern={lineTypePattern}");
                    }
                }
                else
                {
                    // THICK LINE PATH: Transform to NDC on CPU, generate quads
                    float[] ndcVertices = new float[vertices.Length];
                    for (int i = 0; i < vertexCount; i++)
                    {
                        int vi = i * 3;
                        Vector4 worldPos = new Vector4(vertices[vi], vertices[vi + 1], vertices[vi + 2], 1f);
                        Vector4 clipPos = Vector4.Transform(worldPos, mvpRow);

                        if (MathF.Abs(clipPos.W) < 0.0001f)
                        {
                            Debug.WriteLine($"[AR] Invalid clip W at vertex {i} in perspective");
                            continue;
                        }

                        ndcVertices[vi] = clipPos.X / clipPos.W;
                        ndcVertices[vi + 1] = clipPos.Y / clipPos.W;
                        ndcVertices[vi + 2] = clipPos.Z / clipPos.W;
                    }

                    float halfWidthNDC = (lineWidth * 0.5f) / (_viewport.X * 0.5f);
                    float[]? quadVerts = CreateArcQuads(ndcVertices, vertexCount, halfWidthNDC, cumulative, out float[] quadDistances);

                    if (quadVerts != null)
                    {
                        // Build interleaved buffer: position (NDC) + distance (world units)
                        int quadVertCount = quadVerts.Length / 3;
                        float[] interleaved = new float[quadVertCount * 4];
                        for (int i = 0; i < quadVertCount; i++)
                        {
                            int vi = i * 3;
                            int ii = i * 4;
                            interleaved[ii] = quadVerts[vi];
                            interleaved[ii + 1] = quadVerts[vi + 1];
                            interleaved[ii + 2] = quadVerts[vi + 2];
                            interleaved[ii + 3] = quadDistances[i];
                        }

                        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                        GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.DynamicDraw);

                        _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity); // Already in NDC

                        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, quadVertCount);

                        if (_debugTilt)
                        {
                            Debug.WriteLine($"[AR][PERSP-QUAD] segments={segmentCount} worldLen={totalWorldLength:F3} color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} pattern={lineTypePattern}");
                        }
                    }
                    else
                    {
                        // Fallback to simple line rendering if quad creation failed
                        Debug.WriteLine("[AR] Falling back to simple line rendering for degenerate thick arc in perspective");
                        float[] interleavedFallback = new float[vertexCount * 4];
                        for (int i = 0; i < vertexCount; i++)
                        {
                            int vi = i * 3;
                            int ii = i * 4;
                            interleavedFallback[ii] = vertices[vi];
                            interleavedFallback[ii + 1] = vertices[vi + 1];
                            interleavedFallback[ii + 2] = vertices[vi + 2];
                            interleavedFallback[ii + 3] = cumulative[i];
                        }

                        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                        GL.BufferData(BufferTarget.ArrayBuffer, interleavedFallback.Length * sizeof(float), interleavedFallback, BufferUsageHint.DynamicDraw);

                        _shaderProgram.SetMatrix4("mvp", mvpRow);

                        GL.LineWidth(lineWidth);
                        GL.DrawArrays(PrimitiveType.LineStrip, 0, vertexCount);
                    }
                }
            }

            GL.BindVertexArray(0);
            GLDiag.Check("ArcRenderer draw end (perspective)");
        }

        /// <summary>
        /// Validates that a float value is not NaN or Infinity
        /// </summary>
        private bool IsValidFloat(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}