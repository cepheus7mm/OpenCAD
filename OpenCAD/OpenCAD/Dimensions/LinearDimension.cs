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

            Vector2 dir = Definition.DimensionType == DimensionTypeLinear.Aligned
                ? Vector2.Normalize(delta)
                : Direction;

            double value = Math.Abs(Vector2.Dot(delta, dir));

            return new MeasurementResult(value, OpenCADDocument.UnitFormatType.Linear);
        }

        // -----------------------------
        // Rendering
        // -----------------------------
        public override IEnumerable<IDrawable> GenerateGeometry()
        {
            // 1. Resolve extension line origins
            var p1 = Ref1.ResolvePoint(Document);
            var p2 = Ref2.ResolvePoint(Document);

            // For Aligned type, recompute direction from resolved points
            // so the dimension stays aligned when the entity is dragged.
            Vector2 dir = Direction;
            Vector2 nrm = Normal;
            if (Definition.DimensionType == DimensionTypeLinear.Aligned)
            {
                var delta = p2 - p1;
                if (delta.LengthSquared() > 1e-12f)
                {
                    dir = Vector2.Normalize(delta);
                    nrm = new Vector2(-dir.Y, dir.X);
                }
            }

            // 2. Compute dimension line endpoints
            //    Offset is a signed distance along Normal.
            Vector2 d1 = p1 + Offset * nrm;
            Vector2 d2 = p2 + Offset * nrm;

            // Project d2 onto the dimension direction passing through d1
            // so the dimension line is always parallel to Direction.
            float projectedLength = Vector2.Dot(p2 - p1, dir);
            d2 = d1 + projectedLength * dir;

            yield return LineDrawable(d1, d2);

            // 3. Extension lines
            float offsetSign = MathF.Sign(Offset);
            Vector2 extDir = offsetSign * nrm;

            Vector2 ext1Start = p1 + extDir * Style.ScaledExtensionOffset;
            Vector2 ext2Start = p2 + extDir * Style.ScaledExtensionOffset;

            Vector2 ext1End = d1 + extDir * Style.ScaledExtensionBeyond;
            Vector2 ext2End = d2 + extDir * Style.ScaledExtensionBeyond;

            yield return LineDrawable(ext1Start, ext1End);
            yield return LineDrawable(ext2Start, ext2End);

            // 4. Arrowheads
            //    Arrows must point inward along the dimension line (from each end toward the other).
            Vector2 arrowDir1 = Vector2.Normalize(d2 - d1);   // d1 arrow points toward d2
            Vector2 arrowDir2 = Vector2.Normalize(d1 - d2);   // d2 arrow points toward d1

            Arrowhead arrow1 = ComputeArrowhead(d1, arrowDir1, nrm, Style.ScaledArrowSize);
            Arrowhead arrow2 = ComputeArrowhead(d2, arrowDir2, nrm, Style.ScaledArrowSize);
            foreach (var drawable in arrow1.GenerateDrawables(Color, _document))
                yield return drawable;
            foreach (var drawable in arrow2.GenerateDrawables(Color, _document))
                yield return drawable;

            // 5. Text
            Vector2 textPos = 0.5f * (d1 + d2);
            yield return TextDrawable(textPos, arrowDir1);
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
