using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LineVertex
    {
        public Vector2 Position; // already in NDC
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SegmentVertex
    {
        public Vector2 Position;
        public Vector4 Color;
        public float Distance;
    }

    //public SegmentVertex(
    //    Vector2 a,
    //    Vector2 b,
    //    float widthA,
    //    float widthB,
    //    float d0,
    //    float d1,
    //    float side,
    //    uint color,
    //    int lineTypeId
    //)
    //{
    //    A = a;
    //    B = b;
    //    this.widthA = widthA;
    //    this.widthB = widthB;
    //    this.d0 = d0;
    //    this.d1 = d1;
    //    this.side = side;
    //    this.color = color;
    //    this.lineTypeId = lineTypeId;
    //}

    //Vector2 A;        // start point (NDC or world)
    //Vector2 B;        // end point
    //float widthA;
    //float widthB;
    //float d0;      // cumulative start distance
    //float d1;      // cumulative end distance
    //float side;    // -1 or +1 for quad expansion
    //uint color;
    //int lineTypeId;
    //}
}
