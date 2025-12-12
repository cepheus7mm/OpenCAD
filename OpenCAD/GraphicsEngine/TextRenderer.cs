using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.TextRendering;
using OpenTK.Graphics.OpenGL;
using Poly2Tri;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace GraphicsEngine
{
    public class TextRenderer : IRenderer
    {
        private readonly ITextMetricsProvider _textMetrics;
        private readonly ShaderProgram _lineShaderProgram;
        private readonly FillShaderProgram _fillShaderProgram;
        private int _vao;
        private int _vbo;

        public TextRenderer(ShaderProgram lineShaderProgram, ITextMetricsProvider textMetrics)
        {
            _textMetrics = textMetrics;
            _lineShaderProgram = lineShaderProgram;
            _fillShaderProgram = new FillShaderProgram();

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            System.Diagnostics.Debug.WriteLine($"TextRenderer initialized with VAO={_vao}, VBO={_vbo}");
        }

        public bool CanRender(OpenCADObject obj) => obj is SText;

        // ONLY ONE RENDER METHOD - takes RenderContext
        public void Render(OpenCADObject obj, RenderContext context)
        {
            if (obj is not SText textObj) return;

            try
            {
                var fontDescriptor = new FontDescriptor(textObj.FontFamily, textObj.IsBold, textObj.IsItalic);

                var basePoint = textObj.BasePoint;
                double fontSize = textObj.FontSize;
                double rotation = textObj.Rotation;

                var positionedGlyphs = _textMetrics.GetTextOutlines(textObj.Text, fontDescriptor, fontSize);

                double cosRot = Math.Cos(rotation);
                double sinRot = Math.Sin(rotation);

                foreach (var positioned in positionedGlyphs)
                {
                    double localX = positioned.X;
                    double localY = positioned.Y;

                    double worldOffsetX = localX * cosRot - localY * sinRot;
                    double worldOffsetY = localX * sinRot + localY * cosRot;

                    double glyphWorldX = basePoint.X + worldOffsetX;
                    double glyphWorldY = basePoint.Y + worldOffsetY;

                    RenderGlyphFilled(
                        positioned.Glyph,
                        glyphWorldX,
                        glyphWorldY,
                        cosRot,
                        sinRot,
                        textObj,
                        context.ViewMatrix,
                        context.ProjectionMatrix,
                        context.IsHighlighted,
                        context.IsSelected);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextRenderer] Error: {ex.Message}");
            }
        }

        private void RenderGlyphFilled(
            VectorizedGlyph glyph,
            double glyphBaseX,
            double glyphBaseY,
            double cosRot,
            double sinRot,
            SText textObj,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            bool isHighlighted,
            bool isSelected)
        {
            if (glyph.Contours.Count == 0) return;

            // Detect orthographic projection (identity view in ortho)
            bool isOrtho = MathF.Abs(projectionMatrix.M34) < 1e-6f && MathF.Abs(projectionMatrix.M44 - 1f) < 1e-6f;

            // Extract ortho parameters to convert world XY to NDC on CPU
            float centerX = 0f, centerY = 0f, halfW = 1f, halfH = 1f;
            if (isOrtho)
            {
                float sx = projectionMatrix.M11;
                float sy = projectionMatrix.M22;
                float txRow = projectionMatrix.M41;
                float tyRow = projectionMatrix.M42;

                if (MathF.Abs(sx) > 1e-12f && MathF.Abs(sy) > 1e-12f)
                {
                    halfW = 1.0f / sx;
                    halfH = 1.0f / sy;
                    centerX = -txRow / sx;
                    centerY = -tyRow / sy;
                }
            }

            // Normalize contours, split outer/holes, attach holes, triangulate (Poly2Tri)
            var normalizedContours = glyph.Contours
                .Select(c => NormalizeContour(c))
                .Where(list => list.Count >= 3)
                .ToList();

            var outers = new List<List<(double X, double Y)>>();
            var holes = new List<List<(double X, double Y)>>();

            foreach (var points in normalizedContours)
            {
                var area = SignedArea(points);
                if (area < 0)
                {
                    // Outer CW -> reverse to CCW
                    points.Reverse();
                    outers.Add(points);
                }
                else
                {
                    // Hole CCW -> reverse to CW
                    points.Reverse();
                    holes.Add(points);
                }
            }

            if (outers.Count == 0)
            {
                var sorted = normalizedContours.OrderByDescending(p => Math.Abs(SignedArea(p))).ToList();
                if (sorted.Count > 0)
                {
                    var primary = sorted[0];
                    if (SignedArea(primary) < 0) primary.Reverse();
                    outers.Add(primary);

                    for (int i = 1; i < sorted.Count; i++)
                    {
                        var holePts = sorted[i];
                        if (SignedArea(holePts) > 0) holePts.Reverse();
                        holes.Add(holePts);
                    }
                }
            }

            var outerWithHoles = new List<(List<(double X, double Y)> Outer, List<List<(double X, double Y)>> Holes)>();
            foreach (var outer in outers)
                outerWithHoles.Add((outer, new List<List<(double X, double Y)>>()));

            foreach (var h in holes)
            {
                bool attached = false;
                var test = h[0];
                for (int i = 0; i < outerWithHoles.Count && !attached; i++)
                {
                    if (PointInPolygon(test, outerWithHoles[i].Outer))
                    {
                        outerWithHoles[i].Holes.Add(h);
                        attached = true;
                    }
                }
            }

            int totalTriangles = 0;

            foreach (var item in outerWithHoles)
            {
                if (item.Outer.Count < 3) continue;

                var outerPts = item.Outer.Select(p => new Poly2Tri.Triangulation.Polygon.PolygonPoint(p.X, p.Y)).ToList();
                var poly = new Poly2Tri.Triangulation.Polygon.Polygon(outerPts);

                foreach (var hole in item.Holes)
                {
                    if (hole.Count < 3) continue;
                    var holePts = hole.Select(p => new Poly2Tri.Triangulation.Polygon.PolygonPoint(p.X, p.Y)).ToList();
                    poly.AddHole(new Poly2Tri.Triangulation.Polygon.Polygon(holePts));
                }

                try
                {
                    P2T.Triangulate(poly);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TextRenderer] Poly2Tri error: {ex.Message}");
                    continue;
                }

                var vertices = new List<float>(poly.Triangles.Count * 9);
                foreach (var tri in poly.Triangles)
                {
                    foreach (var pt in tri.Points)
                    {
                        double worldX = pt.X * cosRot - pt.Y * sinRot + glyphBaseX;
                        double worldY = pt.X * sinRot + pt.Y * cosRot + glyphBaseY;

                        if (isOrtho)
                        {
                            // World XY -> NDC
                            float ndcX = (float)((worldX - centerX) / halfW);
                            float ndcY = (float)((worldY - centerY) / halfH);
                            vertices.Add(ndcX);
                            vertices.Add(ndcY);
                            vertices.Add(0f);
                        }
                        else
                        {
                            vertices.Add((float)worldX);
                            vertices.Add((float)worldY);
                            vertices.Add(0f);
                        }
                    }
                }

                if (vertices.Count == 0) continue;

                totalTriangles += vertices.Count / 9;

                // Upload positions
                GL.BindVertexArray(_vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.DynamicDraw);

                // Bind attrib 0 (aPosition)
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);

                // Use fill shader
                _fillShaderProgram.Use();

                // MVP setup
                if (isOrtho)
                {
                    _fillShaderProgram.SetMvp(Matrix4x4.Identity);
                }
                else
                {
                    var mvp = Matrix4x4.Identity;
                    mvp = Matrix4x4.Multiply(mvp, viewMatrix);
                    mvp = Matrix4x4.Multiply(mvp, projectionMatrix);
                    _fillShaderProgram.SetMvp(mvp);
                }

                // Color from text object (RGBA)
                var c = textObj.Color;
                _fillShaderProgram.SetColor(new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, 1f));

                // Set selection state for stippling
                _fillShaderProgram.SetIsSelected(isSelected);

                // Draw triangles
                GL.Disable(EnableCap.CullFace);     // text can be concave; avoid accidental backface cull
                GL.Disable(EnableCap.DepthTest);    // render on top (adjust as needed)
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Count / 3);

                GL.BindVertexArray(0);

                // If highlighted, render contour outlines
                if (isHighlighted)
                {
                    RenderContourOutlines(item.Outer, item.Holes, glyphBaseX, glyphBaseY, cosRot, sinRot, 
                        isOrtho, centerX, centerY, halfW, halfH, viewMatrix, projectionMatrix, textObj.Color);
                }
            }

            //System.Diagnostics.Debug.WriteLine($"[TextRenderer] Glyph '{glyph.Character}' triangles: {totalTriangles}");
        }

        private void RenderContourOutlines(
            List<(double X, double Y)> outer,
            List<List<(double X, double Y)>> holes,
            double glyphBaseX,
            double glyphBaseY,
            double cosRot,
            double sinRot,
            bool isOrtho,
            float centerX,
            float centerY,
            float halfW,
            float halfH,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix,
            System.Drawing.Color baseColor)
        {
            // Highlight color - use the object's color
            Vector4 highlightColor = new Vector4(baseColor.R / 255f, baseColor.G / 255f, baseColor.B / 255f, 1.0f);

            // Get viewport for screen space calculations
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);
            Vector2 viewportSize = new Vector2(viewport[2], viewport[3]);

            // Calculate approximate text height in screen space for scaling
            // Use the outer contour's bounding box
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var pt in outer)
            {
                double worldX = pt.X * cosRot - pt.Y * sinRot + glyphBaseX;
                double worldY = pt.X * sinRot + pt.Y * cosRot + glyphBaseY;

                float ndcY;
                if (isOrtho)
                {
                    ndcY = (float)((worldY - centerY) / halfH);
                }
                else
                {
                    ndcY = (float)worldY;
                }

                float screenY = (ndcY * 0.5f + 0.5f) * viewportSize.Y;
                minY = MathF.Min(minY, screenY);
                maxY = MathF.Max(maxY, screenY);
            }

            float textHeightInPixels = MathF.Abs(maxY - minY);

            // More aggressive non-linear scaling for smaller text
            // Use a power function to reduce glow more dramatically when zoomed out
            // Formula: baseGlow * (textHeight / referenceHeight) ^ 0.7
            // This gives:
            // - 10px text → 1.3px glow (less intrusive when small)
            // - 20px text → 2.0px glow (reference)
            // - 40px text → 3.2px glow
            // - 80px text → 5.1px glow
            float baseGlow = 2.0f;
            float referenceHeight = 20.0f;
            float heightRatio = MathF.Max(textHeightInPixels, 1.0f) / referenceHeight;
            float glowRadius = baseGlow * MathF.Pow(heightRatio, 0.7f); // Power of 0.7 for more aggressive scaling down

            // Clamp glow radius to reasonable bounds
            glowRadius = MathF.Min(MathF.Max(glowRadius, 1.5f), 12.0f);

            float lineWidth = 2.0f;
            float totalWidth = lineWidth + glowRadius * 2.0f;

            // Combine outer and holes into one list of contours
            var allContours = new List<List<(double X, double Y)>> { outer };
            allContours.AddRange(holes);

            foreach (var contour in allContours)
            {
                if (contour.Count < 2) continue;

                // Store world positions for distance calculation
                var worldPositions = new List<Vector2>();
                var ndcPositions = new List<Vector3>();
                var screenPositions = new List<Vector2>();

                foreach (var pt in contour)
                {
                    // Transform to world space
                    double worldX = pt.X * cosRot - pt.Y * sinRot + glyphBaseX;
                    double worldY = pt.X * sinRot + pt.Y * cosRot + glyphBaseY;
                    worldPositions.Add(new Vector2((float)worldX, (float)worldY));

                    float ndcX, ndcY;
                    if (isOrtho)
                    {
                        // World XY -> NDC
                        ndcX = (float)((worldX - centerX) / halfW);
                        ndcY = (float)((worldY - centerY) / halfH);
                    }
                    else
                    {
                        ndcX = (float)worldX;
                        ndcY = (float)worldY;
                    }

                    ndcPositions.Add(new Vector3(ndcX, ndcY, 0f));

                    // Calculate screen position for shader uniforms
                    float screenX = (ndcX * 0.5f + 0.5f) * viewportSize.X;
                    float screenY = (ndcY * 0.5f + 0.5f) * viewportSize.Y;
                    screenPositions.Add(new Vector2(screenX, screenY));
                }

                if (ndcPositions.Count == 0) continue;

                // Render each segment as a quad (including closing segment)
                int segmentCount = ndcPositions.Count;
                float cumulativeDistance = 0f;

                for (int i = 0; i < segmentCount; i++)
                {
                    int nextIdx = (i + 1) % segmentCount;

                    // Calculate segment length in world space
                    float dx = worldPositions[nextIdx].X - worldPositions[i].X;
                    float dy = worldPositions[nextIdx].Y - worldPositions[i].Y;
                    float segmentLength = MathF.Sqrt(dx * dx + dy * dy);

                    if (segmentLength < 1e-6f) continue; // Skip degenerate segments

                    // Create quad for this segment
                    Vector2 ndcStart = new Vector2(ndcPositions[i].X, ndcPositions[i].Y);
                    Vector2 ndcEnd = new Vector2(ndcPositions[nextIdx].X, ndcPositions[nextIdx].Y);

                    // Calculate half-width in NDC space (to cover glow area)
                    float halfWidthNDC = (totalWidth * 0.5f) / (viewportSize.X * 0.5f);

                    // Calculate perpendicular offset
                    Vector2 lineVec = ndcEnd - ndcStart;
                    float lineLen = lineVec.Length();
                    if (lineLen < 1e-6f) continue;

                    Vector2 lineDir = lineVec / lineLen;
                    Vector2 perpDir = new Vector2(-lineDir.Y, lineDir.X);
                    Vector2 offset = perpDir * halfWidthNDC;

                    // Create quad vertices (perpendicular to line direction)
                    Vector2 v0 = ndcStart + offset;
                    Vector2 v1 = ndcStart - offset;
                    Vector2 v2 = ndcEnd + offset;
                    Vector2 v3 = ndcEnd - offset;

                    // Build interleaved buffer for quad (6 vertices for 2 triangles)
                    float[] interleaved = new float[24]; // 6 vertices * 4 floats each

                    // Triangle 1: v0, v1, v2
                    interleaved[0] = v0.X; interleaved[1] = v0.Y; interleaved[2] = 0f; interleaved[3] = 0f;
                    interleaved[4] = v1.X; interleaved[5] = v1.Y; interleaved[6] = 0f; interleaved[7] = 0f;
                    interleaved[8] = v2.X; interleaved[9] = v2.Y; interleaved[10] = 0f; interleaved[11] = segmentLength;

                    // Triangle 2: v2, v1, v3
                    interleaved[12] = v2.X; interleaved[13] = v2.Y; interleaved[14] = 0f; interleaved[15] = segmentLength;
                    interleaved[16] = v1.X; interleaved[17] = v1.Y; interleaved[18] = 0f; interleaved[19] = 0f;
                    interleaved[20] = v3.X; interleaved[21] = v3.Y; interleaved[22] = 0f; interleaved[23] = segmentLength;

                    // Upload interleaved data (position + distance)
                    GL.BindVertexArray(_vao);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, interleaved.Length * sizeof(float), interleaved, BufferUsageHint.DynamicDraw);

                    // Set up vertex attributes for line shader (position at 0, distance at 1)
                    int stride = 4 * sizeof(float);
                    GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
                    GL.EnableVertexAttribArray(1);

                    // Use line shader program
                    _lineShaderProgram.Use();

                    // MVP setup - already in NDC for ortho
                    if (isOrtho)
                    {
                        _lineShaderProgram.SetMatrix4("mvp", Matrix4x4.Identity);
                    }
                    else
                    {
                        var mvp = Matrix4x4.Identity;
                        mvp = Matrix4x4.Multiply(mvp, viewMatrix);
                        mvp = Matrix4x4.Multiply(mvp, projectionMatrix);
                        _lineShaderProgram.SetMatrix4("mvp", mvp);
                    }

                    // Set highlight color (now matches object color)
                    _lineShaderProgram.SetVector4("color", highlightColor);

                    // Set glow effect parameters
                    _lineShaderProgram.SetFloat("glowRadius", glowRadius);
                    _lineShaderProgram.SetFloat("lineWidth", lineWidth);
                    _lineShaderProgram.SetVector2("viewport", viewportSize);

                    // Disable line patterns for highlight (continuous line)
                    _lineShaderProgram.SetInt("lineTypePattern", 0);
                    _lineShaderProgram.SetFloat("lineTypeScale", 1.0f);
                    _lineShaderProgram.SetFloat("lineLength", segmentLength);

                    // Set line start/end in SCREEN SPACE for THIS SEGMENT
                    _lineShaderProgram.SetVector2("lineStart", screenPositions[i]);
                    _lineShaderProgram.SetVector2("lineEnd", screenPositions[nextIdx]);

                    // Draw quad as triangles
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                    GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

                    cumulativeDistance += segmentLength;
                }

                // Disable attribute 1 after use
                GL.DisableVertexAttribArray(1);
                GL.BindVertexArray(0);

                //System.Diagnostics.Debug.WriteLine($"[TextRenderer] Rendered contour outline with {segmentCount} segments, totalDistance={cumulativeDistance:F3}, glowRadius={glowRadius:F1}px (textHeight={textHeightInPixels:F1}px)");
            }
        }

        // Helpers (unchanged)
        private static double SignedArea(List<(double X, double Y)> pts)
        {
            double area = 0;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += pts[i].X * pts[j].Y - pts[j].X * pts[i].Y;
            }
            return area / 2.0;
        }

        private static bool PointInPolygon((double X, double Y) p, List<(double X, double Y)> poly)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = poly[i];
                var pj = poly[j];
                bool intersect = ((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                                 (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / ((pj.Y - pi.Y) == 0 ? 1e-12 : (pj.Y - pi.Y)) + pi.X);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private static List<(double X, double Y)> NormalizeContour(GlyphContour contour)
        {
            var pts = contour.Points;
            var onCurve = pts.Where(pt => pt.OnCurve).Select(p => (p.X, p.Y)).ToList();

            List<(double X, double Y)> usePts = onCurve.Count >= 3
                ? onCurve
                : pts.Select(p => (p.X, p.Y)).ToList();

            if (usePts.Count >= 2)
            {
                var first = usePts[0];
                var last = usePts[^1];
                if (NearlyEqual(first.X, last.X) && NearlyEqual(first.Y, last.Y))
                    usePts.RemoveAt(usePts.Count - 1);
            }

            var cleaned = new List<(double X, double Y)>(usePts.Count);
            const double eps2 = 1e-12;
            for (int i = 0; i < usePts.Count; i++)
            {
                var cur = usePts[i];
                if (cleaned.Count == 0)
                {
                    cleaned.Add(cur);
                }
                else
                {
                    var prev = cleaned[^1];
                    double dx = cur.X - prev.X, dy = cur.Y - prev.Y;
                    if (dx * dx + dy * dy > eps2)
                        cleaned.Add(cur);
                }
            }

            if (cleaned.Count < 3)
                cleaned = usePts;

            return cleaned;
        }

        private static bool NearlyEqual(double a, double b, double eps = 1e-12) => Math.Abs(a - b) <= eps;
    }
}