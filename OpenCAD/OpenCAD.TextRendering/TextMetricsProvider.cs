using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAD.TextRendering
{
    /// <summary>
    /// Concrete implementation of ITextMetricsProvider using IFontProvider internally.
    /// Includes caching and improved curve tessellation for high-quality glyph rendering.
    /// </summary>
    public class TextMetricsProvider : ITextMetricsProvider
    {
        private readonly IFontProvider _fontProvider;
        
        // Cache for tessellated glyphs: Key = (char, fontFamily, isBold, isItalic, quantizedSize)
        // Value = List of tessellated contours (each contour is a list of 2D points)
        private readonly Dictionary<TessellationCacheKey, List<List<Point2D>>> _tessellationCache = new();
        
        // Tolerance coefficient: smaller = smoother curves but more points
        // 0.01 = 1% of font size deviation allowed
        private const double ToleranceCoefficient = 0.02;

        public TextMetricsProvider(IFontProvider fontProvider)
        {
            _fontProvider = fontProvider ?? throw new ArgumentNullException(nameof(fontProvider));
        }

        public TextBounds GetTextBounds(string text, FontDescriptor font, double size)
        {
            if (string.IsNullOrEmpty(text))
                return new TextBounds { MinX = 0, MinY = 0, MaxX = 0, MaxY = 0 };

            var parser = _fontProvider.GetFont(font.FamilyName, font.IsBold, font.IsItalic);
            
            double scale = CalculateScale(parser, size);
            double penX = 0;
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (char c in text)
            {
                var glyph = parser.GetGlyph(c);
                
                // Scale glyph bounds to world units
                double glyphMinX = penX + glyph.Bounds.XMin * scale;
                double glyphMinY = glyph.Bounds.YMin * scale;
                double glyphMaxX = penX + glyph.Bounds.XMax * scale;
                double glyphMaxY = glyph.Bounds.YMax * scale;

                minX = Math.Min(minX, glyphMinX);
                minY = Math.Min(minY, glyphMinY);
                maxX = Math.Max(maxX, glyphMaxX);
                maxY = Math.Max(maxY, glyphMaxY);

                penX += glyph.AdvanceWidth * scale;
            }

            return new TextBounds
            {
                MinX = minX,
                MinY = minY,
                MaxX = maxX,
                MaxY = maxY
            };
        }

        public GlyphMetrics GetGlyphMetrics(char c, FontDescriptor font, double size)
        {
            var parser = _fontProvider.GetFont(font.FamilyName, font.IsBold, font.IsItalic);
            var glyph = parser.GetGlyph(c);
            double scale = CalculateScale(parser, size);

            return new GlyphMetrics
            {
                AdvanceWidth = glyph.AdvanceWidth * scale,
                LeftSideBearing = glyph.LeftSideBearing * scale,
                BoundingBoxMinX = glyph.Bounds.XMin * scale,
                BoundingBoxMinY = glyph.Bounds.YMin * scale,
                BoundingBoxMaxX = glyph.Bounds.XMax * scale,
                BoundingBoxMaxY = glyph.Bounds.YMax * scale
            };
        }

        public VectorizedGlyph GetGlyphOutlines(char c, FontDescriptor font, double size)
        {
            var parser = _fontProvider.GetFont(font.FamilyName, font.IsBold, font.IsItalic);
            var glyph = parser.GetGlyph(c);
            
            // Scale the glyph to the requested size
            double scale = CalculateScale(parser, size);
            
            // Get tessellated contours from cache or compute them
            double quantizedSize = QuantizeFontSize(size);
            var tessellatedContours = GetTessellatedContours(c, font, quantizedSize, parser, scale);
            
            // Build a VectorizedGlyph with the tessellated geometry
            return BuildGlyphWithTessellatedContours(glyph, tessellatedContours, scale);
        }

        public IEnumerable<PositionedGlyph> GetTextOutlines(string text, FontDescriptor font, double size)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            var parser = _fontProvider.GetFont(font.FamilyName, font.IsBold, font.IsItalic);
            double scale = CalculateScale(parser, size);
            double quantizedSize = QuantizeFontSize(size);
            double penX = 0;

            foreach (char c in text)
            {
                var glyph = parser.GetGlyph(c);
                
                // Get tessellated contours from cache or compute them
                var tessellatedContours = GetTessellatedContours(c, font, quantizedSize, parser, scale);
                
                // Build glyph with pre-tessellated geometry
                var scaledGlyph = BuildGlyphWithTessellatedContours(glyph, tessellatedContours, scale);

                yield return new PositionedGlyph
                {
                    Glyph = scaledGlyph,
                    X = penX,
                    Y = 0,
                    Character = c
                };

                penX += glyph.AdvanceWidth * scale;
            }
        }

        public FontMetrics GetFontMetrics(FontDescriptor font, double size)
        {
            var parser = _fontProvider.GetFont(font.FamilyName, font.IsBold, font.IsItalic);
            double scale = CalculateScale(parser, size);

            return new FontMetrics
            {
                Ascender = parser.Ascender * scale,
                Descender = parser.Descender * scale,
                LineHeight = (parser.Ascender - parser.Descender) * scale,
                CapHeight = parser.CapHeight * scale,
                XHeight = parser.XHeight * scale,
                UnitsPerEm = parser.UnitsPerEm
            };
        }

        /// <summary>
        /// Quantize font size to nearest 0.5 to improve cache hit rate.
        /// This means sizes 4.1, 4.2, 4.3 all use the same cached geometry.
        /// </summary>
        private double QuantizeFontSize(double size)
        {
            return Math.Round(size * 2.0) / 2.0;
        }

        /// <summary>
        /// Get tessellated contours for a glyph, using cache if available.
        /// </summary>
        private List<List<Point2D>> GetTessellatedContours(
            char character, 
            FontDescriptor font, 
            double quantizedSize,
            FontParser parser,
            double scale)
        {
            var cacheKey = new TessellationCacheKey(character, font.FamilyName, font.IsBold, font.IsItalic, quantizedSize);
            
            if (_tessellationCache.TryGetValue(cacheKey, out var cachedContours))
            {
                return cachedContours;
            }

            // Cache miss - tessellate the glyph
            var glyph = parser.GetGlyph(character);
            
            // Calculate tolerance in WORLD UNITS for consistent quality
            var toleranceInWorldUnits = quantizedSize * ToleranceCoefficient;
            
            var tessellatedContours = new List<List<Point2D>>();
            
            foreach (var contour in glyph.Contours)
            {
                var tessellatedContour = TessellateContour(contour, scale, toleranceInWorldUnits);
                tessellatedContours.Add(tessellatedContour);
            }
            
            // Store in cache
            _tessellationCache[cacheKey] = tessellatedContours;
            
            var totalPoints = tessellatedContours.Sum(c => c.Count);
            var avgPointsPerContour = tessellatedContours.Count > 0 ? totalPoints / tessellatedContours.Count : 0;
            
            System.Diagnostics.Debug.WriteLine($"[TextMetricsProvider] Cached '{character}' size={quantizedSize:F1}: {tessellatedContours.Count} contours, {totalPoints} total points (avg {avgPointsPerContour} pts/contour), tolerance={toleranceInWorldUnits:F4} world units");
            
            return tessellatedContours;
        }

        /// <summary>
        /// Tessellate a single contour, converting Bézier curves to line segments.
        /// FIXED: Now properly handles chains of consecutive off-curve control points without infinite loop.
        /// </summary>
        private List<Point2D> TessellateContour(GlyphContour contour, double scale, double toleranceInWorldUnits)
        {
            var result = new List<Point2D>();
            
            if (contour.Points.Count < 2)
                return result;

            double toleranceInFontUnits = toleranceInWorldUnits / scale;
            int curveSegmentCount = 0;
            int straightSegmentCount = 0;

            for (int i = 0; i < contour.Points.Count; i++)
            {
                var current = contour.Points[i];
                
                if (!current.OnCurve)
                {
                    // Skip off-curve points - they're processed as part of chains
                    continue;
                }
                
                // Add the on-curve point
                result.Add(new Point2D(current.X, current.Y));
                
                // Look ahead to see if we have curve segments
                int nextIdx = (i + 1) % contour.Points.Count;
                var next = contour.Points[nextIdx];
                
                if (!next.OnCurve)
                {
                    // We have a sequence of off-curve points - process them as a chain
                    var offCurvePoints = new List<GlyphPoint>();
                    int j = nextIdx;
                    
                    // Collect all consecutive off-curve points
                    int safetyCounter = 0;
                    while (!contour.Points[j].OnCurve && safetyCounter < contour.Points.Count)
                    {
                        offCurvePoints.Add(contour.Points[j]);
                        j = (j + 1) % contour.Points.Count;
                        safetyCounter++;
                    }
                    
                    // j now points to the next on-curve point
                    var endPoint = contour.Points[j];
                    
                    // Process the chain of curves
                    ProcessOffCurveChain(result, current, offCurvePoints, endPoint, toleranceInFontUnits, ref curveSegmentCount);
                    
                    // Skip past the off-curve points we just processed
                    // The loop will continue from i+1, and we'll skip the off-curve points
                    // because of the continue statement above
                }
                else
                {
                    straightSegmentCount++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"  Tessellation summary: {curveSegmentCount} curve segments, {straightSegmentCount} straight segments, {result.Count} total points");

            // Scale all points from font units to world units
            for (int idx = 0; idx < result.Count; idx++)
            {
                result[idx] = new Point2D(result[idx].X * scale, result[idx].Y * scale);
            }

            return result;
        }

        /// <summary>
        /// Process a chain of off-curve control points between two on-curve points.
        /// Creates multiple quadratic Bézier curves with implied on-curve points between each pair of controls.
        /// MODIFIED: Add exactly one midpoint per curve for minimal smoothing.
        /// </summary>
        private void ProcessOffCurveChain(
            List<Point2D> result,
            GlyphPoint startOnCurve,
            List<GlyphPoint> offCurvePoints,
            GlyphPoint endOnCurve,
            double toleranceInFontUnits,
            ref int curveSegmentCount)
        {
            if (offCurvePoints.Count == 0)
                return;
            
            Point2D currentStart = new Point2D(startOnCurve.X, startOnCurve.Y);
            
            for (int i = 0; i < offCurvePoints.Count; i++)
            {
                var control = offCurvePoints[i];
                Point2D controlPoint = new Point2D(control.X, control.Y);
                Point2D endPoint;
                
                if (i == offCurvePoints.Count - 1)
                {
                    // Last control point - curve ends at the next on-curve point
                    endPoint = new Point2D(endOnCurve.X, endOnCurve.Y);
                }
                else
                {
                    // Implied on-curve point between this control and the next
                    var nextControl = offCurvePoints[i + 1];
                    endPoint = new Point2D(
                        (control.X + nextControl.X) / 2.0,
                        (control.Y + nextControl.Y) / 2.0
                    );
                }
                
                curveSegmentCount++;
                
                // Add exactly ONE midpoint at t=0.5 for minimal smoothing
                double t = 0.5;
                double oneMinusT = 0.5;
                double b0 = 0.25;  // (0.5)²
                double b1 = 0.5;   // 2 × 0.5 × 0.5
                double b2 = 0.25;  // (0.5)²
                
                double midX = b0 * currentStart.X + b1 * controlPoint.X + b2 * endPoint.X;
                double midY = b0 * currentStart.Y + b1 * controlPoint.Y + b2 * endPoint.Y;
                
                result.Add(new Point2D(midX, midY));
                
                // REMOVED: Don't add endpoint here - it will be added by the main loop
                // The endpoint is either:
                // 1. An implied midpoint (will be used as start of next curve in chain)
                // 2. The next actual on-curve point (will be added by TessellateContour loop)
                
                // Next curve starts where this one ended
                currentStart = endPoint;
            }
        }

        /// <summary>
        /// Tessellate a quadratic Bézier curve using fixed parameter steps.
        /// </summary>
        private List<Point2D> TessellateQuadraticBezier(Point2D p0, Point2D p1, Point2D p2, double toleranceInFontUnits)
        {
            var result = new List<Point2D>();
            
            double arcLength = Distance(p0, p1) + Distance(p1, p2);
            int steps = Math.Max(2, (int)Math.Ceiling(arcLength / toleranceInFontUnits));
            steps = Math.Min(steps, 50);
            
            for (int i = 1; i <= steps; i++)
            {
                double t = i / (double)steps;
                double oneMinusT = 1.0 - t;
                double b0 = oneMinusT * oneMinusT;
                double b1 = 2.0 * oneMinusT * t;
                double b2 = t * t;
                
                double x = b0 * p0.X + b1 * p1.X + b2 * p2.X;
                double y = b0 * p0.Y + b1 * p1.Y + b2 * p2.Y;
                
                result.Add(new Point2D(x, y));
            }
            
            return result;
        }

        private double Distance(Point2D a, Point2D b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private VectorizedGlyph BuildGlyphWithTessellatedContours(
            VectorizedGlyph originalGlyph,
            List<List<Point2D>> tessellatedContours,
            double scale)
        {
            var result = new VectorizedGlyph
            {
                Character = originalGlyph.Character,
                AdvanceWidth = originalGlyph.AdvanceWidth * scale,
                LeftSideBearing = originalGlyph.LeftSideBearing * scale,
                Bounds = new GlyphBounds
                {
                    XMin = originalGlyph.Bounds.XMin * scale,
                    YMin = originalGlyph.Bounds.YMin * scale,
                    XMax = originalGlyph.Bounds.XMax * scale,
                    YMax = originalGlyph.Bounds.YMax * scale
                },
                Contours = new List<GlyphContour>()
            };

            foreach (var tessellatedContour in tessellatedContours)
            {
                var glyphContour = new GlyphContour();
                foreach (var point in tessellatedContour)
                {
                    glyphContour.Points.Add(new GlyphPoint(point.X, point.Y, onCurve: true));
                }
                result.Contours.Add(glyphContour);
            }

            return result;
        }

        private double CalculateScale(FontParser parser, double fontSize)
        {
            double unitsHeight = parser.CapHeight;
            if (unitsHeight == 0)
            {
                unitsHeight = parser.UnitsPerEm;
            }
            return fontSize / unitsHeight;
        }

        private struct Point2D
        {
            public double X;
            public double Y;

            public Point2D(double x, double y)
            {
                X = x;
                Y = y;
            }
        }

        private struct TessellationCacheKey : IEquatable<TessellationCacheKey>
        {
            public char Character;
            public string FontFamily;
            public bool IsBold;
            public bool IsItalic;
            public double QuantizedSize;

            public TessellationCacheKey(char character, string fontFamily, bool isBold, bool isItalic, double quantizedSize)
            {
                Character = character;
                FontFamily = fontFamily;
                IsBold = isBold;
                IsItalic = isItalic;
                QuantizedSize = quantizedSize;
            }

            public bool Equals(TessellationCacheKey other)
            {
                return Character == other.Character &&
                       FontFamily == other.FontFamily &&
                       IsBold == other.IsBold &&
                       IsItalic == other.IsItalic &&
                       Math.Abs(QuantizedSize - other.QuantizedSize) < 0.01;
            }

            public override bool Equals(object? obj)
            {
                return obj is TessellationCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Character, FontFamily, IsBold, IsItalic, QuantizedSize);
            }
        }
    }
}
