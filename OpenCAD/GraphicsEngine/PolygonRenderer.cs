using OpenTK.Graphics.OpenGL;
using System.Numerics;

namespace GraphicsEngine
{
    /// <summary>
    /// Renderer for filled polygons using triangle tessellation
    /// </summary>
    public class PolygonRenderer
    {
        private readonly ShaderProgram _shaderProgram;
        private int _vao;
        private int _vbo;

        public PolygonRenderer(ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            GLDiag.Check("PolygonRenderer ctor end");
        }

        /// <summary>
        /// Render a filled polygon from triangulated vertices
        /// </summary>
        public void RenderTriangles(float[] vertices, Vector3 color, Matrix4x4 mvp)
        {
            if (vertices == null || vertices.Length < 9) return; // Need at least 3 vertices (1 triangle)

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);

            _shaderProgram.Use();
            _shaderProgram.SetMatrix4("mvp", mvp);
            _shaderProgram.SetVector3("color", color);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Length / 3);
            GL.BindVertexArray(0);

            GLDiag.Check("PolygonRenderer.RenderTriangles end");
        }
    }
}