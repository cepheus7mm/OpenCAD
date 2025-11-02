using System.Numerics;

namespace GraphicsEngine
{
    /// <summary>
    /// Camera for 3D navigation
    /// </summary>
    public class Camera
    {
        public Vector3 Position { get; set; } = new Vector3(0, 0, 10);
        public Vector3 Target { get; set; } = Vector3.Zero;
        public Vector3 Up { get; set; } = Vector3.UnitY;

        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Position, Target, Up);
        }

        public void Orbit(float deltaX, float deltaY)
        {
            float radius = (Position - Target).Length();
            float theta = MathF.Atan2(Position.Z - Target.Z, Position.X - Target.X);
            float phi = MathF.Acos((Position.Y - Target.Y) / radius);

            theta += deltaX;
            phi += deltaY;
            phi = Math.Clamp(phi, 0.1f, MathF.PI - 0.1f);

            Position = new Vector3(
                Target.X + radius * MathF.Sin(phi) * MathF.Cos(theta),
                Target.Y + radius * MathF.Cos(phi),
                Target.Z + radius * MathF.Sin(phi) * MathF.Sin(theta)
            );
        }

        public void Zoom(float delta)
        {
            Vector3 direction = Vector3.Normalize(Target - Position);
            Position += direction * delta;
        }

        public void Pan(float deltaX, float deltaY)
        {
            // Calculate the view direction (normalized)
            Vector3 viewDir = Vector3.Normalize(Position - Target);
            
            // Calculate right vector (perpendicular to view direction and up)
            Vector3 right = Vector3.Normalize(Vector3.Cross(Up, viewDir));
            
            // Calculate the actual up vector (perpendicular to both)
            Vector3 actualUp = Vector3.Normalize(Vector3.Cross(viewDir, right));

            // Move both position and target by the same amount
            // This creates a "slide" effect perfect for general (perspective) views
            Vector3 offset = right * deltaX + actualUp * deltaY;
            
            Position += offset;
            Target += offset;
        }

        public void PanOrthoWorld(float deltaX, float deltaY)
        {
            // Translate strictly in world XY; keep Up axis locked to +Y
            Vector3 offset = new Vector3(deltaX, deltaY, 0f);
            Position += offset;
            Target += offset;
            Up = Vector3.UnitY;
        }
    }
}