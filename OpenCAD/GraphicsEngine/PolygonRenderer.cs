using OpenTK.Graphics.OpenGL;
using System.Drawing;
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

        public PolygonRenderer(FillShaderProgram shaderProgram)
        {
            _fillShader = shaderProgram;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            // Position attribute
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.BindVertexArray(0);
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
        public void RenderFilled(Vector3[] vertices, Color fillColor,
                                 Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (vertices == null || vertices.Length < 3)
                return;

            // Hard‑set state so we don't depend on whoever drew before us
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _fillShader.Use();

            //Matrix4x4 mvp = projectionMatrix * viewMatrix;
            //_fillShader.SetMvp(mvp);

            // Triangulate polygon
            float[] triVerts = TriangulatePolygon(vertices);
            if (triVerts.Length < 9)
                return;

            // Upload once per draw
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer,
                          triVerts.Length * sizeof(float),
                          triVerts,
                          BufferUsageHint.DynamicDraw);

            // Enable blending
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Use fill shader
            _fillShader.Use();

            // MVP = projection * view
            Matrix4x4 mvp = viewMatrix * projectionMatrix;
            _fillShader.SetMvp(mvp);

            Vector4 rgba = new Vector4(
                fillColor.R / 255f,
                fillColor.G / 255f,
                fillColor.B / 255f,
                fillColor.A / 255f);

            _fillShader.SetColor(rgba);
            _fillShader.SetIsSelected(false);

            // Draw triangles
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, triVerts.Length / 3);
            GL.BindVertexArray(0);
        }
        public static float[] TriangulatePolygon(Vector3[] verts)
        {
            // Project to 2D (XY plane)
            int n = verts.Length;
            var points = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
                points.Add(new Vector2(verts[i].X, verts[i].Y));

            // Ensure CCW winding
            if (SignedArea(points) < 0)
                points.Reverse();

            var indices = new List<int>();
            var V = new List<int>();
            for (int i = 0; i < n; i++)
                V.Add(i);

            int count = 0;
            while (V.Count > 2)
            {
                bool earFound = false;

                for (int i = 0; i < V.Count; i++)
                {
                    int prev = V[(i - 1 + V.Count) % V.Count];
                    int curr = V[i];
                    int next = V[(i + 1) % V.Count];

                    if (IsEar(prev, curr, next, points, V))
                    {
                        indices.Add(prev);
                        indices.Add(curr);
                        indices.Add(next);

                        V.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                {
                    // Degenerate polygon or numerical issue
                    break;
                }

                if (++count > 10000)
                    break; // safety
            }

            // Convert to float[] for OpenGL
            var result = new List<float>(indices.Count * 3 * 3);
            foreach (int idx in indices)
            {
                result.Add(verts[idx].X);
                result.Add(verts[idx].Y);
                result.Add(verts[idx].Z);
            }

            return result.ToArray();
        }
        private static float SignedArea(List<Vector2> pts)
        {
            float area = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                int j = (i + 1) % pts.Count;
                area += pts[i].X * pts[j].Y - pts[j].X * pts[i].Y;
            }
            return area * 0.5f;
        }
        private static bool IsEar(int i0, int i1, int i2,
                          List<Vector2> pts, List<int> V)
        {
            Vector2 a = pts[i0];
            Vector2 b = pts[i1];
            Vector2 c = pts[i2];

            // Must be convex
            if (Cross(b - a, c - b) <= 0)
                return false;

            // Check if any other point lies inside triangle
            for (int k = 0; k < V.Count; k++)
            {
                int vi = V[k];
                if (vi == i0 || vi == i1 || vi == i2)
                    continue;

                if (PointInTriangle(pts[vi], a, b, c))
                    return false;
            }

            return true;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float c1 = Cross(b - a, p - a);
            float c2 = Cross(c - b, p - b);
            float c3 = Cross(a - c, p - c);

            bool hasNeg = (c1 < 0) || (c2 < 0) || (c3 < 0);
            bool hasPos = (c1 > 0) || (c2 > 0) || (c3 > 0);

            return !(hasNeg && hasPos);
        }
    }
}