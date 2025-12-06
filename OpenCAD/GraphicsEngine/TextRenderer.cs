using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.NonGeometric;
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
        private readonly ShaderProgram _lineShaderProgram; // kept for lines if needed elsewhere
        private readonly FillShaderProgram _fillShaderProgram; // NEW: fill shader for triangles
        private int _vao;
        private int _vbo;

        public TextRenderer(ShaderProgram lineShaderProgram, ITextMetricsProvider textMetrics)
        {
            _textMetrics = textMetrics;
            _lineShaderProgram = lineShaderProgram;
            _fillShaderProgram = new FillShaderProgram(); // create fill shader

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

        public bool CanRender(OpenCADObject obj) => obj is OpenCADText;

        public void Render(OpenCADObject obj, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (obj is not OpenCADText textObj) return;

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
                        viewMatrix,
                        projectionMatrix);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TextRenderer] Error: {ex.Message}");
            }
        }

        public void Render(OpenCADObject obj, RenderContext context) =>
            Render(obj, context.ViewMatrix, context.ProjectionMatrix);

        private void RenderGlyphFilled(
            VectorizedGlyph glyph,
            double glyphBaseX,
            double glyphBaseY,
            double cosRot,
            double sinRot,
            OpenCADText textObj,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix)
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

                // Draw triangles
                GL.Disable(EnableCap.CullFace);     // text can be concave; avoid accidental backface cull
                GL.Disable(EnableCap.DepthTest);    // render on top (adjust as needed)
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Count / 3);

                GL.BindVertexArray(0);
            }

            System.Diagnostics.Debug.WriteLine($"[TextRenderer] Glyph '{glyph.Character}' triangles: {totalTriangles}");
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