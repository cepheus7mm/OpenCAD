using System;

namespace OpenCAD.Geometry.Helpers
{
    public class Vector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3D()
        {
            X = 0;
            Y = 0;
            Z = 0;
        }

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3D(Vector3D other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));
            X = other.X;
            Y = other.Y;
            Z = other.Z;
        }

        /// <summary>
        /// Distance from origin
        /// </summary>
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public double LengthSquared => Length * Length;

        public Vector3D Normalized => Length > 0 ? this / Length : new Vector3D(0, 0, 0);

        public override string ToString() => $"({X}, {Y}, {Z})";

        internal static Vector3D ParseFromPropertyString(string str)
        {
            // Example input: "X:1.23, Y:4.56, Z:7.89"
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentNullException(nameof(str));

            double x = 0, y = 0, z = 0;
            bool xFound = false, yFound = false, zFound = false;

            var parts = str.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("X:", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(trimmed.Substring(2).Trim(), out x))
                        xFound = true;
                }
                else if (trimmed.StartsWith("Y:", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(trimmed.Substring(2).Trim(), out y))
                        yFound = true;
                }
                else if (trimmed.StartsWith("Z:", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(trimmed.Substring(2).Trim(), out z))
                        zFound = true;
                }
            }

            if (!xFound || !yFound || !zFound)
                throw new FormatException("Invalid Vector3D property string format. Expected format: \"X:{x}, Y:{y}, Z:{z}\"");

            return new Vector3D(x, y, z);
        }

        public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        
        public static Vector3D operator -(Vector3D a, Vector3D b) => new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        
        public static Vector3D operator -(Vector3D a) => new Vector3D(-a.X, -a.Y, -a.Z);

        public static Vector3D operator *(Vector3D v, double scalar) => new Vector3D(v.X * scalar, v.Y * scalar, v.Z * scalar);

        public static Vector3D operator /(Vector3D v, double scalar)
        {
            if (Math.Abs(scalar) < double.Epsilon)
                throw new DivideByZeroException("Cannot divide by zero.");
            return new Vector3D(v.X / scalar, v.Y / scalar, v.Z / scalar);
        }

        /// <summary>
        /// Transform this vector/point by a double-precision 4x4 matrix.
        /// Treats the vector as a point (homogeneous w = 1) so translation is applied.
        /// Returns a new transformed Vector3D. If homogeneous w != 1, result is divided by w.
        /// </summary>
        public static Vector3D Transform(in Vector3D v, in Matrix4D m)
        {
            // Multiply as homogeneous coordinate (x,y,z,1)
            double x = v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + m.M41;
            double y = v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + m.M42;
            double z = v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + m.M43;
            double w = v.X * m.M14 + v.Y * m.M24 + v.Z * m.M34 + m.M44;

            if (Math.Abs(w - 1.0) > double.Epsilon && Math.Abs(w) > double.Epsilon)
            {
                return new Vector3D(x / w, y / w, z / w);
            }

            return new Vector3D(x, y, z);
        }

        public Vector3D? Rotate(double angle, Vector3D axis)
        {
            // Rodrigues' rotation formula
            double cosTheta = Math.Cos(angle);
            double sinTheta = Math.Sin(angle);
            Vector3D u = new Vector3D(axis);
            double length = u.Length;
            if (length < double.Epsilon)
                return null; // Invalid axis
            // Normalize axis
            u.X /= length;
            u.Y /= length;
            u.Z /= length;
            double dot = X * u.X + Y * u.Y + Z * u.Z;
            double rx = X * cosTheta + (u.Y * Z - u.Z * Y) * sinTheta + u.X * dot * (1 - cosTheta);
            double ry = Y * cosTheta + (u.Z * X - u.X * Z) * sinTheta + u.Y * dot * (1 - cosTheta);
            double rz = Z * cosTheta + (u.X * Y - u.Y * X) * sinTheta + u.Z * dot * (1 - cosTheta);
            return new Vector3D(rx, ry, rz);
        }

        internal static double Dot(Vector3D pointVec, Vector3D lineVec)
        {
            return pointVec.X * lineVec.X + pointVec.Y * lineVec.Y + pointVec.Z * lineVec.Z;
        }
    }
}