using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenTK.Graphics.OpenGL;
using System.Numerics;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using GraphicsEngine.Interfaces;
using OpenCAD.Styles.LineTypes;

namespace GraphicsEngine
{
    public class PolylineRenderer : IRenderer
    {
        private readonly PolylineShaderProgram _shaderProgram;
        private int _vao;
        private int _vbo;
        private bool closed = false;

        private Vector2 _viewport = new Vector2(800, 600);
        private const float THIN_LINE_THRESHOLD = 2.5f;
        private const float MIN_LINE_LENGTH = 0.0001f;
        private const double WIDTH_EPSILON = 1e-12;

        public PolylineRenderer(PolylineShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            // Updated interleaved layout with width attribute:
            // layout 0 : vec3 aPrevPosition
            // layout 1 : vec3 aPosition
            // layout 2 : vec3 aNextPosition
            // layout 3 : float aDistance
            // layout 4 : float aSide
            // layout 5 : float aWidth (NEW - per-vertex width in world units)
            //
            // stride = (3 + 3 + 3 + 1 + 1 + 1) * sizeof(float) = 12 * sizeof(float)
            int stride = 12 * sizeof(float);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, 9 * sizeof(float));
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, stride, 10 * sizeof(float));
            GL.EnableVertexAttribArray(4);
            GL.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, stride, 11 * sizeof(float));
            GL.EnableVertexAttribArray(5);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);

            GLDiag.Check("PolylineRenderer ctor end");
        }

        public bool CanRender(OpenCADObject obj) => obj is Polyline;

        public void Render(OpenCADObject obj, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            var ctx = new RenderContext
            {
                ViewMatrix = viewMatrix,
                ProjectionMatrix = projectionMatrix,
                IsHighlighted = false,
                IsSelected = false,
            };
            Render(obj, ctx);
        }

        public void Render(OpenCADObject obj, RenderContext context)
        {
            if (obj is not Polyline polyline) return;

            closed = polyline.IsClosed;

            var points = polyline.GetOrderedVertices().Select(v => v.Position).ToList();
            for (int i = 0; i < points.Count; i++)
                Debug.WriteLine($"pt[{i}] = {points[i].X}, {points[i].Y}, {points[i].Z}");

            // Resolve style from polyline properties (by-layer defaults handled by GeometryBase)
            var effectiveColor = polyline.Color;
            var effectiveLineWeight = polyline.LineWeight;
            var effectiveLineType = polyline.LineTypeID;

            Vector4 color = new Vector4(
                effectiveColor.R / 255f,
                effectiveColor.G / 255f,
                effectiveColor.B / 255f,
                effectiveColor.A / 255f
            );

            float lineWidth = effectiveLineWeight.ToOpenGLWidth();
            int lineTypePattern = 0;// GetLineTypePattern(effectiveLineType);
            double linetypeScale = polyline.LinetypeScale;

            // Selection/highlight overrides
            float glowRadius = 0.0f;
            if (context.IsSelected)
            {
                lineTypePattern = 8; // fine dashed
                lineWidth = Math.Max(lineWidth, 2.0f);
            }
            if (context.IsHighlighted)
            {
                glowRadius = 5.0f;
            }

            // Viewport dimensions
            if (context != null)
            {
                _viewport = context.Viewport;
                if (_viewport.X <= 0 || _viewport.Y <= 0) return;
            }
            else
            {
                int[] vp = new int[4];
                GL.GetInteger(GetPName.Viewport, vp);
                if (vp[2] <= 0 || vp[3] <= 0) return;
                _viewport = new Vector2(vp[2], vp[3]);
            }

            int segmentCount = polyline.GetSegmentCount();
            if (segmentCount == 0) return;

            bool isOrtho = MathF.Abs(context.ProjectionMatrix.M34) < 1e-6f &&
                           MathF.Abs(context.ProjectionMatrix.M44 - 1f) < 1e-6f;

            // Save GL state for blending/depth so we can enable blending for glow and restore afterwards
            bool blendWasEnabled = GL.IsEnabled(EnableCap.Blend);
            bool depthWasEnabled = GL.IsEnabled(EnableCap.DepthTest);
            // Save depth write mask so we can restore it after the glow pass
            bool depthWriteWasEnabled = true;
            GL.GetBoolean(GetPName.DepthWritemask, out depthWriteWasEnabled);

            // If we have glow, enable alpha blending so semi-transparent halo composites correctly.
            if (glowRadius > 0.0f)
            {
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                // Optionally: to prevent the glow halo from being clipped by depth,
                // you can disable depth test while drawing highlighted geometry.
                // Disable depth test and depth writes for the glow pass so the halo is not occluded
                // by nearby geometry. We keep the full polyline rendering afterwards with depth test/writes.
                GL.Disable(EnableCap.DepthTest);
                GL.DepthMask(false);
            }

            GL.BindVertexArray(_vao);
            _shaderProgram.Use();
            _shaderProgram.SetVector4("color", color);
            _shaderProgram.SetInt("lineTypePattern", lineTypePattern);
            _shaderProgram.SetVector2("viewport", _viewport);
            _shaderProgram.SetFloat("glowRadius", glowRadius);
            // To make the fragment shader produce a visible glow halo we must expand the rasterized geometry
            // so fragments exist in the halo region. Use 'renderLineWidth' for vertex offsets and 'coreLineWidth'
            // in the fragment shader to keep core vs halo semantics.
            float renderLineWidth = lineWidth + 2.0f * glowRadius; // expand geometry by glow radius on both sides
            _shaderProgram.SetFloat("renderLineWidth", renderLineWidth);
            _shaderProgram.SetFloat("coreLineWidth", lineWidth);
            _shaderProgram.SetFloat("lineTypeScale", (float)linetypeScale);

            if (isOrtho)
            {
                // For orthographic we still supply the projection as the MVP (no camera transform)
                RenderOrthographic(polyline, context.ViewMatrix, context.ProjectionMatrix, lineWidth, glowRadius);
            }
            else
            {
                RenderPerspective(polyline, context.ViewMatrix, context.ProjectionMatrix, lineWidth, glowRadius);
            }

            GL.BindVertexArray(0);

            // Restore saved GL state
            if (!blendWasEnabled) GL.Disable(EnableCap.Blend);
            // Restore depth test and depth write mask
            if (depthWasEnabled) GL.Enable(EnableCap.DepthTest);
            else GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(depthWriteWasEnabled);

            GLDiag.Check("PolylineRenderer draw end");
        }

        private void RenderOrthographic(Polyline polyline, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, float lineWidth, float glowRadius)
        {
            // Build world-space point sequence for the polyline (including arc tessellation and widths)
            var (points, widths) = GatherPolylinePointsWithWidths(polyline);
            if (points.Count < 2) return;

            Matrix4x4 mvp = viewMatrix * projectionMatrix;
            DrawPolyline(points, widths, mvp, lineWidth, glowRadius);
        }

        private void RenderPerspective(Polyline polyline, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, float lineWidth, float glowRadius)
        {
            // Build world-space point sequence for the polyline (including arc tessellation and widths)
            var (points, widths) = GatherPolylinePointsWithWidths(polyline);
            if (points.Count < 2) return;

            // Preserve the existing ordering used elsewhere: view * projection (kept for consistency)
            Matrix4x4 mvp = Matrix4x4.Identity;
            mvp = Matrix4x4.Multiply(mvp, viewMatrix);
            mvp = Matrix4x4.Multiply(mvp, projectionMatrix);

            DrawPolyline(points, widths, mvp, lineWidth, glowRadius);
        }

        /// <summary>
        /// Convert the polyline (segments + arcs) into a sequential list of world-space points with per-point widths.
        /// Avoids duplicate consecutive points. Width is interpolated along segments.
        /// </summary>
        private (List<Point3D> points, List<float> widths) GatherPolylinePointsWithWidths(Polyline polyline)
        {
            var pts = new List<Point3D>();
            var widths = new List<float>();

            int segmentCount = polyline.GetSegmentCount();
            
            // DEBUG: Log vertex widths for all vertices
            Debug.WriteLine($"=== Polyline Render: {polyline.VertexCount} vertices, {segmentCount} segments ===");
            for (int v = 0; v < polyline.VertexCount; v++)
            {
                var vertex = polyline.GetVertex(v);
                if (vertex != null)
                {
                    Debug.WriteLine($"  Vertex {v}: StartWidth={vertex.StartWidth:F3}, EndWidth={vertex.EndWidth:F3}, Pos=({vertex.Position.X:F2},{vertex.Position.Y:F2})");
                }
            }
            
            for (int i = 0; i < segmentCount; i++)
            {
                var (start, end, bulge) = polyline.GetSegment(i);
                var startVertex = polyline.GetVertex(i);
                var endVertex = polyline.GetVertex(i == polyline.VertexCount - 1 ? 0 : i + 1);

                if (startVertex == null || endVertex == null) continue;

                // Get width values from vertices - each segment is independent
                double startWidth = startVertex.StartWidth;
                double endWidth = endVertex.EndWidth;
                
                // DEBUG: Log which widths are used for this segment
                Debug.WriteLine($"  Segment {i}: using StartVertex[{i}].StartWidth={startWidth:F3}, EndVertex[{(i == polyline.VertexCount - 1 ? 0 : i + 1)}].EndWidth={endWidth:F3}");

                if (Math.Abs(bulge) <= 1e-10)
                {
                    // Straight segment: always add start point for this segment
                    if (i == 0)
                    {
                        // First segment: add start point
                        pts.Add(start);
                        widths.Add((float)startWidth);
                    }
                    else
                    {
                        // Subsequent segments: start point may coincide with previous end, but we still add it
                        // because the width might differ (discontinuous width change)
                        pts.Add(start);
                        widths.Add((float)startWidth);
                    }
                    
                    // Always add end point
                    pts.Add(end);
                    widths.Add((float)endWidth);
                }
                else
                {
                    // Arc segment: tessellate with interpolated widths
                    var (arcPoints, arcWidths) = TessellateArcSegmentWithWidths(start, end, bulge, startWidth, endWidth);
                    
                    if (i == 0)
                    {
                        // First segment: add all points
                        pts.AddRange(arcPoints);
                        widths.AddRange(arcWidths);
                    }
                    else
                    {
                        // Subsequent segments: add all points (including start, even if it coincides with previous end)
                        pts.AddRange(arcPoints);
                        widths.AddRange(arcWidths);
                    }
                }
            }
            
            Debug.WriteLine($"=== Final: {pts.Count} render points ===");

            return (pts, widths);
        }

        private static bool ApproximatelyEqual(Point3D a, Point3D b, double tol = 1e-8)
        {
            return Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol && Math.Abs(a.Z - b.Z) <= tol;
        }

        /// <summary>
        /// Build interleaved vertex buffer expected by the Polyline shader and draw as a triangle-strip.
        /// Each poly vertex emits two VERTEX records (side = -1, +1).
        /// Vertex layout: prev.xyz, cur.xyz, next.xyz, distance, side, width
        /// </summary>
        private void DrawPolyline(List<Point3D> points, List<float> widths, Matrix4x4 mvp, float defaultLineWidth, float glowRadius)
        {
            int n = points.Count;
            if (n < 2) return;

            // Check if any vertex has non-zero width
            bool hasVariableWidth = widths.Any(w => w > WIDTH_EPSILON);

            // Compute cumulative world distances
            var distances = new List<float>(n);
            distances.Add(0f);
            for (int i = 1; i < n; i++)
            {
                var a = new Vector3((float)points[i - 1].X, (float)points[i - 1].Y, (float)points[i - 1].Z);
                var b = new Vector3((float)points[i].X, (float)points[i].Y, (float)points[i].Z);
                float d = Vector3.Distance(a, b);
                distances.Add(distances[i - 1] + d);
            }

            // Helper: find a distinct neighbor in direction dir (-1 or +1). If closed, wrap around.
            int FindDistinctIndex(int start, int dir)
            {
                int maxSteps = n; // limit steps to avoid infinite loop
                int idx = start;
                for (int step = 0; step < maxSteps; step++)
                {
                    idx += dir;
                    if (closed)
                    {
                        idx = (idx % n + n) % n;
                    }
                    else
                    {
                        if (idx < 0 || idx >= n) break;
                    }

                    if (!ApproximatelyEqual(points[idx], points[start])) return idx;
                }
                return -1;
            }

            // Build interleaved buffer: two verts per point
            var vb = new List<float>(n * 2 * 12);
            for (int i = 0; i < n; i++)
            {
                int prevIndex = (i == 0) ? (closed ? n - 1 : 0) : i - 1;
                int nextIndex = (i == n - 1) ? (closed ? 0 : n - 1) : i + 1;

                Point3D prevWorld = points[prevIndex];
                Point3D nextWorld = points[nextIndex];
                Point3D curWorld = points[i];

                float dist = distances[i];
                float width = hasVariableWidth ? widths[i] : 0f; // 0 -> use uniform width in shader

                // Detect degenerate neighbor case where prev==cur==next (or nearly so) and attempt to recover.
                if (ApproximatelyEqual(prevWorld, curWorld) && ApproximatelyEqual(nextWorld, curWorld))
                {
                    int distinctPrev = FindDistinctIndex(i, -1);
                    int distinctNext = FindDistinctIndex(i, 1);

                    if (distinctPrev != -1) prevWorld = points[distinctPrev];
                    if (distinctNext != -1) nextWorld = points[distinctNext];

                    // If still degenerate, fabricate a tiny symmetric offset along X to produce a stable tangent.
                    if (ApproximatelyEqual(prevWorld, curWorld) && ApproximatelyEqual(nextWorld, curWorld))
                    {
                        const double EPS_FALLBACK = 1e-6;
                        prevWorld = new Point3D(curWorld.X + EPS_FALLBACK, curWorld.Y, curWorld.Z);
                        nextWorld = new Point3D(curWorld.X - EPS_FALLBACK, curWorld.Y, curWorld.Z);
                    }
                }

                // Transform prev/cur/next to NDC
                Vector3 prevNdc = WorldToNdc(prevWorld, mvp);
                Vector3 curNdc = WorldToNdc(curWorld, mvp);
                Vector3 nextNdc = WorldToNdc(nextWorld, mvp);

                // left side (-1)
                vb.Add(prevNdc.X); vb.Add(prevNdc.Y); vb.Add(prevNdc.Z);
                vb.Add(curNdc.X); vb.Add(curNdc.Y); vb.Add(curNdc.Z);
                vb.Add(nextNdc.X); vb.Add(nextNdc.Y); vb.Add(nextNdc.Z);
                vb.Add(dist);
                vb.Add(-1.0f);
                vb.Add(width);

                // right side (+1)
                vb.Add(prevNdc.X); vb.Add(prevNdc.Y); vb.Add(prevNdc.Z);
                vb.Add(curNdc.X); vb.Add(curNdc.Y); vb.Add(curNdc.Z);
                vb.Add(nextNdc.X); vb.Add(nextNdc.Y); vb.Add(nextNdc.Z);
                vb.Add(dist);
                vb.Add(1.0f);
                vb.Add(width);

            }

            float[] data = vb.ToArray();

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.DynamicDraw);

            // Set MVP for shader
            _shaderProgram.SetMatrix4("mvp", Matrix4x4.Identity);
            _shaderProgram.SetInt("hasVariableWidth", hasVariableWidth ? 1 : 0);

            // Draw triangle strip - vertex count is points.Count * 2
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, n * 2);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        private static Vector3 WorldToNdc(Point3D p, Matrix4x4 vp)
        {
            var world = new Vector4((float)p.X, (float)p.Y, (float)p.Z, 1f);
            var clip = Vector4.Transform(world, vp);

            if (Math.Abs(clip.W) < 1e-6f)
                return new Vector3(clip.X, clip.Y, clip.Z); // or return zero/guard

            return new Vector3(clip.X / clip.W,
                               clip.Y / clip.W,
                               clip.Z / clip.W);
        }

        private int GetLineTypePattern(LineType lineType)
        {
            return lineType switch
            {
                LineType.Continuous => 0,
                LineType.Dashed => 1,
                LineType.Dotted => 2,
                LineType.DashDot => 3,
                LineType.DashDotDot => 4,
                LineType.Center => 5,
                LineType.Hidden => 6,
                LineType.Phantom => 7,
                LineType.ByLayer => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Tessellate arc segment into points with interpolated widths
        /// </summary>
        private (List<Point3D> points, List<float> widths) TessellateArcSegmentWithWidths(
            Point3D start, Point3D end, double bulge, double startWidth, double endWidth)
        {
            var points = new List<Point3D>();
            var widths = new List<float>();

            double angle = GeometricCalculator.GetAngleFromBulge(bulge);
            double absAngle = Math.Abs(angle);
            int numSegments = Math.Clamp((int)(absAngle / (Math.PI / 16)), 8, 32);
            (double radius, double startAngle, double endAngle, Point3D center) = 
                GeometricCalculator.GetArcParametersFromBulge(start, end, bulge);

            // Add start point
            points.Add(start);
            widths.Add((float)startWidth);

            // Add intermediate points with interpolated widths
            for (int i = 1; i <= numSegments; i++)
            {
                double t = (double)i / numSegments;
                double currentAngle = startAngle + t * (endAngle - startAngle);
                double x = center.X + radius * Math.Cos(currentAngle);
                double y = center.Y + radius * Math.Sin(currentAngle);
                double z = start.Z + t * (end.Z - start.Z);
                Point3D currentPoint = new Point3D(x, y, z);

                // Linearly interpolate width
                double currentWidth = startWidth + t * (endWidth - startWidth);

                points.Add(currentPoint);
                widths.Add((float)currentWidth);
            }

            return (points, widths);
        }

        private void TessellateArcSegment(List<float> vertexList, Point3D start, Point3D end, double bulge)
        {
            double angle = GeometricCalculator.GetAngleFromBulge(bulge);
            double absAngle = Math.Abs(angle);
            int numSegments = Math.Clamp((int)(absAngle / (Math.PI / 16)), 8, 32);
            (double radius, double startAngle, double endAngle, Point3D center) = GeometricCalculator.GetArcParametersFromBulge(start, end, bulge);


            Point3D prevPoint = start;
            for (int i = 1; i <= numSegments; i++)
            {
                double t = (double)i / numSegments;
                double currentAngle = startAngle + t * (endAngle - startAngle);
                double x = center.X + radius * Math.Cos(currentAngle);
                double y = center.Y + radius * Math.Sin(currentAngle);
                double z = start.Z + t * (end.Z - start.Z);
                Point3D currentPoint = new Point3D(x, y, z);

                AddVertex(vertexList, prevPoint);
                AddVertex(vertexList, currentPoint);
                prevPoint = currentPoint;
            }
        }

        private void AddVertex(List<float> vertexList, Point3D point)
        {
            vertexList.Add((float)point.X);
            vertexList.Add((float)point.Y);
            vertexList.Add((float)point.Z);
        }
    }
}