using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.SegmentSource
{
    public struct LinetypeGpuData
    {
        public int PatternCount;
        public float PatternLength;
        
        // Use managed array instead of fixed buffer (no unsafe needed)
        public float[] Pattern; // Will be packed into vec4[16] in std140

        public LinetypeGpuData()
        {
            PatternCount = 0;
            PatternLength = 0;
            Pattern = new float[64]; // Initialize with 64 elements
        }
    }

    public readonly struct Segment
    {
        public readonly Vector2 A;
        public readonly Vector2 B;
        public readonly float WidthA;
        public readonly float WidthB;
        public readonly float D0;
        public readonly float D1;
        public readonly int LineTypeId;
        public readonly Vector4 Color;

        public Segment(
            Vector2 a, Vector2 b,
            float widthA, float widthB,
            float d0, float d1,
            int lineTypeId, Vector4 color)
        {
            A = a;
            B = b;
            WidthA = widthA;
            WidthB = widthB;
            D0 = d0;
            D1 = d1;
            LineTypeId = lineTypeId;
            Color = color;
        }
    }
}
