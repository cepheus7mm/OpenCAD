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
    }
}