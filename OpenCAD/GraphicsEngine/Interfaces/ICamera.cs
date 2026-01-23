using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine.Interfaces
{
    public interface ICamera
    {
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
    }
}
