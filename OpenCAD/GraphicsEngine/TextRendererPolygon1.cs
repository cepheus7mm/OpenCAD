using OpenCAD;
using OpenCAD.NonGeometric;
using OpenCAD.Geometry;
using OpenCAD.TextRendering;
using System.Numerics;
using OpenTK.Graphics.OpenGL;

namespace GraphicsEngine
{
    /// <summary>
    /// Renderer for OpenCADText objects using font parsing and vectorization
    /// </summary>
    public class TextRendererPolygon1 : IRenderer
    {
        private readonly ShaderProgram _shaderProgram;
        private readonly FontRegistry _fontRegistry;
        private readonly PolygonRenderer _polygonRenderer;

        public TextRendererPolygon1(ShaderProgram shaderProgram, FontRegistry fontRegistry)
        {
            _shaderProgram = shaderProgram;
            _fontRegistry = fontRegistry;
            _polygonRenderer = new PolygonRenderer(shaderProgram);
        }

        public bool CanRender(OpenCADObject obj)
        {
            return obj is OpenCADText;
        }

        public void Render(OpenCADObject obj, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            var context = new RenderContext
            {
                ViewMatrix = viewMatrix,
                ProjectionMatrix = projectionMatrix,
                IsHighlighted = false,
                IsSelected = false
            };
            Render(obj, context);
        }

        public void Render(OpenCADObject obj, RenderContext context)
        {
            if (obj is not OpenCADText textObj) return;

            try
            {
                // Get font parser for the text's font family and style
                var fontParser = _fontRegistry.GetFont(
                    textObj.FontFamily,
                    textObj.IsBold,
                    textObj.IsItalic
                );

                // Get insertion point and font size
                var basePoint = textObj.BasePoint;
                double fontSize = textObj.FontSize;
                double rotation = textObj.Rotation;

                // Current pen position
                double penX = basePoint.X;
                double penY = basePoint.Y;

                // Get color
                var color = GetTextColor(textObj);

                System.Diagnostics.Debug.WriteLine($"[TextRenderer] Rendering text: '{textObj.Text}' at ({penX:F3}, {penY:F3}), size={fontSize:F3}, rotation={rotation:F3}");

                // Render each character
                foreach (char c in textObj.Text)
                {
                    var glyph = fontParser.GetGlyph(c);

                    System.Diagnostics.Debug.WriteLine($"[TextRenderer] Character '{c}': {glyph.Contours.Count} contours, advance={glyph.AdvanceWidth}");

                    // Convert glyph contours to filled triangles
                    RenderGlyphFilled(glyph, penX, penY, fontSize, rotation, fontParser, color, context);

                    // Advance pen position by the glyph's advance width
                    // Scale from font units to world units
                    double advanceWorld = (glyph.AdvanceWidth / (double)fontParser.UnitsPerEm) * fontSize;
                    
                    // Apply rotation to advance vector
                    double rotRad = rotation;
                    penX += advanceWorld * Math.Cos(rotRad);
                    penY += advanceWorld * Math.Sin(rotRad);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextRenderer] Error rendering text: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[TextRenderer] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Render a single glyph as filled triangles
        /// </summary>
        private void RenderGlyphFilled(VectorizedGlyph glyph, double x, double y, double fontSize, 
            double rotation, FontParser fontParser, Vector3 color, RenderContext context)
        {
            // Calculate scaling factor from font units to world units
            double scale = fontSize / fontParser.UnitsPerEm;

            System.Diagnostics.Debug.WriteLine($"[TextRenderer] RenderGlyphFilled: scale={scale:F6}, unitsPerEm={fontParser.UnitsPerEm}");

            // Tessellate all contours into triangles
            var triangles = TessellateGlyph(glyph, x, y, scale, rotation);

            System.Diagnostics.Debug.WriteLine($"[TextRenderer] Tessellated {triangles.Count / 3} triangles");

            if (triangles.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[TextRenderer] WARNING: No triangles generated for glyph!");
                return;
            }

            // Convert to flat array
            var vertices = new float[triangles.Count * 3];
            for (int i = 0; i < triangles.Count; i++)
            {
                vertices[i * 3] = (float)triangles[i].X;
                vertices[i * 3 + 1] = (float)triangles[i].Y;
                vertices[i * 3 + 2] = (float)triangles[i].Z;
            }

            // Compute MVP matrix
            var model = Matrix4x4.Identity;
            var mvp = Matrix4x4.Multiply(model, context.ViewMatrix);
            mvp = Matrix4x4.Multiply(mvp, context.ProjectionMatrix);

            // Render filled triangles
            _polygonRenderer.RenderTriangles(vertices, color, mvp);
        }

        /// <summary>
        /// Tessellate a glyph into triangles - handles multiple contours and holes
        /// </summary>
        private List<Point3D> TessellateGlyph(VectorizedGlyph glyph, double x, double y, double scale, double rotation)
        {
            var allTriangles = new List<Point3D>();
            double cosRot = Math.Cos(rotation);
            double sinRot = Math.Sin(rotation);

            System.Diagnostics.Debug.WriteLine($"[TessellateGlyph] Processing {glyph.Contours.Count} contours");

            // Process each contour separately
            for (int contourIndex = 0; contourIndex < glyph.Contours.Count; contourIndex++)
            {
                var contour = glyph.Contours[contourIndex];
                
                System.Diagnostics.Debug.WriteLine($"[TessellateGlyph] Contour {contourIndex}: {contour.Points.Count} points");

                if (contour.Points.Count < 3)
                {
                    System.Diagnostics.Debug.WriteLine($"[TessellateGlyph] Skipping contour {contourIndex} - too few points");
                    continue;
                }

                // Convert contour to world space polygon, handling curves
                var polygon = BuildPolygonFromContour(contour, x, y, scale, cosRot, sinRot);

                System.Diagnostics.Debug.WriteLine($"[TessellateGlyph] Built polygon with {polygon.Count} vertices");

                if (polygon.Count < 3)
                {
                    System.Diagnostics.Debug.WriteLine($"[TessellateGlyph] Skipping contour {contourIndex} - polygon too small");
                    continue;
                }

                // Determine winding order
                double area = CalculateSignedArea(polygon);
                System.Diagnostics.Debug.WriteLine($"[TessellateGlyph] Contour {contourIndex} signed area: {area:F6}");

                // Reverse if needed to ensure counter-clockwise for filled regions
                // In TrueType: outer contours are clockwise (negative area), holes are counter-clockwise (positive area)
                // For rendering, we want outer contours counter-clockwise
                if (area < 0)
                {
                    polygon.Reverse();
                    System.Diagnostics.Debug.WriteLine($"[TessellateGlyph] Reversed contour {contourIndex} to counter-clockwise");
                }

                // Triangulate the polygon using ear clipping
                var contourTriangles = EarClipping(polygon);
                
                System.Diagnostics.Debug.WriteLine($"[TessellateGlyph] Generated {contourTriangles.Count / 3} triangles for contour {contourIndex}");
                
                allTriangles.AddRange(contourTriangles);
            }

            return allTriangles;
        }

        /// <summary>
        /// Build a polygon from a contour, properly handling on-curve and off-curve points
        /// </summary>
        private List<Point3D> BuildPolygonFromContour(GlyphContour contour, double x, double y, 
            double scale, double cosRot, double sinRot)
        {
            var polygon = new List<Point3D>();
            
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var current = contour.Points[i];
                var next = contour.Points[(i + 1) % contour.Points.Count];

                if (current.OnCurve)
                {
                    // Add the on-curve point
                    polygon.Add(TransformGlyphPoint(current, x, y, scale, cosRot, sinRot));

                    // Check if next point is a control point (off-curve)
                    if (!next.OnCurve)
                    {
                        // Find the next on-curve point
                        int nextOnCurveIndex = (i + 2) % contour.Points.Count;
                        var nextOnCurve = contour.Points[nextOnCurveIndex];

                        // If the next point after control is also off-curve, insert implied on-curve point
                        if (!nextOnCurve.OnCurve)
                        {
                            // Create implied on-curve point at midpoint
                            var implied = new GlyphPoint(
                                (next.X + nextOnCurve.X) / 2.0,
                                (next.Y + nextOnCurve.Y) / 2.0,
                                true
                            );
							
							// Sample curve from current to implied
							var samples = SampleQuadraticBezierFromPoints(current, next, implied, 8);
							foreach (var sample in samples)
							{
								polygon.Add(TransformGlyphPoint(sample, x, y, scale, cosRot, sinRot));
							}
                        }
                        else
                        {
                            // Sample curve from current to nextOnCurve with next as control
                            var samples = SampleQuadraticBezierFromPoints(current, next, nextOnCurve, 8);
                            foreach (var sample in samples)
                            {
                                polygon.Add(TransformGlyphPoint(sample, x, y, scale, cosRot, sinRot));
                            }
                            i++; // Skip the control point in next iteration
                        }
                    }
                }
            }

            return polygon;
        }

        /// <summary>
        /// Sample a quadratic Bezier curve from GlyphPoints
        /// </summary>
        private List<GlyphPoint> SampleQuadraticBezierFromPoints(GlyphPoint p0, GlyphPoint p1Control, GlyphPoint p2, int segments)
        {
            var samples = new List<GlyphPoint>();
            
            // Don't include t=0 (that's already added), but include t=1
            for (int i = 1; i <= segments; i++)
            {
                double t = i / (double)segments;
                double t1 = 1 - t;

                double x = t1 * t1 * p0.X + 2 * t1 * t * p1Control.X + t * t * p2.X;
                double y = t1 * t1 * p0.Y + 2 * t1 * t * p1Control.Y + t * t * p2.Y;

                samples.Add(new GlyphPoint(x, y, true));
            }

            return samples;
        }

        /// <summary>
        /// Calculate signed area of polygon (positive = counter-clockwise, negative = clockwise)
        /// </summary>
        private double CalculateSignedArea(List<Point3D> polygon)
        {
            double area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var p1 = polygon[i];
                var p2 = polygon[(i + 1) % polygon.Count];
                area += (p2.X - p1.X) * (p2.Y + p1.Y);
            }
            return area / 2.0;
        }

        /// <summary>
        /// Sample a quadratic Bezier curve
        /// </summary>
        private List<GlyphPoint> SampleQuadraticBezier(GlyphPoint p0, GlyphPoint p1Control, GlyphPoint p2, int segments)
        {
            var samples = new List<GlyphPoint>();
            
            for (int i = 0; i <= segments; i++)
            {
                double t = i / (double)segments;
                double t1 = 1 - t;

                double x = t1 * t1 * p0.X + 2 * t1 * t * p1Control.X + t * t * p2.X;
                double y = t1 * t1 * p0.Y + 2 * t1 * t * p1Control.Y + t * t * p2.Y;

                samples.Add(new GlyphPoint(x, y, true));
            }

            return samples;
        }

        /// <summary>
        /// Transform a glyph point from font space to world space
        /// </summary>
        private Point3D TransformGlyphPoint(GlyphPoint point, double baseX, double baseY, 
            double scale, double cosRot, double sinRot)
        {
            // Scale from font units to world units
            double scaledX = point.X * scale;
            double scaledY = point.Y * scale;

            // Apply rotation
            double rotatedX = scaledX * cosRot - scaledY * sinRot;
            double rotatedY = scaledX * sinRot + scaledY * cosRot;

            // Translate to base point
            return new Point3D(baseX + rotatedX, baseY + rotatedY, 0);
        }

        /// <summary>
        /// Triangulate a polygon using the ear clipping algorithm
        /// </summary>
        private List<Point3D> EarClipping(List<Point3D> polygon)
        {
            if (polygon.Count < 3)
            {
                System.Diagnostics.Debug.WriteLine("[EarClipping] ERROR: Polygon has < 3 vertices");
                return new List<Point3D>();
            }

            var triangles = new List<Point3D>();
            var vertices = new List<Point3D>(polygon);

            int maxIterations = vertices.Count * 2; // Prevent infinite loops
            int iterations = 0;

            while (vertices.Count > 3 && iterations < maxIterations)
            {
                iterations++;
                bool earFound = false;

                for (int i = 0; i < vertices.Count; i++)
                {
                    int prev = (i - 1 + vertices.Count) % vertices.Count;
                    int next = (i + 1) % vertices.Count;

                    if (IsEar(vertices, prev, i, next))
                    {
                        // Add triangle (maintain counter-clockwise order)
                        triangles.Add(vertices[prev]);
                        triangles.Add(vertices[i]);
                        triangles.Add(vertices[next]);

                        // Remove the ear vertex
                        vertices.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                {
                    System.Diagnostics.Debug.WriteLine($"[EarClipping] WARNING: No ear found, {vertices.Count} vertices remaining. Using triangle fan fallback.");
                    
                    // Fallback: create a simple triangle fan from first vertex
                    for (int i = 1; i < vertices.Count - 1; i++)
                    {
                        triangles.Add(vertices[0]);
                        triangles.Add(vertices[i]);
                        triangles.Add(vertices[i + 1]);
                    }
                    break;
                }
            }

            // Add the final triangle
            if (vertices.Count == 3)
            {
                triangles.AddRange(vertices);
            }

            System.Diagnostics.Debug.WriteLine($"[EarClipping] Generated {triangles.Count / 3} triangles from {polygon.Count} vertices");

            return triangles;
        }

        /// <summary>
        /// Check if a vertex forms a valid ear (convex vertex with no other vertices inside)
        /// </summary>
        private bool IsEar(List<Point3D> vertices, int prev, int current, int next)
        {
            var a = vertices[prev];
            var b = vertices[current];
            var c = vertices[next];

            // Check if the triangle is counter-clockwise (convex)
            double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (cross <= 0) return false; // Concave or collinear

            // Check if any other vertex is inside this triangle
            for (int i = 0; i < vertices.Count; i++)
            {
                if (i == prev || i == current || i == next) continue;

                if (PointInTriangle(vertices[i], a, b, c))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check if a point is inside a triangle using barycentric coordinates
        /// </summary>
        private bool PointInTriangle(Point3D p, Point3D a, Point3D b, Point3D c)
        {
            double denominator = ((b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y));
            if (Math.Abs(denominator) < 1e-10) return false;

            double alpha = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / denominator;
            double beta = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / denominator;
            double gamma = 1.0 - alpha - beta;

            return alpha > 0 && beta > 0 && gamma > 0;
        }

        /// <summary>
        /// Get the text color from the text object
        /// </summary>
        private Vector3 GetTextColor(OpenCADText textObj)
        {
            var color = textObj.Color;
            
            // Convert System.Drawing.Color to Vector3 (RGB normalized to 0-1)
            return new Vector3(
                color.R / 255.0f,
                color.G / 255.0f,
                color.B / 255.0f
            );
        }
    }
}