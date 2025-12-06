using System.Collections.Generic;

namespace OpenCAD.TextRendering
{
    /// <summary>
    /// Represents a glyph that has been converted to vector geometry
    /// </summary>
    public class VectorizedGlyph
    {
        /// <summary>
        /// The character this glyph represents
        /// </summary>
        public char Character { get; set; }

        /// <summary>
        /// Contours representing the glyph outline.
        /// Each contour is a list of points forming a closed shape.
        /// </summary>
        public List<GlyphContour> Contours { get; set; } = new();

        /// <summary>
        /// Horizontal advance width (distance to next character origin)
        /// </summary>
        public double AdvanceWidth { get; set; }

        /// <summary>
        /// Left side bearing (offset from origin to left edge of glyph)
        /// </summary>
        public double LeftSideBearing { get; set; }

        /// <summary>
        /// Bounding box of the glyph
        /// </summary>
        public GlyphBounds Bounds { get; set; } = new();
    }

    /// <summary>
    /// A single contour (closed path) in a glyph
    /// </summary>
    public class GlyphContour
    {
        /// <summary>
        /// Points defining the contour.
        /// Can be line segments or curve control points.
        /// </summary>
        public List<GlyphPoint> Points { get; set; } = new();
    }

    /// <summary>
    /// A point in a glyph contour
    /// </summary>
    public class GlyphPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>
        /// True if this is an on-curve point, false if it's a control point
        /// </summary>
        public bool OnCurve { get; set; }

        public GlyphPoint(double x, double y, bool onCurve = true)
        {
            X = x;
            Y = y;
            OnCurve = onCurve;
        }
    }

    /// <summary>
    /// Bounding box for a glyph
    /// </summary>
    public class GlyphBounds
    {
        public double XMin { get; set; }
        public double YMin { get; set; }
        public double XMax { get; set; }
        public double YMax { get; set; }
    }
}