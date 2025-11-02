using OpenCAD.Geometry;
using OpenCAD;
using System.Numerics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace GraphicsEngine
{
    /// <summary>
    /// Modern OpenGL renderer for Line geometry using VBOs and shaders
    /// </summary>
    public class LineRenderer : IRenderer
    {
        private readonly ShaderProgram _shaderProgram;
        private int _vao; // Vertex Array Object
        private int _vbo; // Vertex Buffer Object
        private readonly bool _smokeTest = Environment.GetEnvironmentVariable("OPENCAD_SMOKE_TEST") == "1";
        private static readonly bool _debugTilt = Environment.GetEnvironmentVariable("OPENCAD_DEBUG_TILT") == "1";

        // Color constants for highlighting and selection (RGBA)
        private static readonly Vector4 HighlightColor = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);   // Yellow, fully opaque
        private static readonly Vector4 SelectedColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);    // Green, fully opaque

        public LineRenderer(ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            if (_vao == 0 || _vbo == 0)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Failed to create VAO/VBO (vao={_vao}, vbo={_vbo}). Is a GL context current?");
            }

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

            var start = line.Start;
            var end = line.End;

            // Get effective color and line weight from the object
            var effectiveColor = obj.GetEffectiveColor();
            var effectiveLineWeight = obj.GetEffectiveLineWeight();
            
            // Convert System.Drawing.Color to Vector4 (normalized RGBA with alpha)
            Vector4 color = new Vector4(
                effectiveColor.R / 255.0f,
                effectiveColor.G / 255.0f,
                effectiveColor.B / 255.0f,
                effectiveColor.A / 255.0f  // Alpha channel for transparency
            );
            
            // Convert LineWeight to OpenGL width
            float lineWidth = effectiveLineWeight.ToOpenGLWidth();

            // Override color and line width for highlighted/selected objects
            if (context.IsSelected)
            {
                color = SelectedColor;
                lineWidth = Math.Max(lineWidth, 2.0f); // Make selected lines at least 2px thick
            }
            else if (context.IsHighlighted)
            {
                color = HighlightColor;
                lineWidth = Math.Max(lineWidth, 2.0f); // Make highlighted lines at least 2px thick
            }

            // Orthographic detection: no perspective divide
            bool isOrtho = MathF.Abs(context.ProjectionMatrix.M34) < 1e-6f && 
                          MathF.Abs(context.ProjectionMatrix.M44 - 1f) < 1e-6f;

            if (isOrtho)
            {
                RenderOrthographic(start, end, context.ProjectionMatrix, color, lineWidth);
            }
            else
            {
                RenderPerspective(start, end, context.ViewMatrix, context.ProjectionMatrix, color, lineWidth);
            }
        }

        private void RenderOrthographic(Point3D start, Point3D end, Matrix4x4 projectionMatrix, Vector4 color, float lineWidth)
        {
            // CPU path: derive ortho window from projection (row-major)
            float sx = projectionMatrix.M11;
            float sy = projectionMatrix.M22;
            float txRow = projectionMatrix.M41;
            float tyRow = projectionMatrix.M42;

            if (MathF.Abs(sx) > 1e-12f && MathF.Abs(sy) > 1e-12f)
            {
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

                float[] ndcVerts =
                {
                    ndcAx, ndcAy, 0f,
                    ndcBx, ndcBy, 0f
                };

                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, ndcVerts.Length * sizeof(float), ndcVerts, BufferUsageHint.DynamicDraw);

                _shaderProgram.Use();
                _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity);
                _shaderProgram.SetVector4("color", color);  // Pass RGBA color

                GL.BindVertexArray(_vao);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);

                GL.LineWidth(lineWidth);
                GL.DrawArrays(PrimitiveType.Lines, 0, 2);
                GL.BindVertexArray(0);

                GLDiag.Check("LineRenderer draw end (CPU ortho)");

                if (_debugTilt)
                {
                    Debug.WriteLine($"[LR][CPU-ORTHO] center=({centerX:F4},{centerY:F4}) half=({halfW:F4},{halfH:F4}) A_ndc=({ndcAx:F4},{ndcAy:F4}) B_ndc=({ndcBx:F4},{ndcBy:F4}) color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2}");
                }
            }
        }

        private void RenderPerspective(Point3D start, Point3D end, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Vector4 color, float lineWidth)
        {
            float[] vertices =
            {
                (float)start.X, (float)start.Y, (float)start.Z,
                (float)end.X,   (float)end.Y,   (float)end.Z
            };

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);

            _shaderProgram.Use();

            // Row-vector semantics: MVP = M • V • P (GL will transpose on upload)
            var model = Matrix4x4.Identity;
            var mvpRow = model;
            mvpRow = Matrix4x4.Multiply(mvpRow, viewMatrix);
            mvpRow = Matrix4x4.Multiply(mvpRow, projectionMatrix);

            if (_debugTilt)
            {
                float m11 = mvpRow.M11, m12 = mvpRow.M12, m21 = mvpRow.M21, m22 = mvpRow.M22;
                float tx = mvpRow.M41, ty = mvpRow.M42;
                Debug.WriteLine($"[LR] mode=Persp viewIdentity={viewMatrix.IsIdentity}");
                Debug.WriteLine($"[LR] MVP 2x2: [[{m11:F6}, {m12:F6}], [{m21:F6}, {m22:F6}]]  trans=({tx:F6},{ty:F6}) color=({color.X:F2},{color.Y:F2},{color.Z:F2},{color.W:F2}) lineWidth={lineWidth:F2}");
            }

            _shaderProgram.SetMatrix4("mvp", mvpRow);
            _shaderProgram.SetVector4("color", color);  // Pass RGBA color

            GL.BindVertexArray(_vao);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GLDiag.DumpPipelineState(_vao, _vbo, _shaderProgram.ProgramId);

            GL.LineWidth(lineWidth);
            GL.DrawArrays(PrimitiveType.Lines, 0, 2);
            GL.BindVertexArray(0);

            GLDiag.Check("LineRenderer draw end");
        }
    }
}