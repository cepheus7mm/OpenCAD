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
        private readonly FillShaderProgram _fillShader = new FillShaderProgram();
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

        /// <summary>
        /// Renders a filled polygon with transparency support
        /// </summary>
        public void RenderFilled(Vector3[] vertices, System.Drawing.Color fillColor, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (vertices == null || vertices.Length < 3) return;

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _fillShader.Use();

            // Correct MVP order: projection * view
            Matrix4x4 mvp = Matrix4x4.Multiply(projectionMatrix, viewMatrix);
            _fillShader.SetMvp(mvp);

            Vector4 rgba = new Vector4(
                fillColor.R / 255f,
                fillColor.G / 255f,
                fillColor.B / 255f,
                fillColor.A / 255f);
            _fillShader.SetColor(rgba);
            _fillShader.SetIsSelected(false);

            int vao = GL.GenVertexArray();
            int vbo = GL.GenBuffer();

            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            float[] vertexData = new float[vertices.Length * 3];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertexData[i * 3 + 0] = vertices[i].X;
                vertexData[i * 3 + 1] = vertices[i].Y;
                vertexData[i * 3 + 2] = vertices[i].Z;
            }

            GL.BufferData(BufferTarget.ArrayBuffer, vertexData.Length * sizeof(float), vertexData, BufferUsageHint.DynamicDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.DrawArrays(PrimitiveType.TriangleFan, 0, vertices.Length);

            GL.BindVertexArray(0);
            GL.DeleteBuffer(vbo);
            GL.DeleteVertexArray(vao);

            GLDiag.Check("PolygonRenderer.RenderFilled end");
        }
    }
}