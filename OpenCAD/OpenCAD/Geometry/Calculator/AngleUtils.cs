using System;

namespace OpenCAD.Geometry.Calculator
{
    internal static class AngleUtils
    {
        private const double Pi = Math.PI;
        private const double TwoPi = 2.0 * Pi;

        public static double[] Cardinals = {0.0, Pi / 2.0, Pi, 3.0 * Pi / 2.0};

        // Principal angle: (-π, π]
        public static double NormalizeSigned(double a)
        {
            a %= TwoPi;
            if (a > Pi) a -= TwoPi;
            if (a <= -Pi) a += TwoPi;
            return a;
        }

        // Unsigned angle: [0, 2π)
        public static double NormalizeUnsigned(double a)
        {
            a %= TwoPi;
            if (a < 0) a += TwoPi;
            return a;
        }

        // CCW sweep: [0, 2π)
        public static double NormalizeSweepCCW(double sweep)
        {
            sweep %= TwoPi;
            if (sweep < 0) sweep += TwoPi;
            return sweep;
        }

        public static bool SweepContains(double startAngle, double sweep, double a)
        {
            double endAngle = startAngle + sweep;
            endAngle = NormalizeUnsigned(endAngle);
            a = NormalizeUnsigned(a);

            if (sweep > 0)
            {
                // CCW sweep
                if (endAngle < startAngle)
                    endAngle += 2.0 * Math.PI;

                if (a < startAngle)
                    a += 2.0 * Math.PI;

                return a >= startAngle && a <= endAngle;
            }
            else
            {
                // CW sweep
                if (startAngle < endAngle)
                    startAngle += 2.0 * Math.PI;

                if (a < endAngle)
                    a += 2.0 * Math.PI;

                return a <= startAngle && a >= endAngle;
            }
        }
    }
}