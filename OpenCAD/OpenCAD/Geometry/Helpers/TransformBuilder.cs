using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers
{
    public static class TransformBuilder
    {
        //
        // TRANSLATION
        //
        public static Matrix4D Translate(Vector2 delta)
            => Matrix4D.CreateTranslation(delta.X, delta.Y, 0);

        public static Matrix4D Translate(double x, double y)
            => Matrix4D.CreateTranslation(x, y, 0);

        public static Matrix4D Translate(double x, double y, double z)
            => Matrix4D.CreateTranslation(x, y, z);


        //
        // ROTATION AROUND A PIVOT (CCW, +Z)
        //
        public static Matrix4D Rotate(Vector2 pivot, double radians)
        {
            var t1 = Matrix4D.CreateTranslation(-pivot.X, -pivot.Y, 0);
            var r = Matrix4D.CreateRotationZ(radians);
            var t2 = Matrix4D.CreateTranslation(pivot.X, pivot.Y, 0);

            return t1 * r * t2;
        }


        //
        // UNIFORM SCALE AROUND A PIVOT
        //
        public static Matrix4D Scale(Vector2 pivot, double scale)
        {
            var t1 = Matrix4D.CreateTranslation(-pivot.X, -pivot.Y, 0);
            var s = Matrix4D.CreateScale(scale);
            var t2 = Matrix4D.CreateTranslation(pivot.X, pivot.Y, 0);

            return t1 * s * t2;
        }


        //
        // NON-UNIFORM SCALE AROUND A PIVOT
        //
        public static Matrix4D Scale(Vector2 pivot, double sx, double sy)
        {
            var t1 = Matrix4D.CreateTranslation(-pivot.X, -pivot.Y, 0);
            var s = Matrix4D.CreateScale(sx, sy, 1);
            var t2 = Matrix4D.CreateTranslation(pivot.X, pivot.Y, 0);

            return t1 * s * t2;
        }


        //
        // MIRROR ACROSS AN ARBITRARY 2D LINE (p0 → p1)
        //
        public static Matrix4D Mirror(Vector2 p0, Vector2 p1)
        {
            // Direction of mirror line
            Vector2 d = p1 - p0;
            d = Vector2.Normalize(d);

            double dx = d.X;
            double dy = d.Y;

            // Reflection matrix around a line through the origin
            // Matches your row-vector transform convention
            var reflect = new Matrix4D(
                dx * dx - dy * dy, 2 * dx * dy, 0, 0,
                2 * dx * dy, dy * dy - dx * dx, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            );

            // Move line to origin → reflect → move back
            var t1 = Matrix4D.CreateTranslation(-p0.X, -p0.Y, 0);
            var t2 = Matrix4D.CreateTranslation(p0.X, p0.Y, 0);

            return t1 * reflect * t2;
        }
    }
}
