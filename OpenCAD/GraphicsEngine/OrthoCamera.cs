using GraphicsEngine.Interfaces;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GraphicsEngine
{
    public sealed class OrthoCamera : ICamera
    {
        // -------------------------
        // Camera state history
        // -------------------------
        private record struct CameraState(Vector3 Position, Vector3 Target, float WorldWidth);
        private const int MaxHistoryDepth = 50;
        private readonly Stack<CameraState> _stateHistory = new();

        public bool CanZoomPrevious => _stateHistory.Count > 0;

        public void PushState()
        {
            if (_stateHistory.Count >= MaxHistoryDepth)
            {
                // Trim the oldest entry by rebuilding without it
                var entries = _stateHistory.ToArray(); // top-first
                _stateHistory.Clear();
                for (int i = entries.Length - 2; i >= 0; i--)
                    _stateHistory.Push(entries[i]);
            }
            _stateHistory.Push(new CameraState(Position, Target, WorldWidth));
        }

        public bool PopState()
        {
            if (!_stateHistory.TryPop(out var state))
                return false;

            Position = state.Position;
            Target = state.Target;
            WorldWidth = state.WorldWidth;
            UpdateProjection(ViewportWidthPx / ViewportHeightPx);
            return true;
        }

        // -------------------------
        // Camera state
        // -------------------------
        public Vector3 Position { get; set; }
        public Vector3 Target { get; set; }
        public Vector3 Up { get; set; }

        public float WorldWidth { get; private set; } = 40f;
        public float WorldHeight { get; private set; }

        public float Near = -1000f;
        public float Far = 1000f;

        // -------------------------
        // Viewport + DPI
        // -------------------------
        public float ViewportWidthPx { get; private set; }
        public float ViewportHeightPx { get; private set; }
        public float DpiScaleX { get; private set; } = 1f;
        public float DpiScaleY { get; private set; } = 1f;

        // -------------------------
        // Matrices
        // -------------------------
        public Matrix4x4 ViewMatrix => Matrix4x4.CreateLookAt(Position, Target, Up);
        public Matrix4x4 ProjectionMatrix { get; private set; }
        public Matrix4x4 ViewProjectionMatrix { get; private set; }
        public Matrix4x4 InverseViewProjectionMatrix { get; private set; }

        public float PanScale => WorldWidth;

        public OrthoCamera(float initialWidth)
        {
            Position = new Vector3(0, 0, 10);
            Target = new Vector3(0, 0, 0);
            Up = new Vector3(0, 1, 0);

            WorldWidth = initialWidth;
        }

        // -------------------------
        // Viewport + DPI setters
        // -------------------------
        public void SetViewportSize(float widthPx, float heightPx)
        {
            ViewportWidthPx = Math.Max(1, widthPx);
            ViewportHeightPx = Math.Max(1, heightPx);

            float aspect = ViewportWidthPx / ViewportHeightPx;
            WorldHeight = WorldWidth / aspect;

            UpdateProjection(aspect);
        }

        public void SetDpi(float dpiX, float dpiY)
        {
            DpiScaleX = dpiX;
            DpiScaleY = dpiY;
        }

        // -------------------------
        // Projection update
        // -------------------------
        public void UpdateProjection(float aspect)
        {
            WorldHeight = WorldWidth / aspect;

            ProjectionMatrix = Matrix4x4.CreateOrthographic(
                WorldWidth,
                WorldHeight,
                Near,
                Far
            );

            ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
            Matrix4x4.Invert(ViewProjectionMatrix, out var inv);
            InverseViewProjectionMatrix = inv;
        }

        // -------------------------
        // Camera movement
        // -------------------------
        public void Zoom(float zoomFactor)
        {
            PushState();
            WorldWidth /= zoomFactor;
            WorldWidth = Math.Clamp(WorldWidth, 1f, 1e9f);

            float aspect = ViewportWidthPx / ViewportHeightPx;
            UpdateProjection(aspect);
        }

        public void Pan(Vector2 deltaPixels)
        {
            PushState();
            float dxWorld = (deltaPixels.X / ViewportWidthPx) * WorldWidth;
            float dyWorld = -(deltaPixels.Y / ViewportHeightPx) * WorldHeight;

            var offset = new Vector3(dxWorld, dyWorld, 0);
            Position += offset;
            Target += offset;

            UpdateProjection(ViewportWidthPx / ViewportHeightPx);
        }

        public void PanWorld(Vector3 delta)
        {
            Position += delta;
            Target += delta;
            UpdateProjection(ViewportWidthPx / ViewportHeightPx);
        }

        public void ZoomToExtents(Extents extents, float padding = 1.05f)
        {
            PushState();
            float extW = (float)(extents.Max.X - extents.Min.X);
            float extH = (float)(extents.Max.Y - extents.Min.Y);

            if (extW < 1e-6f && extH < 1e-6f)
                return;

            float aspect = ViewportWidthPx / ViewportHeightPx;

            // Pick WorldWidth so both dimensions fit, then apply padding
            float widthFromW = extW * padding;
            float widthFromH = (extH * padding) * aspect;
            WorldWidth = Math.Max(widthFromW, widthFromH);
            WorldWidth = Math.Clamp(WorldWidth, 1e-3f, 1e9f);

            float cx = (float)((extents.Min.X + extents.Max.X) * 0.5);
            float cy = (float)((extents.Min.Y + extents.Max.Y) * 0.5);
            var offset = new Vector3(cx, cy, 0) - new Vector3(Target.X, Target.Y, 0);
            Position += offset;
            Target += offset;

            UpdateProjection(aspect);
        }

        public void ZoomToCenter(Point3D center, float worldWidth)
        {
            PushState();
            worldWidth = Math.Clamp(worldWidth, 1e-3f, 1e9f);
            WorldWidth = worldWidth;

            var offset = new Vector3((float)center.X, (float)center.Y, 0) - new Vector3(Target.X, Target.Y, 0);
            Position += offset;
            Target += offset;

            UpdateProjection(ViewportWidthPx / ViewportHeightPx);
        }

        // -------------------------
        // Screen ↔ World transforms
        // -------------------------
        public Vector3 ScreenToWorld(System.Drawing.Point screenDip)
        {
            float px = (float)(screenDip.X * DpiScaleX);
            float py = (float)(screenDip.Y * DpiScaleY);

            float ndcX = (px / ViewportWidthPx) * 2f - 1f;
            float ndcY = 1f - (py / ViewportHeightPx) * 2f;

            var clip = new Vector4(ndcX, ndcY, 0f, 1f);
            var world = Vector4.Transform(clip, InverseViewProjectionMatrix);

            return new Vector3(world.X, world.Y, Target.Z);
        }

        public System.Drawing.Point WorldToScreen(Vector3 world)
        {
            var clip = Vector4.Transform(new Vector4(world, 1f), ViewProjectionMatrix);
            var ndc = clip / clip.W;

            float px = (ndc.X + 1f) * 0.5f * ViewportWidthPx;
            float py = (1f - ndc.Y) * 0.5f * ViewportHeightPx;

            return new System.Drawing.Point((int)(px / DpiScaleX), (int)(py / DpiScaleY));
        }
    }
}
