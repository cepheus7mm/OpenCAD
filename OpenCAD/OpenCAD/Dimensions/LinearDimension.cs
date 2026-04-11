using OpenCAD.Geometry;
using OpenCAD.Grips;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Dimensions
{
    public sealed class LinearDimension : DimensionBase
    {
        // -----------------------------
        // Semantic Data
        // -----------------------------
        public EntityReference Ref1 => Definition.Ref1;
        public EntityReference Ref2 => Definition.Ref2;
        public float Offset => Definition.Offset;

        // -----------------------------
        // Construction
        // -----------------------------
        public LinearDimension(DimensionDefinition definition)
            : base(definition) { }

        // -----------------------------
        // Measurement
        // -----------------------------
        public override MeasurementResult ComputeMeasurement()
        {
            var p1 = Ref1.ResolvePoint(Document);
            var p2 = Ref2.ResolvePoint(Document);

            var delta = p2 - p1;
            double value = Math.Abs(Vector2.Dot(delta, Direction));

            return new MeasurementResult(value, OpenCADDocument.UnitFormatType.Linear);
        }

        // -----------------------------
        // Rendering
        // -----------------------------
        public override IEnumerable<IDrawable> GenerateGeometry()
        {
            // 1. Resolve points
            var p1 = Ref1.ResolvePoint(Document);
            var p2 = Ref2.ResolvePoint(Document);

            // 2. Compute projection
            // Scalar projections onto dimension direction
            float s1 = Vector2.Dot(p1, Direction);
            float s2 = Vector2.Dot(p2, Direction);

            // Foot points on the dimension axis
            Vector2 f1 = s1 * Direction;
            Vector2 f2 = s2 * Direction;

            // 3. Compute dimension line
            Vector2 d1 = f1 + Offset * Normal;
            Vector2 d2 = f2 + Offset * Normal;
            yield return LineDrawable(d1, d2);

            // 4. Compute extension lines
            Vector2 ext1Start = p1 + Normal * Style.ScaledExtensionOffset;
            Vector2 ext2Start = p2 + Normal * Style.ScaledExtensionOffset;
            Vector2 ext1End = d1 + Normal * Style.ScaledExtensionBeyond;
            Vector2 ext2End = d2 + Normal * Style.ScaledExtensionBeyond;
            yield return LineDrawable(ext1Start, ext1End);
            yield return LineDrawable(ext2Start, ext2End);

            // 5. Compute arrows
            Arrowhead arrow1 = ComputeArrowhead(d1, Direction, Normal, Style.ScaledArrowSize);
            Arrowhead arrow2 = ComputeArrowhead(d2, -Direction, Normal, Style.ScaledArrowSize);
            foreach (var drawable in arrow1.GenerateDrawables(Color, _document))
                yield return drawable;
            foreach (var drawable in arrow2.GenerateDrawables(Color, _document))
                yield return drawable;

            // 6. Compute text
            Vector2 textPos = 0.5f * (d1 + d2);
            float angle = MathF.Atan2(Direction.Y, Direction.X);
            yield return TextDrawable(textPos, angle);
            // 7. Return drawables
        }


        // -----------------------------
        // Grips
        // -----------------------------
        public override IEnumerable<Grip> GetGrips()
        {
            // Text grip
            // Dimension line grip
            // Extension grips
            throw new NotImplementedException();
        }

        public override DimensionBase ApplyGripDelta(Grip grip, Vector2 delta)
        {
            // Move text
            // Move dimension line
            // Move extension points
            throw new NotImplementedException();
        }

        // -----------------------------
        // Associativity
        // -----------------------------
        public override DimensionBase OnReferencedGeometryChanged()
        {
            // Recompute measurement
            // Recompute view
            return this;
        }

        // -----------------------------
        // Immutability
        // -----------------------------
        protected override DimensionBase With( DimensionDefinition definition)
        {
            return new LinearDimension(definition);
        }
    }
}
