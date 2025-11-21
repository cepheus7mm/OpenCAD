using System;

namespace OpenCAD.Geometry
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

        /// <summary>
        /// Distance from origin
        /// </summary>
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

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
    }
}