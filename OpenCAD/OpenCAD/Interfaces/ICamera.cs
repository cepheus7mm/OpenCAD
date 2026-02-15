using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenCAD.Interfaces
{
    public interface ICamera
    {
        // Existing members
        Matrix4x4 ViewMatrix { get; }
        Matrix4x4 ProjectionMatrix { get; }

        Vector3 Position { get; }
        Vector3 Target { get; }
        Vector3 Up { get; }

        void Pan(Vector2 deltaPixels);
        void Zoom(float zoomFactor);

        float PanScale { get; }

        void SetViewportSize(float widthPixels, float heightPixels);
        void UpdateProjection(float aspect);

        // NEW: viewport + DPI
        float ViewportWidthPx { get; }
        float ViewportHeightPx { get; }
        float DpiScaleX { get; }
        float DpiScaleY { get; }

        // NEW: combined matrices
        Matrix4x4 ViewProjectionMatrix { get; }
        Matrix4x4 InverseViewProjectionMatrix { get; }

        // NEW: canonical transforms
        Vector3 ScreenToWorld(Point screenDip);
        Point WorldToScreen(Vector3 world);
    }
}
