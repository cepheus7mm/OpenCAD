using GraphicsEngine.Interfaces;
using OpenCAD.Interfaces;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace GraphicsEngine
{
    public sealed class Viewport
    {
        DpiScale _dpiScale;
        public int PixelWidth { get; private set; }
        public int PixelHeight { get; private set; }

        public float DpiScaleX { get; private set; }
        public float DpiScaleY { get; private set; }

        public Matrix4x4 ViewMatrix => Camera?.ViewMatrix ?? Matrix4x4.Identity;
        public Matrix4x4 ProjectionMatrix => Camera?.ProjectionMatrix ?? Matrix4x4.Identity;
        public Matrix4x4 ViewProjectionMatrix => ViewMatrix * ProjectionMatrix;

        public ICamera Camera { get; set; }

        private readonly GLWpfControl _glControl;

        public Viewport(GLWpfControl glControl)
        {
            _glControl = glControl ?? throw new ArgumentNullException(nameof(glControl));

            UpdateDpi();
            UpdatePixelSize();
        }

        // ------------------------------------------------------------
        // DPI + Pixel Size
        // ------------------------------------------------------------
        public void UpdateDpi()
        {
            var source = PresentationSource.FromVisual(_glControl);
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformToDevice;
                DpiScaleX = (float)m.M11;
                DpiScaleY = (float)m.M22;
            }
        }

        public void UpdatePixelSize()
        {
            PixelWidth = Math.Max(1, (int)Math.Round(_glControl.ActualWidth * DpiScaleX));
            PixelHeight = Math.Max(1, (int)Math.Round(_glControl.ActualHeight * DpiScaleY));
        }

        // ------------------------------------------------------------
        // Coordinate transforms
        // ------------------------------------------------------------
        public Vector2 WorldToNdc(Vector2 p)
        {
            // 1. Build a world-space homogeneous point
            Vector4 world = new Vector4(p.X, p.Y, 0f, 1f);

            // 2. Transform through View and Projection
            Matrix4x4 vp = Camera.ViewMatrix * Camera.ProjectionMatrix;
            Vector4 clip = Vector4.Transform(world, vp);

            // 3. Perspective divide → NDC
            float invW = 1f / clip.W;
            return new Vector2(clip.X * invW, clip.Y * invW);
        }

        public Vector2 NdcToScreen(Vector2 ndc)
        {
            float x = (ndc.X * 0.5f + 0.5f) * PixelWidth;
            float y = (1.0f - (ndc.Y * 0.5f + 0.5f)) * PixelHeight;
            return new Vector2(x, y);
        }

        public Vector2 ScreenToNdc(Vector2 screen)
        {
            float x = (screen.X / PixelWidth) * 2f - 1f;
            float y = 1f - (screen.Y / PixelHeight) * 2f;
            return new Vector2(x, y);
        }

        public Vector2 ScreenToWorld(Vector2 screen)
        {
            Vector2 ndc = ScreenToNdc(screen);
            var inv = Matrix4x4.Invert(ViewProjectionMatrix, out var invVP)
                ? invVP
                : Matrix4x4.Identity;

            var v = Vector4.Transform(new Vector4(ndc, 0, 1), inv);
            return new Vector2(v.X / v.W, v.Y / v.W);
        }
        public Vector2 WorldToScreen(Vector2 world)
        {
            // 1. World → Clip
            var v = new Vector4(world, 0, 1);
            var clip = Vector4.Transform(v, ViewProjectionMatrix);

            // 2. Perspective divide → NDC (-1..1)
            if (clip.W != 0)
                clip /= clip.W;

            // 3. NDC → Screen
            float x = (clip.X * 0.5f + 0.5f) * PixelWidth;
            float y = (1f - (clip.Y * 0.5f + 0.5f)) * PixelHeight;

            return new Vector2(x, y);
        }

        // ------------------------------------------------------------
        // Resize handler
        // ------------------------------------------------------------
        public void OnSizeChanged()
        {
            UpdateDpi();
            UpdatePixelSize();
        }

        public float MillimetersToPixels(float mm)
        {
            var pixels = (Math.Abs(mm) / 25.4f) * (float)_dpiScale.PixelsPerInchX;
            return Math.Max(1f, Math.Min(pixels, 200.0f));
        }
        public float PixelsToWorld(float pixels)
        {
            var camera = Camera as OrthoCamera;
            if (camera == null)
                throw new InvalidOperationException("PixelsToWorld is only valid in orthographic mode.");

            if (PixelWidth <= 0 || PixelHeight <= 0)
                return 0f;

            // world units per pixel (use Y for stability)
            float worldPerPixel = camera.WorldHeight / PixelHeight;

            return pixels * worldPerPixel;
        }
    }
}
