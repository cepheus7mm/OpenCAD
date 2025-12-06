using System;
using System.Collections.Generic;

namespace OpenCAD.TextRendering
{
    /// <summary>
    /// High-level service for text measurement and rendering queries.
    /// Abstracts away font file parsing details.
    /// </summary>
    public interface ITextMetricsProvider
    {
        /// <summary>
        /// Get the bounding box for rendered text in world units.
        /// </summary>
        TextBounds GetTextBounds(string text, FontDescriptor font, double size);

        /// <summary>
        /// Get metrics for a single glyph.
        /// </summary>
        GlyphMetrics GetGlyphMetrics(char c, FontDescriptor font, double size);

        /// <summary>
        /// Get vectorized outline for a single glyph (for rendering).
        /// </summary>
        VectorizedGlyph GetGlyphOutlines(char c, FontDescriptor font, double size);

        /// <summary>
        /// Get vectorized outlines for all glyphs in a text string (for rendering).
        /// Returns glyphs positioned with proper advance widths.
        /// </summary>
        IEnumerable<PositionedGlyph> GetTextOutlines(string text, FontDescriptor font, double size);

        /// <summary>
        /// Get font-level metrics (ascender, descender, cap height, etc.) scaled to the given size.
        /// </summary>
        FontMetrics GetFontMetrics(FontDescriptor font, double size);
    }

    /// <summary>
    /// Bounding box for text in world units.
    /// </summary>
    public struct TextBounds
    {
        public double MinX, MinY, MaxX, MaxY;

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
    }

    /// <summary>
    /// Metrics for a single glyph.
    /// </summary>
    public struct GlyphMetrics
    {
        public double AdvanceWidth;
        public double LeftSideBearing;
        public double BoundingBoxMinX, BoundingBoxMinY;
        public double BoundingBoxMaxX, BoundingBoxMaxY;
    }

    /// <summary>
    /// Font-level typographic metrics scaled to a specific size.
    /// </summary>
    public struct FontMetrics
    {
        public double Ascender;
        public double Descender;
        public double LineHeight;
        public double CapHeight;
        public double XHeight;
        public double UnitsPerEm;
    }

    /// <summary>
    /// Describes a font by family name and style (bold, italic, etc.).
    /// </summary>
    public struct FontDescriptor
    {
        public string FamilyName { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }

        public FontDescriptor(string familyName, bool bold = false, bool italic = false)
        {
            FamilyName = familyName;
            IsBold = bold;
            IsItalic = italic;
        }

        public override string ToString() => $"{FamilyName}{(IsBold ? " Bold" : "")}{(IsItalic ? " Italic" : "")}";
    }

    /// <summary>
    /// A glyph with its world-space position (for rendering text strings).
    /// </summary>
    public struct PositionedGlyph
    {
        public VectorizedGlyph Glyph { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public char Character { get; set; }
    }
}
