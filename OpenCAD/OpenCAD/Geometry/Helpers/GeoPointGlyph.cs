using System;
using System.Drawing;
using OpenCAD.Geometry;

namespace OpenCAD.Geometry.Helpers
{
    /// <summary>
    /// A visual marker (glyph) for a GeoPoint snap location.
    /// This is a container object that holds child geometry (Lines, Arcs) to form the visual shape.
    /// The glyph itself is not drawable - only its children are rendered.
    /// </summary>
    public class GeoPointGlyph : OpenCADObject
    {
        private const double DEFAULT_GLYPH_SIZE_PIXELS = 12.0; // Default size in pixels
        private double _halfSize;

        /// <summary>
        /// Gets the GeoPoint this glyph represents
        /// </summary>
        public GeoPoint GeoPoint { get; }
        
        /// <summary>
        /// Gets the size of the glyph in world units
        /// </summary>
        public double Size { get; }
        
        /// <summary>
        /// Creates a new GeoPointGlyph for the specified point and scale
        /// </summary>
        /// <param name="geoPoint">The GeoPoint to visualize</param>
        /// <param name="scaleFactor">World units per pixel (for sizing)</param>
        /// <param name="document">The document this glyph belongs to (optional)</param>
        public GeoPointGlyph(GeoPoint geoPoint, double scaleFactor, OpenCADDocument? document = null) 
            : base(document)
        {
            GeoPoint = geoPoint ?? throw new ArgumentNullException(nameof(geoPoint));
            Size = DEFAULT_GLYPH_SIZE_PIXELS * scaleFactor;
            _halfSize = Size / 2.0;

            BuildGeometry();
        }
        
        /// <summary>
        /// Builds the child geometry based on the GeoPoint type
        /// </summary>
        private void BuildGeometry()
        {
            // Get color based on GeoPoint mode
            var glyphColor = Color.Yellow;// GetColorForMode(GeoPoint.PointType);
            
            // Build geometry based on point type
            switch (GeoPoint.PointType)
            {
                case GeoPointModes.Vertex:
                    BuildSquare(glyphColor);
                    break;
                    
                case GeoPointModes.Middle:
                    BuildTriangle(glyphColor);
                    break;
                    
                case GeoPointModes.Center:
                    BuildCircle(glyphColor);
                    break;
                    
                case GeoPointModes.Point:
                    BuildXMark(glyphColor);
                    break;
                    
                case GeoPointModes.Quadrant:
                    BuildDiamond(glyphColor);
                    break;
                    
                case GeoPointModes.Intersection:
                    BuildPlusSign(glyphColor);
                    break;
                    
                case GeoPointModes.Anchor:
                    BuildAnchor(glyphColor);
                    break;
                    
                case GeoPointModes.Perpendicular:
                    BuildPerpendicular(glyphColor);
                    break;
                    
                case GeoPointModes.Tangent:
                    BuildTangent(glyphColor);
                    break;
                    
                case GeoPointModes.NearestPoint:
                    BuildDot(glyphColor);
                    break;
                    
                case GeoPointModes.ApparentCrossing:
                    BuildHourglass(glyphColor);
                    break;
                    
                case GeoPointModes.ExtensionSnap:
                    BuildExtension(glyphColor);
                    break;
                    
                default:
                    // Default: small circle
                    BuildCircle(glyphColor);
                    break;
            }
        }
        
        /// <summary>
        /// Gets the color for a specific GeoPoint mode
        /// </summary>
        private Color GetColorForMode(GeoPointModes mode)
        {
            return mode switch
            {
                GeoPointModes.Vertex => Color.Cyan,
                GeoPointModes.Middle => Color.Yellow,
                GeoPointModes.Center => Color.Magenta,
                GeoPointModes.Point => Color.Orange,
                GeoPointModes.Quadrant => Color.LightGreen,
                GeoPointModes.Intersection => Color.White,
                GeoPointModes.Anchor => Color.LightBlue,
                GeoPointModes.Perpendicular => Color.Red,
                GeoPointModes.Tangent => Color.Pink,
                GeoPointModes.NearestPoint => Color.LightYellow,
                GeoPointModes.ApparentCrossing => Color.Violet,
                GeoPointModes.ExtensionSnap => Color.LightGray,
                _ => Color.White
            };
        }
        
        #region Geometry Builders
        
        /// <summary>
        /// Builds a square glyph (for Vertex)
        /// </summary>
        private void BuildSquare(Color color)
        {
            var center = GeoPoint;
            
            // Four corners
            var bottomLeft = new Point3D(center.Position.X - _halfSize, center.Position.Y - _halfSize, 0);
            var bottomRight = new Point3D(center.Position.X + _halfSize, center.Position.Y - _halfSize, 0);
            var topRight = new Point3D(center.Position.X + _halfSize, center.Position.Y + _halfSize, 0);
            var topLeft = new Point3D(center.Position.X - _halfSize, center.Position.Y + _halfSize, 0);
            
            // Four lines
            AddLine(bottomLeft, bottomRight, color);
            AddLine(bottomRight, topRight, color);
            AddLine(topRight, topLeft, color);
            AddLine(topLeft, bottomLeft, color);
        }

        /// <summary>
        /// Builds a triangle glyph (for Middle) with height equal to Size and centroid at GeoPoint
        /// </summary>
        private void BuildTriangle(Color color)
        {
            var center = GeoPoint;

            double height = Size;
            double side = (2.0 / Math.Sqrt(3.0)) * height; // ≈ 1.1547 * height
            double halfBase = side / 2.0;

            // The centroid divides the height in a 2:1 ratio from top to base
            double centroidToTop = (2.0 / 3.0) * height;
            double centroidToBase = (1.0 / 3.0) * height;

            // Top vertex (pointing up)
            var top = new Point3D(center.Position.X, center.Position.Y + centroidToTop, 0);

            // Bottom vertices
            var bottomLeft = new Point3D(center.Position.X - halfBase, center.Position.Y - centroidToBase, 0);
            var bottomRight = new Point3D(center.Position.X + halfBase, center.Position.Y - centroidToBase, 0);

            AddLine(top, bottomLeft, color);
            AddLine(bottomLeft, bottomRight, color);
            AddLine(bottomRight, top, color);
        }

        /// <summary>
        /// Builds a circle glyph (for Center)
        /// </summary>
        private void BuildCircle(Color color)
        {
            // Create a full circle arc (0 to 2π)
            var arc = new Circle(GeoPoint.Position, _halfSize, Document);
            arc.Color = color;
            arc.LineWeight = LineWeight.LineWeight015;
            Add(arc);
        }
        
        /// <summary>
        /// Builds an X-mark glyph (for Point)
        /// </summary>
        private void BuildXMark(Color color)
        {
            var center = GeoPoint;
            
            // Diagonal lines forming X
            var topLeft = new Point3D(center.Position.X - _halfSize, center.Position.Y + _halfSize, 0);
            var topRight = new Point3D(center.Position.X + _halfSize, center.Position.Y + _halfSize, 0);
            var bottomLeft = new Point3D(center.Position.X - _halfSize, center.Position.Y - _halfSize, 0);
            var bottomRight = new Point3D(center.Position.X + _halfSize, center.Position.Y - _halfSize, 0);
            
            AddLine(topLeft, bottomRight, color);
            AddLine(bottomLeft, topRight, color);
        }
        
        /// <summary>
        /// Builds a diamond glyph (for Quadrant)
        /// </summary>
        private void BuildDiamond(Color color)
        {
            var center = GeoPoint;
            
            // Four corners - rotated square
            var top = new Point3D(center.Position.X, center.Position.Y + _halfSize, 0);
            var right = new Point3D(center.Position.X + _halfSize, center.Position.Y, 0);
            var bottom = new Point3D(center.Position.X, center.Position.Y - _halfSize, 0);
            var left = new Point3D(center.Position.X - _halfSize, center.Position.Y, 0);
            
            AddLine(top, right, color);
            AddLine(right, bottom, color);
            AddLine(bottom, left, color);
            AddLine(left, top, color);
        }
        
        /// <summary>
        /// Builds a plus sign glyph (for Crossing)
        /// </summary>
        private void BuildPlusSign(Color color)
        {
            var center = GeoPoint;
            
            // Horizontal line
            var left = new Point3D(center.Position.X - _halfSize, center.Position.Y, 0);
            var right = new Point3D(center.Position.X + _halfSize, center.Position.Y, 0);
            AddLine(left, right, color);
            
            // Vertical line
            var top = new Point3D(center.Position.X, center.Position.Y + _halfSize, 0);
            var bottom = new Point3D(center.Position.X, center.Position.Y - _halfSize, 0);
            AddLine(top, bottom, color);
        }
        
        /// <summary>
        /// Builds an anchor glyph (for Anchor)
        /// </summary>
        private void BuildAnchor(Color color)
        {
            // Upward-pointing triangle with base
            var center = GeoPoint;
            
            var top = new Point3D(center.Position.X, center.Position.Y + _halfSize, 0);
            var bottomLeft = new Point3D(center.Position.X - _halfSize * 0.7, center.Position.Y - _halfSize * 0.3, 0);
            var bottomRight = new Point3D(center.Position.X + _halfSize * 0.7, center.Position.Y - _halfSize * 0.3, 0);
            
            AddLine(top, bottomLeft, color);
            AddLine(top, bottomRight, color);
            
            // Base line
            var baseLeft = new Point3D(center.Position.X - _halfSize, center.Position.Y - _halfSize, 0);
            var baseRight = new Point3D(center.Position.X + _halfSize, center.Position.Y - _halfSize, 0);
            AddLine(baseLeft, baseRight, color);
        }

        /// <summary>
        /// Builds a perpendicular glyph (for Perpendicular) with right angle legs of length Size,
        /// a small square of side _halfSize, and the center at the intersection of the right angle's bisectors
        /// (coincident with the upper right corner of the small square).
        /// </summary>
        private void BuildPerpendicular(Color color)
        {
            var center = GeoPoint;

            // Right angle legs: from center, one left, one down, both of length Size
            var rightAngleEndLeft = new Point3D(center.Position.X - _halfSize, center.Position.Y + _halfSize, 0);
            var rightAngleEndDown = new Point3D(center.Position.X - _halfSize, center.Position.Y - _halfSize, 0);
            var rightAngleEndRight = new Point3D(center.Position.X + _halfSize, center.Position.Y - _halfSize, 0);

            AddLine(rightAngleEndLeft, rightAngleEndDown, color);
            AddLine(rightAngleEndDown, rightAngleEndRight, color);

            // Small square: upper right corner at center, side = _halfSize
            //var sq1 = new Point3D(center.Position.X, center.Position.Y, 0); // upper right (at center)
            var sq2 = new Point3D(center.Position.X - _halfSize, center.Position.Y, 0); // upper left
            var sq3 = new Point3D(center.Position.X, center.Position.Y - _halfSize, 0); // lower left
            //var sq4 = new Point3D(center.Position.X, center.Position.Y - _halfSize, 0); // lower right

            AddLine(center.Position, sq2, color);
            AddLine(center.Position, sq3, color);
        }

        /// <summary>
        /// Builds a tangent glyph (for Tangent)
        /// </summary>
        private void BuildTangent(Color color)
        {
            double radius = Size * 0.4;
            var center = GeoPoint;
            
            // Small circle
            var arc = new Circle(center.Position, radius, Document);
            arc.Color = color;
            arc.LineWeight = LineWeight.LineWeight015;
            Add(arc);
            
            // Tangent line
            var lineStart = new Point3D(center.Position.X + radius, center.Position.Y, 0);
            var lineEnd = new Point3D(center.Position.X + radius, center.Position.Y + Size, 0);
            AddLine(lineStart, lineEnd, color);
        }
        
        /// <summary>
        /// Builds a small dot glyph (for NearestPoint)
        /// </summary>
        private void BuildDot(Color color)
        {
            double radius = Size * 0.25;
            
            var arc = new Circle(GeoPoint.Position, radius, Document);
            arc.Color = color;
            arc.LineWeight = LineWeight.LineWeight015;
            Add(arc);
        }
        
        /// <summary>
        /// Builds an hourglass glyph (for ApparentCrossing)
        /// </summary>
        private void BuildHourglass(Color color)
        {
            var center = GeoPoint;
            
            // Two triangles forming hourglass
            var topLeft = new Point3D(center.Position.X - _halfSize, center.Position.Y + _halfSize, 0);
            var topRight = new Point3D(center.Position.X + _halfSize, center.Position.Y + _halfSize, 0);
            var bottomLeft = new Point3D(center.Position.X - _halfSize, center.Position.Y - _halfSize, 0);
            var bottomRight = new Point3D(center.Position.X + _halfSize, center.Position.Y - _halfSize, 0);
            
            // Top triangle
            AddLine(topLeft, center.Position, color);
            AddLine(topRight, center.Position, color);
            AddLine(topLeft, topRight, color);
            
            // Bottom triangle
            AddLine(bottomLeft, center.Position, color);
            AddLine(bottomRight, center.Position, color);
            AddLine(bottomLeft, bottomRight, color);
        }
        
        /// <summary>
        /// Builds an extension snap glyph (for ExtensionSnap)
        /// </summary>
        private void BuildExtension(Color color)
        {
            var center = GeoPoint;
            
            // Dashed extension line (small segments)
            double segmentLength = Size * 0.15;
            double gap = Size * 0.1;
            
            var start = new Point3D(center.Position.X - _halfSize, center.Position.Y, 0);
            
            for (int i = 0; i < 3; i++)
            {
                var segStart = new Point3D(start.X + i * (segmentLength + gap), start.Y, 0);
                var segEnd = new Point3D(segStart.X + segmentLength, segStart.Y, 0);
                AddLine(segStart, segEnd, color);
            }
            
            // Small cross at center
            double crossSize = Size * 0.2;
            AddLine(
                new Point3D(center.Position.X - crossSize, center.Position.Y, 0),
                new Point3D(center.Position.X + crossSize, center.Position.Y, 0),
                color);
            AddLine(
                new Point3D(center.Position.X, center.Position.Y - crossSize, 0),
                new Point3D(center.Position.X, center.Position.Y + crossSize, 0),
                color);
        }
        
        /// <summary>
        /// Helper method to add a line to the glyph
        /// </summary>
        private void AddLine(Point3D start, Point3D end, Color color)
        {
            var line = new Line(Document, start, end);
            line.Color = color;
            line.LineWeight = LineWeight.LineWeight015;
            Add(line);
        }
        
        #endregion
    }
}