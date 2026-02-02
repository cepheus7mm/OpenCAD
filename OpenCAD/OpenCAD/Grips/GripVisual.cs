using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips
{
    public readonly struct GripVisual
    {
        public Vector2 WorldPosition { get; }
        public GripStyle Style { get; }

        public GripVisual(Vector2 worldPosition, GripStyle style)
        {
            WorldPosition = worldPosition;
            Style = style;
        }
    }
}
