using System;
using System.Text.Json.Serialization;

namespace OpenCAD.Geometry
{
    public class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Distance from origin
        /// </summary>
        [JsonIgnore]
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>
        /// Distance between two points
        /// </summary>
        public double DistanceTo(Point3D other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;
            double dz = other.Z - Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Angle to another point in the XY plane
        /// </summary>
        /// <param name="other"></param>
        /// <returns>An angle in radians</returns>
        public double AngleTo(Point3D other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;
            return Math.Atan2(dy, dx);
        }

        public override string ToString() => $"({X}, {Y}, {Z})";

        public static Point3D operator +(Point3D a, Vector3D b) =>
            new Point3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Point3D operator -(Point3D a, Vector3D b) =>
            new Point3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3D operator -(Point3D a, Point3D b) =>
            new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public override bool Equals(object? obj)
        {
            if (obj is Point3D other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public static bool operator ==(Point3D? a, Point3D? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }

        public static bool operator !=(Point3D? a, Point3D? b)
        {
            return !(a == b);
        }

        public Point3D Clone() => new Point3D(X, Y, Z);

        public Vector3D AsVector3D() =>
            new Vector3D(X, Y, Z);

        public static Point3D Parse(string str)
        {
            // Expecting format "(x, y, z)"
            str = str.Trim('(', ')');
            var parts = str.Split(',');
            if (parts.Length != 3)
                throw new FormatException("Invalid Point3D format");
            double x = double.Parse(parts[0]);
            double y = double.Parse(parts[1]);
            double z = double.Parse(parts[2]);
            return new Point3D(x, y, z);
        }

        public static Point3D ParseFromPropertyString(string str)
        {
            var vec = Vector3D.ParseFromPropertyString(str);

            return new Point3D(vec.X, vec.Y, vec.Z);
        }
    }
}