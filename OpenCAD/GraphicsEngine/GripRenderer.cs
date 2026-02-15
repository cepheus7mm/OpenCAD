using OpenCAD.Grips;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace GraphicsEngine
{
    public sealed class GripRenderer
    {
        private readonly Shader _quadShader = new Shader(QuadShader.VertexShader, QuadShader.FragmentShader);      // screen-space quad shader
        private readonly Shader _glowShader = new Shader(GlowShader.VertexShader, GlowShader.FragmentShader);      // glow source shader
        private readonly int _vbo;
        private readonly int _vao;

        private readonly Viewport _viewport;

        public GripRenderer(Viewport viewport)
        {
            _viewport = viewport;

            // Create VAO/VBO for dynamic quad batches
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            // Each vertex = vec2 position + vec4 color
            int stride = (2 + 4) * sizeof(float);

            GL.EnableVertexAttribArray(0); // position
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);

            GL.EnableVertexAttribArray(1); // color
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

            GL.BindVertexArray(0);
        }

        // ------------------------------------------------------------
        // PUBLIC ENTRY POINT
        // ------------------------------------------------------------
        public void RenderGrips(
            IEnumerable<GripVisual> grips,
            GripVisual? hoverGrip,
            GripVisual? activeGrip)
        {
            // 1. Glow pass (hover + active)
            BeginGlowPass();
            DrawGlowGrips(hoverGrip, activeGrip);
            EndGlowPass();

            // 2. Overlay pass (all grips)
            BeginOverlayPass();
            DrawGripQuads(grips, hoverGrip, activeGrip);
            EndOverlayPass();
        }

        // ------------------------------------------------------------
        // GLOW PASS
        // ------------------------------------------------------------
        private void BeginGlowPass()
        {
            _glowShader.Use();
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        }

        private void DrawGlowGrips(GripVisual? hover, GripVisual? active)
        {
            var list = new List<GripVisual>();
            if (hover.HasValue) list.Add(hover.Value);
            if (active.HasValue) list.Add(active.Value);

            if (list.Count == 0)
                return;

            // ✅ Draw each grip separately with its own color
            foreach (var grip in list)
            {
                _glowShader.SetVector4("uGlowColor", grip.Style.GlowColor);
                UploadAndDraw(new[] { grip }, glow: true);
            }
        }

        private void EndGlowPass()
        {
            GL.Disable(EnableCap.Blend);
        }

        // ------------------------------------------------------------
        // OVERLAY PASS
        // ------------------------------------------------------------
        private void BeginOverlayPass()
        {
            _quadShader.Use();
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _quadShader.SetVector2("uViewportSize", new Vector2(_viewport.PixelWidth, _viewport.PixelHeight));
        }

        private void DrawGripQuads(
            IEnumerable<GripVisual> grips,
            GripVisual? hover,
            GripVisual? active)
        {
            var all = new List<GripVisual>();
            all.AddRange(grips);

            if (hover.HasValue)
                all.Add(hover.Value);

            if (active.HasValue)
                all.Add(active.Value);

            UploadAndDraw(all, glow: false);
        }

        private void EndOverlayPass()
        {
            GL.Disable(EnableCap.Blend);
        }

        // ------------------------------------------------------------
        // CORE BATCHING LOGIC
        // ------------------------------------------------------------
        private void UploadAndDraw(IEnumerable<GripVisual> grips, bool glow)
        {
            var verts = new List<float>();

            foreach (var grip in grips)
            {
                BuildQuadVertices(grip, glow, verts);
            }

            if (verts.Count == 0)
                return;

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            GL.BufferData(BufferTarget.ArrayBuffer,
                verts.Count * sizeof(float),
                verts.ToArray(),
                BufferUsageHint.DynamicDraw);

            int vertexCount = verts.Count / 6; // 2 pos + 4 color
            GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
        }

        // ------------------------------------------------------------
        // QUAD BUILDER
        // ------------------------------------------------------------
        private void BuildQuadVertices(GripVisual grip, bool glow, List<float> verts)
        {
            float sizePx = glow ? grip.Style.GlowRadius + 4f : grip.Style.SizePx;
            float half = sizePx * 0.5f;

            Vector2 screen = _viewport.WorldToScreen(grip.WorldPosition);

            Vector2 p0 = new(screen.X - half, screen.Y - half);
            Vector2 p1 = new(screen.X + half, screen.Y - half);
            Vector2 p2 = new(screen.X + half, screen.Y + half);
            Vector2 p3 = new(screen.X - half, screen.Y + half);

            Vector4 color = glow ? grip.Style.GlowColor : grip.Style.BaseColor;

            if (glow)
            {
                p0 = ScreenToNdc(p0);
                p1 = ScreenToNdc(p1);
                p2 = ScreenToNdc(p2);
                p3 = ScreenToNdc(p3);
            }

            AddTri(verts, p0, p1, p2, color);
            AddTri(verts, p2, p3, p0, color);
        }

        private void AddTri(List<float> verts, Vector2 a, Vector2 b, Vector2 c, Vector4 color)
        {
            AddVert(verts, a, color);
            AddVert(verts, b, color);
            AddVert(verts, c, color);
        }

        private void AddVert(List<float> verts, Vector2 pos, Vector4 color)
        {
            verts.Add(pos.X);
            verts.Add(pos.Y);
            verts.Add(color.X);
            verts.Add(color.Y);
            verts.Add(color.Z);
            verts.Add(color.W);
        }
        private Vector2 ScreenToNdc(Vector2 p)
        {
            float x = (p.X / _viewport.PixelWidth) * 2f - 1f;
            float y = 1f - (p.Y / _viewport.PixelHeight) * 2f;
            return new Vector2(x, y);
        }
    }
}
