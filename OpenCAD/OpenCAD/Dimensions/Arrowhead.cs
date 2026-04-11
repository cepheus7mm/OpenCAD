using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System.Drawing;
using System.Numerics;

namespace OpenCAD.Dimensions
{
    /// <summary>
    /// Describes the geometry of a single arrowhead on a dimension line.
    /// </summary>
    public readonly struct Arrowhead
    {
        public Arrowhead(Vector2 tip, Vector2 baseLeft, Vector2 baseRight, ArrowType arrowType)
        {
            Tip = tip;
            BaseLeft = baseLeft;
            BaseRight = baseRight;
            ArrowType = arrowType;
        }

        public Vector2 Tip { get; }
        public Vector2 BaseLeft { get; }
        public Vector2 BaseRight { get; }
        public ArrowType ArrowType { get; }

        /// <summary>
        /// Generates the drawable geometry for this arrowhead based on its <see cref="ArrowType"/>.
        /// </summary>
        /// <param name="fillColor">Fill color used for <see cref="ArrowType.ClosedFilled"/>.</param>
        /// <param name="doc">The document the drawables belong to.</param>
        public IDrawable[] GenerateDrawables(Color fillColor, OpenCADDocument? doc = null)
        {
            return ArrowType switch
            {
                ArrowType.ClosedFilled => GenerateClosedFilled(fillColor, doc),
                ArrowType.ClosedBlank => GenerateClosedBlank(doc),
                ArrowType.Open => GenerateOpen(doc),
                _ => Array.Empty<IDrawable>(),
            };
        }

        private IDrawable[] GenerateClosedFilled(Color fillColor, OpenCADDocument? doc)
        {
            return new IDrawable[]
            {
                new Polygon(ToTriangle(), fillColor, doc)
            };
        }

        private IDrawable[] GenerateClosedBlank(OpenCADDocument? doc)
        {
            return new IDrawable[]
            {
                new Polygon(ToTriangle(), Color.Transparent, doc)
            };
        }

        private IDrawable[] GenerateOpen(OpenCADDocument? doc)
        {
            var tip = new Point3D(Tip.X, Tip.Y, 0);
            return new IDrawable[]
            {
                new Line(tip, new Point3D(BaseLeft.X, BaseLeft.Y, 0), doc),
                new Line(tip, new Point3D(BaseRight.X, BaseRight.Y, 0), doc)
            };
        }

        private Point3D[] ToTriangle()
        {
            return new Point3D[]
            {
                new(Tip.X, Tip.Y, 0),
                new(BaseLeft.X, BaseLeft.Y, 0),
                new(BaseRight.X, BaseRight.Y, 0)
            };
        }
    }
}