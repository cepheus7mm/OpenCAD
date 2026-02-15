using System;

namespace OpenCAD.Geometry.Helpers
{
    /// <summary>
    /// 4x4 matrix using double precision.
    /// Provides a subset of System.Numerics.Matrix4x4 functionality but with doubles.
    /// </summary>
    public readonly struct Matrix4D
    {
        public double M11 { get; init; }
        public double M12 { get; init; }
        public double M13 { get; init; }
        public double M14 { get; init; }

        public double M21 { get; init; }
        public double M22 { get; init; }
        public double M23 { get; init; }
        public double M24 { get; init; }

        public double M31 { get; init; }
        public double M32 { get; init; }
        public double M33 { get; init; }
        public double M34 { get; init; }

        public double M41 { get; init; }
        public double M42 { get; init; }
        public double M43 { get; init; }
        public double M44 { get; init; }

        public static Matrix4D Identity => new Matrix4D
        {
            M11 = 1,
            M22 = 1,
            M33 = 1,
            M44 = 1
        };

        public bool IsMirror
        {
            get
            {
                // Extract the 3×3 linear part
                double a = M11, b = M12, c = M13;
                double d = M21, e = M22, f = M23;
                double g = M31, h = M32, i = M33;

                // Compute determinant of the 3×3
                double det =
                    a * (e * i - f * h) -
                    b * (d * i - f * g) +
                    c * (d * h - e * g);

                return det < 0;
            }
        }

        public Matrix4D(
            double m11, double m12, double m13, double m14,
            double m21, double m22, double m23, double m24,
            double m31, double m32, double m33, double m34,
            double m41, double m42, double m43, double m44)
        {
            M11 = m11; M12 = m12; M13 = m13; M14 = m14;
            M21 = m21; M22 = m22; M23 = m23; M24 = m24;
            M31 = m31; M32 = m32; M33 = m33; M34 = m34;
            M41 = m41; M42 = m42; M43 = m43; M44 = m44;
        }

        public static Matrix4D CreateTranslation(double x, double y, double z) =>
            new Matrix4D(
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                x, y, z, 1);

        public static Matrix4D CreateScale(double scale) =>
            CreateScale(scale, scale, scale);

        public static Matrix4D CreateScale(double scaleX, double scaleY, double scaleZ) =>
            new Matrix4D(
                scaleX, 0, 0, 0,
                0, scaleY, 0, 0,
                0, 0, scaleZ, 0,
                0, 0, 0, 1);

        public static Matrix4D CreateRotationZ(double radians)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);

            // Matrix layout and Transform convention in this code use:
            // x' = x*M11 + y*M21 + z*M31 + M41
            // y' = x*M12 + y*M22 + z*M32 + M42
            // For standard CCW rotation about +Z (when looking down +Z),
            // we want x' = cos*x - sin*y ; y' = sin*x + cos*y
            // With our Transform convention this corresponds to:
            // M11 = cos, M21 = -sin
            // M12 = sin, M22 =  cos
            return new Matrix4D(
                c, s, 0, 0,
               -s, c, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1);
        }

        public static Matrix4D operator *(in Matrix4D left, in Matrix4D right)
        {
            return Multiply(left, right);
        }

        public static Matrix4D Multiply(in Matrix4D a, in Matrix4D b)
        {
            return new Matrix4D(
                a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
                a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
                a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
                a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,

                a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
                a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
                a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
                a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,

                a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
                a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
                a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
                a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,

                a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
                a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
                a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
                a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44
            );
        }

        /// <summary>
        /// Transform a 3D point (treated as w=1) by this matrix.
        /// </summary>
        public Point3D Transform(Point3D p)
        {
            double x = p.X * M11 + p.Y * M21 + p.Z * M31 + M41;
            double y = p.X * M12 + p.Y * M22 + p.Z * M32 + M42;
            double z = p.X * M13 + p.Y * M23 + p.Z * M33 + M43;
            double w = p.X * M14 + p.Y * M24 + p.Z * M34 + M44;

            if (Math.Abs(w - 1.0) > double.Epsilon && Math.Abs(w) > double.Epsilon)
            {
                return new Point3D(x / w, y / w, z / w);
            }

            return new Point3D(x, y, z);
        }

        /// <summary>
        /// Transforms a scalar value (e.g., radius) by the uniform scale of this matrix.
        /// If the matrix is not uniformly scaled, the X scale is used.
        /// </summary>
        public double Transform(double value)
        {
            // Compute the scale as the length of the first column (ignoring translation)
            double scale = Math.Sqrt(M11 * M11 + M21 * M21 + M31 * M31);
            return value * scale;
        }

        /// <summary>
        /// Transform a vector (treated as w=0) by this matrix.
        /// Translation component is ignored.
        /// </summary>
        public Vector3D TransformVector(Vector3D v)
        {
            double x = v.X * M11 + v.Y * M21 + v.Z * M31;
            double y = v.X * M12 + v.Y * M22 + v.Z * M32;
            double z = v.X * M13 + v.Y * M23 + v.Z * M33;
            return new Vector3D(x, y, z);
        }

        /// <summary>
        /// Attempts to invert the matrix. Returns false if matrix is not invertible.
        /// Implementation adapted for doubles.
        /// </summary>
        public static bool TryInvert(in Matrix4D matrix, out Matrix4D result)
        {
            // Use Gauss-Jordan or analytic inverse. Here we compute adjugate / determinant.
            double a00 = matrix.M11, a01 = matrix.M12, a02 = matrix.M13, a03 = matrix.M14;
            double a10 = matrix.M21, a11 = matrix.M22, a12 = matrix.M23, a13 = matrix.M24;
            double a20 = matrix.M31, a21 = matrix.M32, a22 = matrix.M33, a23 = matrix.M34;
            double a30 = matrix.M41, a31 = matrix.M42, a32 = matrix.M43, a33 = matrix.M44;

            double b00 = a00 * a11 - a01 * a10;
            double b01 = a00 * a12 - a02 * a10;
            double b02 = a00 * a13 - a03 * a10;
            double b03 = a01 * a12 - a02 * a11;
            double b04 = a01 * a13 - a03 * a11;
            double b05 = a02 * a13 - a03 * a12;
            double b06 = a20 * a31 - a21 * a30;
            double b07 = a20 * a32 - a22 * a30;
            double b08 = a20 * a33 - a23 * a30;
            double b09 = a21 * a32 - a22 * a31;
            double b10 = a21 * a33 - a23 * a31;
            double b11 = a22 * a33 - a23 * a32;

            double det = b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;

            if (Math.Abs(det) < double.Epsilon)
            {
                result = default;
                return false;
            }

            double invDet = 1.0 / det;

            result = new Matrix4D
            {
                M11 = (a11 * b11 - a12 * b10 + a13 * b09) * invDet,
                M12 = (-a01 * b11 + a02 * b10 - a03 * b09) * invDet,
                M13 = (a31 * b05 - a32 * b04 + a33 * b03) * invDet,
                M14 = (-a21 * b05 + a22 * b04 - a23 * b03) * invDet,

                M21 = (-a10 * b11 + a12 * b08 - a13 * b07) * invDet,
                M22 = (a00 * b11 - a02 * b08 + a03 * b07) * invDet,
                M23 = (-a30 * b05 + a32 * b02 - a33 * b01) * invDet,
                M24 = (a20 * b05 - a22 * b02 + a23 * b01) * invDet,

                M31 = (a10 * b10 - a11 * b08 + a13 * b06) * invDet,
                M32 = (-a00 * b10 + a01 * b08 - a03 * b06) * invDet,
                M33 = (a30 * b04 - a31 * b02 + a33 * b00) * invDet,
                M34 = (-a20 * b04 + a21 * b02 - a23 * b00) * invDet,

                M41 = (-a10 * b09 + a11 * b07 - a12 * b06) * invDet,
                M42 = (a00 * b09 - a01 * b07 + a02 * b06) * invDet,
                M43 = (-a30 * b03 + a31 * b01 - a32 * b00) * invDet,
                M44 = (a20 * b03 - a21 * b01 + a22 * b00) * invDet
            };

            return true;
        }

        /// <summary>
        /// Create a uniform scale matrix around basePoint using the distance base->target as scale factor.
        /// Returns false if scale is zero/near-zero.
        /// </summary>
        public static bool TryCreateUniformScaleMatrix(Point3D basePoint, Point3D targetPoint, out Matrix4D matrix)
        {
            double dx = targetPoint.X - basePoint.X;
            double dy = targetPoint.Y - basePoint.Y;
            double dz = targetPoint.Z - basePoint.Z;
            double scale = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (scale <= double.Epsilon)
            {
                matrix = default;
                return false;
            }

            var toOrigin = CreateTranslation(-basePoint.X, -basePoint.Y, -basePoint.Z);
            var scaleMat = CreateScale(scale);
            var back = CreateTranslation(basePoint.X, basePoint.Y, basePoint.Z);
            matrix = toOrigin * scaleMat * back;
            return true;
        }

        /// <summary>
        /// Create a rotation matrix about Z using the angle from basePoint to targetPoint.
        /// Returns false if angle is invalid (NaN/Inf).
        /// </summary>
        public static bool TryCreateRotationMatrix(Point3D basePoint, Point3D targetPoint, out Matrix4D matrix)
        {
            double dx = targetPoint.X - basePoint.X;
            double dy = targetPoint.Y - basePoint.Y;

            double angleRad = Math.Atan2(dy, dx);

            if (double.IsNaN(angleRad) || double.IsInfinity(angleRad))
            {
                matrix = default;
                return false;
            }

            var toOrigin = CreateTranslation(-basePoint.X, -basePoint.Y, -basePoint.Z);
            var rot = CreateRotationZ(angleRad);
            var back = CreateTranslation(basePoint.X, basePoint.Y, basePoint.Z);
            matrix = toOrigin * rot * back;
            return true;
        }

        public override string ToString()
        {
            return $"[{M11:F6} {M12:F6} {M13:F6} {M14:F6}; {M21:F6} {M22:F6} {M23:F6} {M24:F6}; {M31:F6} {M32:F6} {M33:F6} {M34:F6}; {M41:F6} {M42:F6} {M43:F6} {M44:F6}]";
        }

        public static double[] ToArray (in Matrix4D matrix)
        {
            return
            [
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            ];
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                ToArray(this));
        }

        public override bool Equals(object? obj)
        {
            if (obj is Matrix4D other)
            {
                return M11 == other.M11 && M12 == other.M12 && M13 == other.M13 && M14 == other.M14 &&
                       M21 == other.M21 && M22 == other.M22 && M23 == other.M23 && M24 == other.M24 &&
                       M31 == other.M31 && M32 == other.M32 && M33 == other.M33 && M34 == other.M34 &&
                       M41 == other.M41 && M42 == other.M42 && M43 == other.M43 && M44 == other.M44;
            }
            return false;
        }

        public static bool TryCreateTranslation(Point3D basePoint, Point3D targetPoint, out Matrix4D translation)
        {
            translation = CreateTranslation(
                targetPoint.X - basePoint.X,
                targetPoint.Y - basePoint.Y,
                targetPoint.Z - basePoint.Z);
            return IsInvertible(translation);
        }

        public static bool IsInvertible(Matrix4D translation)
        {
            return TryInvert(translation, out _);
        }

        public static bool operator ==(Matrix4D? a, Matrix4D? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }

        public static bool operator !=(Matrix4D? a, Matrix4D? b)
        {
            return !(a == b);
        }
    }
}