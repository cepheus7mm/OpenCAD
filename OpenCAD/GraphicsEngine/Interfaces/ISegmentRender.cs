using OpenCAD.SegmentSource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine.Interfaces
{
    public interface ISegmentRenderer
    {
        void BeginFrame(Viewport viewport, Matrix4x4 viewProj);
        void DrawSegments(IEnumerable<Segment> segments, Viewport vp, LinetypeGpuData linetypeGpuData);
        void EndFrame();
    }
}
