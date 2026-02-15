using System;

namespace OpenCAD.Geometry.Helpers
{
    public readonly struct Vector3D
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Vector3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3D Zero => new Vector3D(0, 0, 0);
        public static Vector3D UnitX => new Vector3D(1, 0, 0);
        public static Vector3D UnitY => new Vector3D(0, 1, 0);
        public static Vector3D UnitZ => new Vector3D(0, 0, 1);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public double LengthSquared => X * X + Y * Y + Z * Z;

        public Vector3D Normalized
        {
            get
            {
                double len = Length;
                return len > 0 ? new Vector3D(X / len, Y / len, Z / len)
                               : Zero;
            }
        }

        public override string ToString() => $"({X}, {Y}, {Z})";

        public static Vector3D operator +(Vector3D a, Vector3D b)
            => new Vector3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vector3D operator -(Vector3D a, Vector3D b)
            => new Vector3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3D operator -(Vector3D a)
            => new Vector3D(-a.X, -a.Y, -a.Z);

        public static Vector3D operator *(Vector3D v, double s)
            => new Vector3D(v.X * s, v.Y * s, v.Z * s);

        public static Vector3D operator /(Vector3D v, double s)
        {
            if (Math.Abs(s) < double.Epsilon)
                throw new DivideByZeroException();
            return new Vector3D(v.X / s, v.Y / s, v.Z / s);
        }

        public override bool Equals(object? obj)
            => obj is Vector3D other &&
               X == other.X && Y == other.Y && Z == other.Z;

        public override int GetHashCode()
            => HashCode.Combine(X, Y, Z);

        public static bool operator ==(Vector3D a, Vector3D b)
            => AreAlmostEqual(a.X, b.X) &&
               AreAlmostEqual(a.Y, b.Y) &&
               AreAlmostEqual(a.Z, b.Z);

        public static bool operator !=(Vector3D a, Vector3D b)
            => !(a == b);

        public static double Dot(Vector3D a, Vector3D b)
            => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3D Cross(Vector3D a, Vector3D b)
            => new Vector3D(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);

        public double Dot(Vector3D other)
            => Dot(this, other);

        public double DistanceTo(Vector3D other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;
            double dz = other.Z - Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public Vector3D Rotate(double angle, Vector3D axis)
        {
            double len = axis.Length;
            if (len < double.Epsilon)
                return this;

            Vector3D u = axis / len;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            double dot = Dot(this, u);

            double rx = X * cos + (u.Y * Z - u.Z * Y) * sin + u.X * dot * (1 - cos);
            double ry = Y * cos + (u.Z * X - u.X * Z) * sin + u.Y * dot * (1 - cos);
            double rz = Z * cos + (u.X * Y - u.Y * X) * sin + u.Z * dot * (1 - cos);

            return new Vector3D(rx, ry, rz);
        }

        private static bool AreAlmostEqual(double a, double b, double tolerance = 1e-12)
            => Math.Abs(a - b) < tolerance;

        public bool IsZeroLength()
        {
            return AreAlmostEqual(0, X) && AreAlmostEqual(0, Y);
        }
    }
}