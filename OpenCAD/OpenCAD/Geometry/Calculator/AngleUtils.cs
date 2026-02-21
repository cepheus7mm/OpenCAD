using System;

namespace OpenCAD.Geometry.Calculator
{
    internal static class AngleUtils
    {
        public const double Pi = Math.PI;
        public const double TwoPi = 2.0 * Math.PI;

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
            if (a < 0)
                a += TwoPi;

            // Clamp 2π → 0
            if (Math.Abs(a - TwoPi) < 1e-7)
                a = 0;

            return a;
        }

        public static double NormalizeAnglePositive(double a)
        {
            a %= TwoPi;
            if (a < 0) a += TwoPi;
            return a;
        }

        public static double NormalizeAngleNegative(double a)
        {
            a %= TwoPi;
            if (a > 0) a -= TwoPi;
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

        public static double ClampAngleToSweep(double startAngle, double sweep, double angle)
        {
            // Normalize everything to [0, 2π)
            startAngle = NormalizeUnsigned(startAngle);
            angle = NormalizeUnsigned(angle);

            double endAngle = NormalizeUnsigned(startAngle + sweep);

            // Degenerate sweep → clamp to start
            if (Math.Abs(sweep) < 1e-12)
                return startAngle;

            // -----------------------------
            // CCW sweep (positive)
            // -----------------------------
            if (sweep > 0)
            {
                double a = angle;

                // Handle wrap-around
                if (endAngle < startAngle)
                    endAngle += TwoPi;

                if (a < startAngle)
                    a += TwoPi;

                if (a < startAngle)
                    return startAngle;

                if (a > endAngle)
                    return endAngle;

                return NormalizeUnsigned(a);
            }

            // -----------------------------
            // CW sweep (negative)
            // -----------------------------
            else
            {
                double a = angle;

                // Handle wrap-around
                if (startAngle < endAngle)
                    startAngle += TwoPi;

                if (a < endAngle)
                    a += TwoPi;

                if (a > startAngle)
                    return startAngle;

                if (a < endAngle)
                    return endAngle;

                return NormalizeUnsigned(a);
            }
        }
    }
}