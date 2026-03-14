using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Calculator
{
    public readonly struct GeoSegment
    {
        public readonly Vector2 A;
        public readonly Vector2 B;

        public GeoSegment(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }
    }

    public static class CurveTessellator
    {
        /// <summary>
        /// Tessellates an arc into straight segments based on a sagitta tolerance.
        /// Angles in radians, CCW, world space.
        /// </summary>
        public static void TessellateArc(
            Vector2 center,
            float radius,
            float startAngle,
            float sweep,
            float maxSagitta,              // in world units
            List<GeoSegment> output)
        {
            //float sweep = endAngle - startAngle;
            float absSweep = MathF.Abs(sweep);

            if (absSweep < 1e-6f || radius <= 0f)
                return;

            // Clamp sagitta to something reasonable
            maxSagitta = MathF.Max(maxSagitta, 1e-6f);

            // Compute angle per segment from sagitta
            // sagitta = R - R * cos(θ/2) => cos(θ/2) = 1 - sagitta/R
            float cosHalf = 1f - maxSagitta / radius;
            cosHalf = Math.Clamp(cosHalf, -1f, 1f);

            float anglePerSegment = 2f * MathF.Acos(cosHalf);
            if (anglePerSegment <= 0f || float.IsNaN(anglePerSegment))
                anglePerSegment = absSweep; // fallback: single segment

            int segments = Math.Max(1, (int)MathF.Ceiling(absSweep / anglePerSegment));
            float step = sweep / segments;

            Vector2 prev = center + radius * new Vector2(
                MathF.Cos(startAngle),
                MathF.Sin(startAngle)
            );

            for (int i = 1; i <= segments; i++)
            {
                float angle = startAngle + step * i;

                Vector2 next = center + radius * new Vector2(
                    MathF.Cos(angle),
                    MathF.Sin(angle)
                );

                output.Add(new GeoSegment(prev, next));
                prev = next;
            }
        }

        /// <summary>
        /// Tessellates a full circle using the same arc logic.
        /// </summary>
        public static void TessellateCircle(
            Vector2 center,
            float radius,
            float maxSagitta,
            List<GeoSegment> output)
        {
            TessellateArc(center, radius, 0f, MathF.Tau, maxSagitta, output);
        }
    }
}
