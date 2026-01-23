using OpenCAD.SegmentSource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine
{
    public class SegmentBatchBuilder
    {
        private readonly List<SegmentVertex> _vertices = new();

        //public void Add(Segment s, Matrix4x4 viewProj)
        //{
        //    Vector2 a = RenderEngine.WorldToNdc(s.A, viewProj);
        //    Vector2 b = RenderEngine.WorldToNdc(s.B, viewProj);

        //    //_vertices.Add(new SegmentVertex(a, b, s.WidthA, s.WidthB, s.D0, s.D1, -1, s.Color, s.LineTypeId));
        //    //_vertices.Add(new SegmentVertex(a, b, s.WidthA, s.WidthB, s.D0, s.D1, +1, s.Color, s.LineTypeId));
            
        //    AddSegment(a, b, s.WidthA);

        //}

        public void AddSegment(Viewport vp, Segment segment)
        {
            Vector2 aNdc = vp.WorldToNdc(segment.A);
            Vector2 bNdc = vp.WorldToNdc(segment.B);

            // 1. Convert NDC → screen pixels
            Vector2 aScreen = vp.NdcToScreen(aNdc);
            Vector2 bScreen = vp.NdcToScreen(bNdc);

            // 2. Compute direction in screen space
            Vector2 dir = bScreen - aScreen;
            if (dir.LengthSquared() < 1e-12f)
                return; // zero-length segment

            dir = Vector2.Normalize(dir);

            // 3. Perpendicular vector
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            // 4. Pixel offset
            Vector2 offsetA = perp * (vp.MillimetersToPixels(segment.WidthA) * 0.5f);
            Vector2 offsetB = perp * (vp.MillimetersToPixels(segment.WidthB) * 0.5f);

            // 5. Expanded quad corners in screen space
            Vector2 a0 = aScreen + offsetA;
            Vector2 a1 = aScreen - offsetA;
            Vector2 b0 = bScreen + offsetB;
            Vector2 b1 = bScreen - offsetB;

            // 6. Convert back to NDC
            Vector2 a0Ndc = vp.ScreenToNdc(a0);
            Vector2 a1Ndc = vp.ScreenToNdc(a1);
            Vector2 b0Ndc = vp.ScreenToNdc(b0);
            Vector2 b1Ndc = vp.ScreenToNdc(b1);

            // 7. Emit final quad vertices (no shader expansion needed)
            _vertices.Add(new SegmentVertex { Position = a0Ndc, Color = segment.Color, Distance = segment.D0 });
            _vertices.Add(new SegmentVertex { Position = a1Ndc, Color = segment.Color, Distance = segment.D0 });
            _vertices.Add(new SegmentVertex { Position = b0Ndc, Color = segment.Color, Distance = segment.D1 });
            _vertices.Add(new SegmentVertex { Position = b1Ndc, Color = segment.Color, Distance = segment.D1 });
        }

        public ReadOnlySpan<SegmentVertex> Build() => CollectionsMarshal.AsSpan(_vertices);
    }
}
