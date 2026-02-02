using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips
{
    public readonly struct GripStyle
    {
        public Vector4 BaseColor { get; }
        public Vector4 GlowColor { get; }
        public float GlowRadius { get; }
        public float SizePx { get; }

        public GripStyle(Vector4 baseColor, Vector4 glowColor, float glowRadius, float sizePx)
        {
            BaseColor = baseColor;
            GlowColor = glowColor;
            GlowRadius = glowRadius;
            SizePx = sizePx;
        }
    }

    public static class GripStyles
    {
        public static readonly GripStyle Inactive =
            new(new(0.2f, 0.6f, 1f, 1f), new(0, 0, 0, 0), 0f, 8f);

        public static readonly GripStyle Hover =
            new(new(0.2f, 0.9f, 1f, 1f), new(0.2f, 0.9f, 1f, 1f), 6f, 8f);

        public static readonly GripStyle Active =
            new(new(1f, 0.5f, 0f, 1f), new(1f, 0.5f, 0f, 1f), 8f, 8f);

        public static readonly GripStyle Selected =
            new(new(0.2f, 0.9f, 1f, 1f), new(0f, 0f, 0f, 0f), 0f, 8f);
    }
}
