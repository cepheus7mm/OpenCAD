using GraphicsEngine.Interfaces;
using OpenCAD.SegmentSource;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SysVec2 = System.Numerics.Vector2;
using SysVec3 = System.Numerics.Vector3;
using SysVec4 = System.Numerics.Vector4;
using SysMat4 = System.Numerics.Matrix4x4;

using GlVec2 = OpenTK.Mathematics.Vector2;
using GlVec3 = OpenTK.Mathematics.Vector3;
using GlMat4 = OpenTK.Mathematics.Matrix4;

namespace GraphicsEngine
{
    public class SegmentRenderer : ISegmentRenderer
    {
        private readonly Shader _shader = new Shader(UnifiedSegmentShaders.VertexShader,
                        UnifiedSegmentShaders.FragmentShader);
        private readonly Shader _glowShader = new Shader(GlowShader.VertexShader, GlowShader.FragmentShader);

        private Matrix4x4 _viewProj;
        
        // Buffers for normal segment rendering
        private int _vao;
        private int _vbo;
        private int _linetypeUbo;

        // Dedicated buffers for glow rendering
        private int _glowVao;
        private int _glowVbo;

        float GlowRadiusPx = 3f;

        public SegmentRenderer(Shader shader)
        {
            _shader = shader;
            CreateBuffers();
            int blockIndex = GL.GetUniformBlockIndex(_shader.Handle, "LinetypeUBO");
            if (blockIndex != -1)
            {
                System.Diagnostics.Debug.WriteLine($"✅ LinetypeUBO found at block index: {blockIndex}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ LinetypeUBO block not found in shader!");
            }
        }

        public void CreateBuffers()
        {
            // ========================================
            // Create buffers for NORMAL segment rendering
            // ========================================
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            int stride = sizeof(float) * (2 + 4 + 1); // vec2 + vec4 + float

            // Position (location = 0)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(
                index: 0,
                size: 2,
                type: VertexAttribPointerType.Float,
                normalized: false,
                stride: stride,
                offset: 0
            );

            // Color (location = 1)
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(
                index: 1,
                size: 4,
                type: VertexAttribPointerType.Float,
                normalized: false,
                stride: stride,
                offset: sizeof(float) * 2
            );

            // Distance (2)
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, sizeof(float) * 6);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            _linetypeUbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, _linetypeUbo);

            // std140: 16 bytes header + 64 vec4s * 16 bytes each = 1040 bytes
            const int LinetypeUboSizeBytes = 1040;

            GL.BufferData(BufferTarget.UniformBuffer, LinetypeUboSizeBytes, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 3, _linetypeUbo);

            // ========================================
            // Create dedicated buffers for GLOW rendering
            // ========================================
            _glowVao = GL.GenVertexArray();
            _glowVbo = GL.GenBuffer();

            GL.BindVertexArray(_glowVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _glowVbo);

            int glowStride = sizeof(float) * 2; // Only position (vec2)

            // Position only (location = 0)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(
                index: 0,
                size: 2,
                type: VertexAttribPointerType.Float,
                normalized: false,
                stride: glowStride,
                offset: 0
            );

            // Disable color attribute for glow
            GL.DisableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

        public void BeginFrame(Viewport vp, Matrix4x4 viewProj)
        {
            _viewProj = viewProj;
            _shader.Use();
            _shader.SetMatrix4("uViewProj", viewProj);
            _shader.SetVector2("uViewportSize", new SysVec2(vp.PixelWidth, vp.PixelHeight));
        }

        private void UploadAndDraw(ReadOnlySpan<SegmentVertex> vertices, LinetypeGpuData linetypeGpuData)
        {
            if (vertices.Length == 0)
                return;

            var packed = PackLinetypeToStd140(linetypeGpuData);

            GL.BindBuffer(BufferTarget.UniformBuffer, _linetypeUbo);
            GL.BufferSubData(
                BufferTarget.UniformBuffer,
                IntPtr.Zero,
                packed.Length * sizeof(float),
                packed
            );

            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 3, _linetypeUbo);

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            _shader.SetFloat("uHalfWidthPixels", 3f);

            int sizeInBytes = vertices.Length * Marshal.SizeOf<SegmentVertex>();
            GL.BufferData(BufferTarget.ArrayBuffer, sizeInBytes, vertices.ToArray(), BufferUsageHint.DynamicDraw);
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, vertices.Length);

            GL.BindVertexArray(0);
        }


        public void EndFrame() { }

        public void DrawSegments(IEnumerable<Segment> segments, Viewport vp, LinetypeGpuData linetypeGpuData)
        {
            var builder = new SegmentBatchBuilder();

            foreach (var s in segments)
            {
                builder.AddSegment(vp, s);
            }

            var verts = builder.Build();
            UploadAndDraw(verts, linetypeGpuData);
        }
        
        public void BeginGlowPass(Viewport vp, SysMat4 proj)
        {
            // Enable blending
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Depth test ON, but depth write OFF
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(false);

            // Use your glow shader or enable glow mode
            _glowShader.Use();
            _glowShader.SetMatrix4("uProjection", proj);
            _glowShader.SetVector2("uViewportSize", new SysVec2(vp.PixelWidth, vp.PixelHeight));
            _glowShader.SetVector4("uGlowColor", new SysVec4(1f, 1f, 0f, 0.25f)); // bright yellow

            // Glow radius in pixels (tweak as needed)
            _glowShader.SetFloat("uGlowRadiusPx", GlowRadiusPx);
        }

        public void DrawGlow(IEnumerable<Segment> segments, Viewport vp)
        {
            var isClosedLoop = segments.FirstOrDefault() is Segment first && segments.LastOrDefault() is Segment last &&
                             (first.A - last.B).LengthSquared() < 1e-6f;
            for (int i = 0; i < segments.Count(); i++)
            {
                var seg = segments.ElementAt(i);
                EmitGlowQuad(seg, vp);

                float halfWidthPx = vp.MillimetersToPixels(seg.WidthA) * 0.5f;
                float radiusPx = halfWidthPx + GlowRadiusPx;

                if (i == 0 && !isClosedLoop)
                {
                    // Start cap at A
                    EmitGlowCapHalf(seg.A, seg.B, radiusPx, vp);
                }
                if (i == segments.Count() - 1 && !isClosedLoop)
                {
                    // Half-circle at B (facing outward)
                    EmitGlowCapHalf(seg.B, seg.A, radiusPx, vp);
                }
            }
        }

        public void EndGlowPass()
        {
            // Restore depth write
            GL.DepthMask(true);

            // Restore blending state if needed
            GL.Disable(EnableCap.Blend);
        }

        private void EmitGlowQuad(Segment segment, Viewport vp)
        {
            // Convert world → NDC
            SysVec2 ndcA = vp.WorldToNdc(segment.A);
            SysVec2 ndcB = vp.WorldToNdc(segment.B);

            // Convert NDC → screen
            SysVec2 sA = vp.NdcToScreen(ndcA);
            SysVec2 sB = vp.NdcToScreen(ndcB);

            // Direction and perpendicular in screen space
            SysVec2 dir = SysVec2.Normalize(sB - sA);
            SysVec2 perp = new SysVec2(-dir.Y, dir.X);

            float halfWidthPx = (vp.MillimetersToPixels(segment.WidthA) * 0.5f);
            float glowPx = GlowRadiusPx;

            float total = halfWidthPx + glowPx;

            // Expand in screen space
            SysVec2 sA0 = sA + perp * total;
            SysVec2 sA1 = sA - perp * total;
            SysVec2 sB0 = sB + perp * total;
            SysVec2 sB1 = sB - perp * total;

            // Convert back to NDC
            SysVec2 ndcA0 = vp.ScreenToNdc(sA0);
            SysVec2 ndcA1 = vp.ScreenToNdc(sA1);
            SysVec2 ndcB0 = vp.ScreenToNdc(sB0);
            SysVec2 ndcB1 = vp.ScreenToNdc(sB1);

            // Upload vertices (two triangles)
            float[] verts =
            {
                ndcA0.X, ndcA0.Y,
                ndcA1.X, ndcA1.Y,
                ndcB0.X, ndcB0.Y,

                ndcA1.X, ndcA1.Y,
                ndcB0.X, ndcB0.Y,
                ndcB1.X, ndcB1.Y
            };

            // Use the dedicated GLOW VAO and VBO
            GL.BindVertexArray(_glowVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _glowVbo);
            
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StreamDraw);

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
    
            GL.BindVertexArray(0);
        }

        private void EmitGlowCapHalf(SysVec2 worldPos, SysVec2 worldOther, float radiusPx, Viewport vp)
        {
            // Compute direction in screen space
            SysVec2 dir = GetScreenDirection(worldPos, worldOther, vp);

            // Perpendicular vector
            SysVec2 perp = new SysVec2(-dir.Y, dir.X);

            // Convert center to NDC and screen
            SysVec2 ndcCenter = vp.WorldToNdc(worldPos);
            SysVec2 centerScreen = vp.NdcToScreen(ndcCenter);

            // Determine half-circle angle range
            float baseAngle = MathF.Atan2(dir.Y, dir.X) + MathF.PI;
            float startAngle = baseAngle - MathF.PI / 2f;
            float endAngle = baseAngle + MathF.PI / 2f;

            const int segments = 20;
            float angleStep = (endAngle - startAngle) / segments;

            // Allocate triangle fan: center + (segments+1) ring vertices
            float[] verts = new float[(segments + 2) * 2];

            // Center vertex (in NDC)
            verts[0] = ndcCenter.X;
            verts[1] = ndcCenter.Y;

            // Generate half-circle arc
            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + i * angleStep;

                float sx = centerScreen.X + MathF.Cos(angle) * radiusPx;
                float sy = centerScreen.Y + MathF.Sin(angle) * radiusPx;

                SysVec2 ndcPt = vp.ScreenToNdc(new SysVec2(sx, sy));

                verts[(i + 1) * 2 + 0] = ndcPt.X;
                verts[(i + 1) * 2 + 1] = ndcPt.Y;
            }

            // Upload and draw
            GL.BindVertexArray(_glowVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _glowVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StreamDraw);

            GL.DrawArrays(PrimitiveType.TriangleFan, 0, segments + 2);
        }

        private SysVec2 GetScreenDirection(SysVec2 worldA, SysVec2 worldB, Viewport vp)
        {
            SysVec2 ndcA = vp.WorldToNdc(worldA);
            SysVec2 ndcB = vp.WorldToNdc(worldB);

            SysVec2 sA = vp.NdcToScreen(ndcA);
            SysVec2 sB = vp.NdcToScreen(ndcB);

            SysVec2 dir = sB - sA;
            if (dir.LengthSquared() < 1e-12f)
                return new SysVec2(1, 0); // fallback

            return SysVec2.Normalize(dir);
        }

        // Optional: Add cleanup method
        public void Dispose()
        {
            // Delete normal rendering buffers
            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }
            if (_vbo != 0)
            {
                GL.DeleteBuffer(_vbo);
                _vbo = 0;
            }

            // Delete glow rendering buffers
            if (_glowVao != 0)
            {
                GL.DeleteVertexArray(_glowVao);
                _glowVao = 0;
            }
            if (_glowVbo != 0)
            {
                GL.DeleteBuffer(_glowVbo);
                _glowVbo = 0;
            }
        }

        // std140 layout:
        // offset 0:  int PatternCount (4 bytes)
        // offset 4:  float PatternLength (4 bytes)
        // offset 8-15: padding (8 bytes)
        // offset 16+: vec4[64] array (1024 bytes total)

        private float[] PackLinetypeToStd140(LinetypeGpuData src)
        {
            // 1040 bytes total / 4 bytes per float = 260 floats
            var data = new float[260];

            // Header (16 bytes = 4 floats)
            data[0] = BitConverter.Int32BitsToSingle(src.PatternCount);
            data[1] = src.PatternLength;
            // data[2] and data[3] are padding (remain 0)

            // Array starts at float index 4 (offset 16 bytes)
            // Pack 4 pattern values per vec4 (using x,y,z,w components)
            int baseIndex = 4;
            int patternCount = Math.Min(src.Pattern?.Length ?? 0, 64);
            
            for (int i = 0; i < patternCount; i++)
            {
                int vec4Index = i / 4;           // Which vec4 (0-15 for 64 values)
                int component = i % 4;            // Which component (x,y,z,w = 0,1,2,3)
                int floatIndex = baseIndex + (vec4Index * 4) + component;
                
                data[floatIndex] = src.Pattern[i];
            }

            return data;
        }
    }
}
