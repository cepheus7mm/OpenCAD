using OpenCAD.Geometry.Helpers;
using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace OpenCAD.Geometry
{
    public readonly struct Point3D
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Point3D(Vector2 vec)
        {
            X = vec.X;
            Y = vec.Y;
            Z = 0;
        }

        public static Point3D Origin => new Point3D(0, 0, 0);

        public static Point3D NotAPoint => new Point3D(double.NaN, double.NaN, double.NaN);

        public static Point3D PositiveInfinity => new Point3D(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);

        public static Point3D NegativeInfinity => new Point3D(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);

        public bool IsValid =>
            double.IsFinite(X) &&
            double.IsFinite(Y) &&
            double.IsFinite(Z);

        public bool IsNotAPoint =>
            double.IsNaN(X) ||
            double.IsNaN(Y) ||
            double.IsNaN(Z);

        public override string ToString() => $"({X}, {Y}, {Z})";

        // --- Operators ---------------------------------------------------------

        // Point + Vector = Point
        public static Point3D operator +(Point3D p, Vector3D v)
            => new Point3D(p.X + v.X, p.Y + v.Y, p.Z + v.Z);

        // Point - Vector = Point
        public static Point3D operator -(Point3D p, Vector3D v)
            => new Point3D(p.X - v.X, p.Y - v.Y, p.Z - v.Z);

        // Point - Point = Vector
        public static Vector3D operator -(Point3D a, Point3D b)
            => new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        // --- Equality ----------------------------------------------------------

        public override bool Equals(object? obj)
            => obj is Point3D other &&
               X == other.X && Y == other.Y && Z == other.Z;

        public override int GetHashCode()
            => HashCode.Combine(X, Y, Z);

        public static bool operator ==(Point3D a, Point3D b)
            => a.Equals(b);

        public static bool operator !=(Point3D a, Point3D b)
            => !a.Equals(b);

        // --- Helpers -----------------------------------------------------------

        public static Point3D MidPoint(Point3D a, Point3D b)
            => new Point3D((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);

        public double DistanceTo(Point3D other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;
            double dz = other.Z - Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public double AngleTo(Point3D other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;
            return Math.Atan2(dy, dx);
        }

        public Vector3D AsVector3D()
            => new Vector3D(X, Y, Z);

        public Point3D Transform(Matrix4D m)
        {
            double x = X * m.M11 + Y * m.M21 + Z * m.M31 + m.M41;
            double y = X * m.M12 + Y * m.M22 + Z * m.M32 + m.M42;
            double z = X * m.M13 + Y * m.M23 + Z * m.M33 + m.M43;
            double w = X * m.M14 + Y * m.M24 + Z * m.M34 + m.M44;

            if (Math.Abs(w) > double.Epsilon && Math.Abs(w - 1.0) > double.Epsilon)
                return new Point3D(x / w, y / w, z / w);

            return new Point3D(x, y, z);
        }
    }
}