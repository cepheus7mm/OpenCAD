using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;

namespace GraphicsEngine
{
    public sealed class WorldPolygon
    {
        public Vector3[] Vertices { get; }
        public System.Drawing.Color FillColor { get; }
        public float Z => Vertices.Length > 0 ? Vertices[0].Z : 0f;

        public WorldPolygon(Vector3[] vertices, System.Drawing.Color fillColor)
        {
            // Assume vertices are in world space, CCW, non-self-intersecting
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            FillColor = fillColor;
        }
    }

    public sealed class WorldPolygonRenderer : IDisposable
    {
        private readonly int _vao;
        private readonly int _vbo;
        private readonly FillShaderProgram _shader;

        public WorldPolygonRenderer(FillShaderProgram shader)
        {
            _shader = shader ?? throw new ArgumentNullException(nameof(shader));

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            // vec3 position
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(
                0, 3, VertexAttribPointerType.Float,
                false, 3 * sizeof(float), 0);

            GL.BindVertexArray(0);
        }

        public void Render(WorldPolygon poly, Matrix4x4 view, Matrix4x4 proj)
        {
            if (poly.Vertices.Length < 3)
                return;

            // 1. Triangulate in world space (indices into poly.Vertices)
            var indices = Triangulate(poly.Vertices);
            if (indices.Length < 3)
                return;

            // 2. Build VP same way as line renderer
            Matrix4x4 vp = view * proj;

            // 3. Transform each indexed vertex: world → clip → NDC
            var ndcTriangles = new float[indices.Length * 3]; // xyz per vertex
            int t = 0;

            foreach (int idx in indices)
            {
                var v = poly.Vertices[idx];
                Vector4 world = new Vector4(v.X, v.Y, v.Z, 1f);

                Vector4 clip = Vector4.Transform(world, vp);

                if (Math.Abs(clip.W) < 1e-6f)
                    continue; // or skip / guard against divide-by-zero

                float ndcX = clip.X / clip.W;
                float ndcY = clip.Y / clip.W;
                float ndcZ = clip.Z / clip.W; // could be 0 if you want a flat overlay

                ndcTriangles[t++] = ndcX;
                ndcTriangles[t++] = ndcY;
                ndcTriangles[t++] = ndcZ;
            }

            int vertexCount = t / 3;
            if (vertexCount < 3)
                return;

            // 4. Upload NDC verts
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer,
                          vertexCount * 3 * sizeof(float),
                          ndcTriangles,
                          BufferUsageHint.DynamicDraw);

            // 5. Fixed state (same as before)
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // 6. Use shader with identity MVP
            _shader.Use();
            _shader.SetMvp(Matrix4x4.Identity);

            var c = poly.FillColor;
            _shader.SetColor(new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f));
            _shader.SetIsSelected(false);

            // 7. Draw in NDC
            GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);

            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
        }
        private static int[] Triangulate(Vector3[] verts)
        {
            // For now assume polygon is simple, CCW, no holes.
            // Use a standard ear clipping algorithm in XY plane (ignore Z).
            // Return indices into verts[].

            int n = verts.Length;
            if (n < 3) return Array.Empty<int>();

            var indices = new List<int>();
            var remaining = Enumerable.Range(0, n).ToList();

            int guard = 0;
            while (remaining.Count > 3 && guard++ < 10000)
            {
                bool earFound = false;

                for (int i = 0; i < remaining.Count; i++)
                {
                    int i0 = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int i1 = remaining[i];
                    int i2 = remaining[(i + 1) % remaining.Count];

                    var a = verts[i0];
                    var b = verts[i1];
                    var c = verts[i2];

                    // Check if ABC is convex
                    if (!IsConvex(a, b, c)) continue;

                    // Check no other point is inside triangle
                    bool anyInside = false;
                    for (int j = 0; j < remaining.Count; j++)
                    {
                        int idx = remaining[j];
                        if (idx == i0 || idx == i1 || idx == i2) continue;
                        if (PointInTriangle(verts[idx], a, b, c))
                        {
                            anyInside = true;
                            break;
                        }
                    }
                    if (anyInside) continue;

                    // We found an ear
                    indices.Add(i0);
                    indices.Add(i1);
                    indices.Add(i2);
                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                    break; // degenerate, give up
            }

            if (remaining.Count == 3)
            {
                indices.Add(remaining[0]);
                indices.Add(remaining[1]);
                indices.Add(remaining[2]);
            }

            return indices.ToArray();
        }

        private static bool IsConvex(Vector3 a, Vector3 b, Vector3 c)
        {
            // Work in XY plane
            var ab = new Vector2((float)(b.X - a.X), (float)(b.Y - a.Y));
            var bc = new Vector2((float)(c.X - b.X), (float)(c.Y - b.Y));
            float cross = ab.X * bc.Y - ab.Y * bc.X;
            return cross > 0f; // assuming CCW
        }

        private static bool PointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var pa = new Vector2((float)(p.X - a.X), (float)(p.Y - a.Y));
            var pb = new Vector2((float)(p.X - b.X), (float)(p.Y - b.Y));
            var pc = new Vector2((float)(p.X - c.X), (float)(p.Y - c.Y));

            var ab = new Vector2((float)(b.X - a.X), (float)(b.Y - a.Y));
            var bc = new Vector2((float)(c.X - b.X), (float)(c.Y - b.Y));
            var ca = new Vector2((float)(a.X - c.X), (float)(a.Y - c.Y));

            float c1 = ab.X * pa.Y - ab.Y * pa.X;
            float c2 = bc.X * pb.Y - bc.Y * pb.X;
            float c3 = ca.X * pc.Y - ca.Y * pc.X;

            bool hasNeg = (c1 < 0) || (c2 < 0) || (c3 < 0);
            bool hasPos = (c1 > 0) || (c2 > 0) || (c3 > 0);
            return !(hasNeg && hasPos);
        }
    }
}
