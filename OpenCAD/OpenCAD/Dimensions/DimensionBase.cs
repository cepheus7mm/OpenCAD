using OpenCAD.Geometry;
using OpenCAD.Grips;
using OpenCAD.Interfaces;
using OpenCAD.Styles;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Dimensions
{
    public abstract class DimensionBase : DrawableBase, IDrawableSource, IHitTestable
    {
        protected DimensionBase(DimensionDefinition definition) : base(definition.Document)
        {
            Definition = definition;
        }

        public readonly DimensionDefinition Definition;

        public Vector2 Direction => Vector2.Normalize(Definition.Direction);
        public Vector2 Normal => Vector2.Normalize(Definition.Normal);

        // -----------------------------
        // 1. Semantic Layer
        // -----------------------------
        //public IReadOnlyList<EntityReference> References { get; }
        public abstract MeasurementResult ComputeMeasurement();

        // -----------------------------
        // 2. Style Layer
        // -----------------------------
        public OpenCADDimensionStyle Style => Definition.Style;

        // -----------------------------
        // 3. View Layer
        // -----------------------------
        public Vector2? UserTextPosition { get; }
        public bool IsTextFlipped { get; }
        public bool IsDragged { get; }

        // -----------------------------
        // 4. Rendering Layer
        // -----------------------------
        public abstract IEnumerable<IDrawable> GenerateGeometry();

        protected Arrowhead ComputeArrowhead(Vector2 tip, Vector2 direction, Vector2 normal, float arrowSize)
        {
            Vector2 baseCenter = tip + direction * arrowSize;

            Vector2 perp = new Vector2(-direction.Y, direction.X); // perpendicular
            Vector2 baseLeft = baseCenter + perp * (arrowSize * 0.5f * 0.3333f);
            Vector2 baseRight = baseCenter - perp * (arrowSize * 0.5f * 0.3333f);

            return new Arrowhead(tip, baseLeft, baseRight, Style.ArrowType);
        }

        protected virtual IDrawable LineDrawable(Vector2 d1, Vector2 d2)
        {
            return new Line(new Point3D(d1.X, d1.Y, 0), new Point3D(d2.X, d2.Y, 0), _document);
        }

        protected IDrawable TextDrawable(Vector2 textPos, Vector2 lineDirection)
        {
            float angle = MathF.Atan2(lineDirection.Y, lineDirection.X);

            // Normalize angle to [0, 2π)
            float twoPi = MathF.PI * 2f;
            angle = ((angle % twoPi) + twoPi) % twoPi;

            // Flip text if angle falls outside the readable range.
            // Readable range: [270° - overshoot, 90° + overshoot] i.e. right-side-up.
            // Flip when angle > 90° + overshoot AND angle < 270° - overshoot.
            float overshootRad = Style.TextAngleOvershoot * (MathF.PI / 180f);
            float upperLimit = MathF.PI * 0.5f + overshootRad;   // 90° + overshoot
            float lowerLimit = MathF.PI * 1.5f - overshootRad;   // 270° - overshoot

            bool flip = angle > upperLimit && angle < lowerLimit;
            if (flip)
            {
                angle -= MathF.PI; // rotate 180°
                lineDirection = -lineDirection;
            }

            var text = ComputeMeasurement().FormattedResult(_document);
            var sText = new SText(_document, text, new Point3D(textPos.X, textPos.Y, 0), angle);
            sText.FontSize = Style.ScaledTextHeight;

            float halfHeight = Style.ScaledTextHeight * 0.5f;
            float halfWidth = (float)sText.Length * 0.5f;

            // Shift back along line direction to center, shift along Normal to raise above line
            Vector2 adjustedPos = textPos
                - halfWidth * lineDirection
                + halfHeight * Normal;

            sText.BasePoint = new Point3D(adjustedPos.X, adjustedPos.Y, 0);

            return sText;
        }

        // -----------------------------
        // 5. Editing / Grips
        // -----------------------------
        public abstract IEnumerable<Grip> GetGrips();
        public abstract DimensionBase ApplyGripDelta(Grip grip, Vector2 delta);

        // -----------------------------
        // 6. Associativity
        // -----------------------------
        public abstract DimensionBase OnReferencedGeometryChanged();

        // -----------------------------
        // 7. Immutability Support
        // -----------------------------
        protected abstract DimensionBase With(DimensionDefinition definition);

        // -----------------------------
        // 8. Hit Testing
        // -----------------------------
        public HitResult HitTest(HitTestContext ctx)
        {
            foreach (var drawable in GenerateGeometry())
            {
                if (drawable is ICurve curve)
                {
                    var pt = curve.GetClosestPoint(ctx.WorldPosition);
                    if (ctx.IsInsidePickBox(pt))
                        return HitResult.FromEntity(this);
                }
                else if (drawable is SText sText)
                {
                    var pt = sText.GetClosestPointTo(ctx.WorldPosition);
                    if (ctx.IsInsidePickBox(pt))
                        return HitResult.FromEntity(this);
                }
            }

            return HitResult.None();
        }
    }
}
