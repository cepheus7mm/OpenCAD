using GraphicsEngine.Interfaces;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine
{
    public sealed class OrthoCamera : ICamera
    {
        public Vector3 Position { get; set; }   // eye in world space
        public Vector3 Target { get; set; }     // look-at point
        public Vector3 Up { get; set; }         // up vector

        public float WorldWidth { get; private set; } = 40.0f;  // world units across horizontally
        public float WorldHeight { get; private set; }   // world units across vertically
        public float Near = -1000f;
        public float Far = 1000f;

        public float PanScale => WorldWidth;
        public Matrix4x4 ViewMatrix => GetViewMatrix();

        public Matrix4x4 ProjectionMatrix { get; private set; }

        public OrthoCamera(float width)
        {
            Position = new Vector3(0, 0, 10);   // looking down -Z or +Z depending on your convention
            Target = new Vector3(0, 0, 0);
            Up = new Vector3(0, 1, 0);
            WorldWidth = width;
        }

        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Position, Target, Up);
        }

        public void SetViewportSize(float width, float height)
        {
            WorldWidth = width;
            WorldHeight = height;
        }

        public Matrix4x4 GetProjectionMatrix()
        {
            var aspect = WorldWidth / WorldHeight;
            float halfWidth = WorldWidth * 0.5f;
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
            WorldWidth /= zoomFactor;
            WorldWidth = Math.Clamp(WorldWidth, minWidth, maxWidth);
        }

        public void Pan(Vector2 deltaPixels)
        {
            // Convert screen delta to NDC [-1, 1] range
            float dxNdc = 2f * deltaPixels.X / WorldWidth;
            float dyNdc = 2f * deltaPixels.Y / WorldHeight; // invert Y for screen → world

            // Convert NDC to world units using current ortho extent
            float halfWidth = WorldWidth * 0.5f;
            float halfHeight = halfWidth / (WorldWidth / WorldHeight);

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

        public void UpdateProjection(float aspect)
        {
            WorldHeight = WorldWidth / aspect;

            ProjectionMatrix = Matrix4x4.CreateOrthographic(
                WorldWidth,
                WorldHeight,
                Near,
                Far
            );
        }
    }
}
