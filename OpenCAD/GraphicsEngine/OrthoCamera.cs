using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine
{
    public sealed class OrthoCamera
    {
        public Vector3 Position;   // eye in world space
        public Vector3 Target;     // look-at point
        public Vector3 Up;         // up vector

        public float OrthoWidth;   // world units across horizontally
        public float Near = -1000f;
        public float Far = 1000f;

        public OrthoCamera()
        {
            Position = new Vector3(0, 0, 10);   // looking down -Z or +Z depending on your convention
            Target = new Vector3(0, 0, 0);
            Up = new Vector3(0, 1, 0);
            OrthoWidth = 100; // some default extent
        }

        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Position, Target, Up);
        }

        public Matrix4x4 GetProjectionMatrix(float aspect)
        {
            float halfWidth = OrthoWidth * 0.5f;
            float halfHeight = halfWidth / aspect;

            return Matrix4x4.CreateOrthographicOffCenter(
                -halfWidth, halfWidth,
                -halfHeight, halfHeight,
                Near, Far);
        }
        public void Zoom(float zoomFactor)
        {
            // zoomFactor > 1 = zoom in, < 1 = zoom out
            var minWidth = 1f;   // prevent zooming in too far
            var maxWidth = 1000000000f; // prevent zooming out too far
            OrthoWidth /= zoomFactor;
            OrthoWidth = Math.Clamp(OrthoWidth, minWidth, maxWidth);
        }
        public void Pan(Vector2 deltaPixels, float viewportWidthPixels, float viewportHeightPixels)
        {
            // Convert screen delta to NDC [-1, 1] range
            float dxNdc = 2f * deltaPixels.X / viewportWidthPixels;
            float dyNdc = -2f * deltaPixels.Y / viewportHeightPixels; // invert Y for screen → world

            // Convert NDC to world units using current ortho extent
            float halfWidth = OrthoWidth * 0.5f;
            float halfHeight = halfWidth / (viewportWidthPixels / viewportHeightPixels);

            float dxWorld = dxNdc * halfWidth;
            float dyWorld = dyNdc * halfHeight;

            var offset = new Vector3(dxWorld, dyWorld, 0);

            Position += offset;
            Target += offset;
        }
        public void PanWorld(Vector3 delta)
        {
            Position += delta;
            Target += delta;
        }
    }
}
