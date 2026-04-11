using OpenCAD.Styles;
using System.Numerics;

namespace OpenCAD.Dimensions
{
    public sealed class DimensionDefinition
    {
        public EntityReference Ref1 { get; init; }
        public EntityReference Ref2 { get; init; }
        public Vector2 Direction { get; init; }
        public Vector2 Normal { get; init; }
        public float Offset { get; init; }
        public OpenCADDimensionStyle Style { get; init; }
        public DimensionUnits Units { get; init; }
        public Vector2? UserTextPosition { get; init; }
        public bool IsTextFlipped { get; init; }
        public bool IsDragged { get; init; }
        public OpenCADDocument? Document { get; init; }

    }
}