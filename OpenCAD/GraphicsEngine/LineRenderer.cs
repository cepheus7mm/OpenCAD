using OpenCAD.Geometry;
using OpenCAD;
using System.Numerics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace GraphicsEngine
{
    /// <summary>
    /// Modern OpenGL renderer for Line geometry using VBOs and shaders
    /// Uses simple line rendering for thin lines and quad rendering for thick lines
    /// </summary>
    public class LineRenderer : IRenderer
    {
        private readonly ShaderProgram _shaderProgram;
        private int _vao; // Vertex Array Object
        private int _vbo; // Vertex Buffer Object
        private readonly bool _smokeTest = Environment.GetEnvironmentVariable("OPENCAD_SMOKE_TEST") == "1";
        private static readonly bool _debugTilt = Environment.GetEnvironmentVariable("OPENCAD_DEBUG_TILT") == "1";

        // Viewport size for screen-space calculations
        private Vector2 _viewport = new Vector2(800, 600);

        // Threshold for switching between simple lines and quads (in pixels)
        private const float THIN_LINE_THRESHOLD = 2.5f;

        // Minimum line length to avoid division by zero (in NDC space)
        private const float MIN_LINE_LENGTH = 0.0001f;

        public LineRenderer(ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            if (_vao == 0 || _vbo == 0)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Failed to create VAO/VBO (vao={_vao}, vbo={_vbo}). Is a GL context current?");
            }

            // Set up VAO once in constructor - this state is preserved
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            GLDiag.Check("LineRenderer ctor end");
            System.Diagnostics.Debug.WriteLine($"LineRenderer initialized with VAO={_vao} and VBO={_vbo}");
        }

        public bool CanRender(OpenCADObject obj)
        {
            return obj is Line;
        }

        public void Render(OpenCADObject obj, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            // Use default render context (not highlighted or selected)
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
            if (obj is not Line line) return;

            try
            {
                var start = line.Start;
                var end = line.End;

                // Validate that points are not null and contain valid values
                if (start == null || end == null)
                {
                    Debug.WriteLine("[LR] Skipping line with null points");
                    return;
                }

                // Check for NaN or infinity in coordinates
                if (!IsValidPoint(start) || !IsValidPoint(end))
                {
                    Debug.WriteLine($"[LR] Skipping line with invalid coordinates: Start=({start.X},{start.Y},{start.Z}), End=({end.X},{end.Y},{end.Z})");
                    return;
                }

                // Get color, line weight, and line type from the Line's properties
                // These properties automatically resolve to layer values if not overridden
                var effectiveColor = line.Color;
                var effectiveLineWeight = line.LineWeight;
                var effectiveLineType = line.LineType;

                // Convert System.Drawing.Color to Vector4 (normalized RGBA with alpha)
                Vector4 color = new Vector4(
                    effectiveColor.R / 255.0f,
                    effectiveColor.G / 255.0f,
                    effectiveColor.B / 255.0f,
                    effectiveColor.A / 255.0f  // Alpha channel for transparency
                );

                // Convert LineWeight to OpenGL width
                float lineWidth = effectiveLineWeight.ToOpenGLWidth();

                // Convert LineType to pattern index for shader
                int lineTypePattern = GetLineTypePattern(effectiveLineType);

                // Override for highlighted/selected objects
                if (context.IsSelected)
                {
                    // Selected objects: use original color with fine dashed pattern (pattern 8)
                    lineTypePattern = 8;
                    lineWidth = Math.Max(lineWidth, 2.0f); // Make selected lines at least 2px thick
                }
                else if (context.IsHighlighted)
                {
                    // Use the object's color but with halved alpha for semi-transparent highlight
                    color = new Vector4(color.X, color.Y, color.Z, color.W * 0.5f);
                    lineWidth = Math.Max(lineWidth, 2.0f); // Make highlighted lines at least 2px thick
                }

                // Determine rendering method based on line width
                bool useThinLineRendering = lineWidth <= THIN_LINE_THRESHOLD;

                // Orthographic detection: no perspective divide
                bool isOrtho = MathF.Abs(context.ProjectionMatrix.M34) < 1e-6f &&
                              MathF.Abs(context.ProjectionMatrix.M44 - 1f) < 1e-6f;

                if (isOrtho)
                {
                    RenderOrthographic(start, end, context.ProjectionMatrix, color, lineWidth, lineTypePattern, useThinLineRendering);
                }
                else
                {
                    RenderPerspective(start, end, context.ViewMatrix, context.ProjectionMatrix, color, lineWidth, lineTypePattern, useThinLineRendering);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LR] Exception rendering line: {ex.Message}");
                Debug.WriteLine($"[LR] Stack trace: {ex.StackTrace}");
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
                LineType.ByLayer => 0, // ByLayer should already be resolved, but default to continuous
                _ => 0
            };
        }

        /// <summary>
        /// Creates a quad (2 triangles) for a line segment with perpendicular end caps
        /// Returns null if the line is too short to render as a quad
        /// </summary>
        private float[]? CreateLineQuad(Vector2 start, Vector2 end, float halfWidth, out Vector2 perpDir)
        {
            perpDir = Vector2.Zero;

            // Check if line is too short (avoid division by zero)
            Vector2 lineVec = end - start;
            float lineLength = lineVec.Length();

            if (lineLength < MIN_LINE_LENGTH)
            {
                // Line is too short to render as a quad
                Debug.WriteLine($"[LR] Line too short to render as quad: {lineLength:F6}");
                return null;
            }

            // Calculate line direction and perpendicular
            Vector2 lineDir = lineVec / lineLength; // Manual normalization to avoid Vector2.Normalize with zero-length
            perpDir = new Vector2(-lineDir.Y, lineDir.X); // 90-degree rotation
            Vector2 offset = perpDir * halfWidth;

            // Create quad vertices (perpendicular to line direction)
            Vector2 v0 = start + offset;
            Vector2 v1 = start - offset;
            Vector2 v2 = end + offset;
            Vector2 v3 = end - offset;

            return new float[]
            {
                // Triangle 1
                v0.X, v0.Y, 0f,
                v1.X, v1.Y, 0f,
                v2.X, v2.Y, 0f,
                
                // Triangle 2
                v2.X, v2.Y, 0f,
                v1.X, v1.Y, 0f,
                v3.X, v3.Y, 0f
            };
        }

        private void RenderOrthographic(Point3D start, Point3D end, Matrix4x4 projectionMatrix, Vector4 color, float lineWidth, int lineTypePattern, bool useThinLineRendering)
        {
            // CPU path: derive ortho window from projection (row-major)
            float sx = projectionMatrix.M11;
            float sy = projectionMatrix.M22;
            float txRow = projectionMatrix.M41;
            float tyRow = projectionMatrix.M42;

            if (MathF.Abs(sx) > 1e-12f && MathF.Abs(sy) > 1e-12f)
            {
                // Get viewport dimensions
                int[] viewport = new int[4];
                GL.GetInteger(GetPName.Viewport, viewport);

                // Validate viewport dimensions
                if (viewport[2] <= 0 || viewport[3] <= 0)
                {
                    Debug.WriteLine($"[LR] Invalid viewport dimensions: {viewport[2]}x{viewport[3]}");
                    return;
                }

                _viewport = new Vector2(viewport[2], viewport[3]);

                float halfW = 1.0f / sx;
                float halfH = 1.0f / sy;
                float centerX = -txRow / sx;
                float centerY = -tyRow / sy;

                float ax = (float)start.X;
                float ay = (float)start.Y;
                float bx = (float)end.X;
                float by = (float)end.Y;

                // World XY -> NDC XY
                float ndcAx = (ax - centerX) / halfW;
                float ndcAy = (ay - centerY) / halfH;
                float ndcBx = (bx - centerX) / halfW;
                float ndcBy = (by - centerY) / halfH;

                // Validate NDC coordinates
                if (!IsValidFloat(ndcAx) || !IsValidFloat(ndcAy) || !IsValidFloat(ndcBx) || !IsValidFloat(ndcBy))
                {
                    Debug.WriteLine($"[LR] Invalid NDC coordinates");
                    return;
                }

                // Convert NDC to screen space for line start/end
                Vector2 screenStart = new Vector2(
                    (ndcAx * 0.5f + 0.5f) * _viewport.X,
                    (ndcAy * 0.5f + 0.5f) * _viewport.Y
                );
                Vector2 screenEnd = new Vector2(
                    (ndcBx * 0.5f + 0.5f) * _viewport.X,
                    (ndcBy * 0.5f + 0.5f) * _viewport.Y
                );

                // Set up shader uniforms
                _shaderProgram.Use();
                _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity);
                _shaderProgram.SetVector4("color", color);
                _shaderProgram.SetInt("lineTypePattern", lineTypePattern);
                _shaderProgram.SetVector2("lineStart", screenStart);
                _shaderProgram.SetVector2("lineEnd", screenEnd);
                _shaderProgram.SetVector2("viewport", _viewport);

                // Bind VAO (vertex attributes already configured in constructor)
                GL.BindVertexArray(_vao);

                if (useThinLineRendering)
                {
                    // Simple line rendering for thin lines
                    float[] ndcVerts =
                    {
                        ndcAx, ndcAy, 0f,
                        ndcBx, ndcBy, 0f
                    };

                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, ndcVerts.Length * sizeof(float), ndcVerts, BufferUsageHint.DynamicDraw);

                    GL.LineWidth(lineWidth);
                    GL.DrawArrays(PrimitiveType.Lines, 0, 2);

                    if (_debugTilt)
                    {
                        float lineLen = Vector2.Distance(screenStart, screenEnd);
                        Debug.WriteLine($"[LR][CPU-ORTHO-THIN] screenLen={lineLen:F1}px color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} pattern={lineTypePattern}");
                    }
                }
                else
                {
                    // Quad rendering for thick lines
                    Vector2 ndcStart = new Vector2(ndcAx, ndcAy);
                    Vector2 ndcEnd = new Vector2(ndcBx, ndcBy);

                    // Calculate half-width in NDC space
                    float halfWidthNDC = (lineWidth * 0.5f) / (_viewport.X * 0.5f);

                    float[]? ndcVerts = CreateLineQuad(ndcStart, ndcEnd, halfWidthNDC, out _);

                    if (ndcVerts != null)
                    {
                        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                        GL.BufferData(BufferTarget.ArrayBuffer, ndcVerts.Length * sizeof(float), ndcVerts, BufferUsageHint.DynamicDraw);

                        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

                        if (_debugTilt)
                        {
                            float lineLen = Vector2.Distance(screenStart, screenEnd);
                            Debug.WriteLine($"[LR][CPU-ORTHO-QUAD] screenLen={lineLen:F1}px color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} pattern={lineTypePattern}");
                        }
                    }
                    else
                    {
                        // Fallback to simple line rendering if quad creation failed
                        Debug.WriteLine("[LR] Falling back to simple line rendering for degenerate thick line");
                        float[] ndcVertsFallback =
                        {
                            ndcAx, ndcAy, 0f,
                            ndcBx, ndcBy, 0f
                        };

                        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                        GL.BufferData(BufferTarget.ArrayBuffer, ndcVertsFallback.Length * sizeof(float), ndcVertsFallback, BufferUsageHint.DynamicDraw);

                        GL.LineWidth(lineWidth);
                        GL.DrawArrays(PrimitiveType.Lines, 0, 2);
                    }
                }

                // Always unbind VAO
                GL.BindVertexArray(0);
                GLDiag.Check("LineRenderer draw end (CPU ortho)");
            }
        }

        private void RenderPerspective(Point3D start, Point3D end, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Vector4 color, float lineWidth, int lineTypePattern, bool useThinLineRendering)
        {
            // Get viewport dimensions
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);

            // Validate viewport dimensions
            if (viewport[2] <= 0 || viewport[3] <= 0)
            {
                Debug.WriteLine($"[LR] Invalid viewport dimensions: {viewport[2]}x{viewport[3]}");
                return;
            }

            _viewport = new Vector2(viewport[2], viewport[3]);

            // Row-vector semantics: MVP = M • V • P (GL will transpose on upload)
            var model = Matrix4x4.Identity;
            var mvpRow = model;
            mvpRow = Matrix4x4.Multiply(mvpRow, viewMatrix);
            mvpRow = Matrix4x4.Multiply(mvpRow, projectionMatrix);

            // Transform line endpoints to clip space
            Vector4 clipStart = Vector4.Transform(new Vector4((float)start.X, (float)start.Y, (float)start.Z, 1.0f), mvpRow);
            Vector4 clipEnd = Vector4.Transform(new Vector4((float)end.X, (float)end.Y, (float)end.Z, 1.0f), mvpRow);

            // Check for valid clip coordinates
            if (MathF.Abs(clipStart.W) < 0.0001f || MathF.Abs(clipEnd.W) < 0.0001f)
            {
                Debug.WriteLine("[LR] Invalid clip space coordinates (W near zero)");
                return;
            }

            // Convert to NDC
            Vector2 ndcStart = new Vector2(clipStart.X / clipStart.W, clipStart.Y / clipStart.W);
            Vector2 ndcEnd = new Vector2(clipEnd.X / clipEnd.W, clipEnd.Y / clipEnd.W);

            // Validate NDC coordinates
            if (!IsValidFloat(ndcStart.X) || !IsValidFloat(ndcStart.Y) || !IsValidFloat(ndcEnd.X) || !IsValidFloat(ndcEnd.Y))
            {
                Debug.WriteLine($"[LR] Invalid NDC coordinates in perspective");
                return;
            }

            // Convert to screen space for stippling
            Vector2 screenStart = new Vector2(
                (ndcStart.X * 0.5f + 0.5f) * _viewport.X,
                (ndcStart.Y * 0.5f + 0.5f) * _viewport.Y
            );
            Vector2 screenEnd = new Vector2(
                (ndcEnd.X * 0.5f + 0.5f) * _viewport.X,
                (ndcEnd.Y * 0.5f + 0.5f) * _viewport.Y
            );

            // Set up shader uniforms
            _shaderProgram.Use();
            _shaderProgram.SetVector4("color", color);
            _shaderProgram.SetInt("lineTypePattern", lineTypePattern);
            _shaderProgram.SetVector2("lineStart", screenStart);
            _shaderProgram.SetVector2("lineEnd", screenEnd);
            _shaderProgram.SetVector2("viewport", _viewport);

            // Bind VAO (vertex attributes already configured in constructor)
            GL.BindVertexArray(_vao);

            if (useThinLineRendering)
            {
                // Simple line rendering for thin lines
                float[] vertices =
                {
                    (float)start.X, (float)start.Y, (float)start.Z,
                    (float)end.X,   (float)end.Y,   (float)end.Z
                };

                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);

                _shaderProgram.SetMatrix4("mvp", mvpRow);

                GL.LineWidth(lineWidth);
                GL.DrawArrays(PrimitiveType.Lines, 0, 2);

                if (_debugTilt)
                {
                    float lineLen = Vector2.Distance(screenStart, screenEnd);
                    Debug.WriteLine($"[LR][PERSP-THIN] screenLen={lineLen:F1}px color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} pattern={lineTypePattern}");
                }
            }
            else
            {
                // Quad rendering for thick lines
                float halfWidthNDC = (lineWidth * 0.5f) / (_viewport.X * 0.5f);
                float[]? ndcVerts = CreateLineQuad(ndcStart, ndcEnd, halfWidthNDC, out _);

                if (ndcVerts != null)
                {
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, ndcVerts.Length * sizeof(float), ndcVerts, BufferUsageHint.DynamicDraw);

                    _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity); // Already in NDC

                    GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

                    if (_debugTilt)
                    {
                        float lineLen = Vector2.Distance(screenStart, screenEnd);
                        Debug.WriteLine($"[LR][PERSP-QUAD] screenLen={lineLen:F1}px color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2} pattern={lineTypePattern}");
                    }
                }
                else
                {
                    // Fallback to simple line rendering if quad creation failed
                    Debug.WriteLine("[LR] Falling back to simple line rendering for degenerate thick line");
                    float[] verticesFallback =
                    {
                        (float)start.X, (float)start.Y, (float)start.Z,
                        (float)end.X,   (float)end.Y,   (float)end.Z
                    };

                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, verticesFallback.Length * sizeof(float), verticesFallback, BufferUsageHint.DynamicDraw);

                    _shaderProgram.SetMatrix4("mvp", mvpRow);

                    GL.LineWidth(lineWidth);
                    GL.DrawArrays(PrimitiveType.Lines, 0, 2);
                }
            }

            // Always unbind VAO
            GL.BindVertexArray(0);
            GLDiag.Check("LineRenderer draw end (perspective)");
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