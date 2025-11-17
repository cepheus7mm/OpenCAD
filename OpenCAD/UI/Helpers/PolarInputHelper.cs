using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenCAD.Geometry;

namespace UI.Helpers
{
    public class PolarInputHelper
    {
        public double Distance { get; private set; }
        public double Angle { get; private set; } // In decimal degrees or radians
        public Vector3D Vector { get; private set; }
        public bool IsValid { get; private set; }

        public PolarInputHelper(string input)
        {
            IsValid = TryParse(input);
        }

        private bool TryParse(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            // Match @distance<angle>
            var match = Regex.Match(input, @"@(?<distance>[^<]+)<(?<angle>.+)");
            if (!match.Success) return false;

            // Parse distance
            Distance = ParseDistance(match.Groups["distance"].Value);
            if (double.IsNaN(Distance)) return false;

            // Parse angle
            Angle = ParseAngle(match.Groups["angle"].Value);
            if (double.IsNaN(Angle)) return false;

            Vector = ComputeVector(Distance, Angle);
            return true;
        }

        private double ParseDistance(string distanceRaw)
        {
            if (string.IsNullOrWhiteSpace(distanceRaw))
                return double.NaN;

            // Normalize symbols
            distanceRaw = distanceRaw.Replace("'", "f").Replace("\"", "i").Trim();

            // Try direct parse first
            if (double.TryParse(distanceRaw, out double distance))
                return distance;

            if (distanceRaw.EndsWith("f"))
            {
                return double.TryParse(distanceRaw.TrimEnd('f'), out double feet) ? feet : double.NaN;
            }

            // Match feet and inches, including fractional inches: e.g., 100f6 1/16i
            var match = Regex.Match(distanceRaw, @"^(?<feet>\d+)f(?<inches>[\d\.]+)?(?:\s*(?<frac_n>\d+)/(?<frac_d>\d+))?i$");
            if (match.Success)
            {
                double feet = double.Parse(match.Groups["feet"].Value);

                double inches = 0.0;
                if (match.Groups["inches"].Success && !string.IsNullOrWhiteSpace(match.Groups["inches"].Value))
                    inches = double.Parse(match.Groups["inches"].Value);

                inches += ParseFraction(match.Groups["frac_n"], match.Groups["frac_d"]);

                return feet + (inches / 12.0);
            }

            // Handle inches only, including fractions: e.g., 6 1/16i
            var inchMatch = Regex.Match(distanceRaw, @"^(?<inches>[\d\.]+)?(?:\s*(?<frac_n>\d+)/(?<frac_d>\d+))?i$");
            if (inchMatch.Success)
            {
                double inches = 0.0;
                if (inchMatch.Groups["inches"].Success && !string.IsNullOrWhiteSpace(inchMatch.Groups["inches"].Value))
                    inches = double.Parse(inchMatch.Groups["inches"].Value);

                inches += ParseFraction(inchMatch.Groups["frac_n"], inchMatch.Groups["frac_d"]);

                return inches / 12.0;
            }

            return double.NaN;
        }

        private double ParseFraction(Group numeratorGroup, Group denominatorGroup)
        {
            if (numeratorGroup.Success && denominatorGroup.Success)
            {
                if (double.TryParse(numeratorGroup.Value, out double numerator) &&
                    double.TryParse(denominatorGroup.Value, out double denominator) &&
                    denominator != 0)
                {
                    return numerator / denominator;
                }
            }
            return 0.0;
        }

        private double ParseAngle(string angleRaw)
        {
            // Normalize symbols
            angleRaw = angleRaw.Replace("°", "d").Replace("'", "m").Replace("\"", "s").Trim();

            if (TryHandleRadians(angleRaw, out double radians))
                return radians;

            if (TryHandleDMS(angleRaw, out double degrees))
                return DegreesToRadians(degrees);

            if (TryHandleBearings(angleRaw, out degrees))
                return DegreesToRadians(degrees);

            // Handle decimal degrees (e.g., 45d)
            if (angleRaw.EndsWith("d"))
            {
                return double.TryParse(angleRaw.TrimEnd('d'), out degrees) ? DegreesToRadians(degrees) : double.NaN;
            }

            // Fallback: try parsing as decimal degrees
            if (double.TryParse(angleRaw, out double fallbackDegrees))
                return DegreesToRadians(fallbackDegrees);

            return double.NaN;
        }

        private bool TryHandleBearings(string angleRaw, out double degrees)
        {
            degrees = 0.0;

            // Match: [N|S] <angle> [E|W], allowing spaces and DMS
            var bearingPattern = @"^(?<dir1>[NS])\s*(?<angle>.+?)\s*(?<dir2>[EW])$";
            var match = Regex.Match(angleRaw, bearingPattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string dir1 = match.Groups["dir1"].Value.ToUpper();
                string dir2 = match.Groups["dir2"].Value.ToUpper();
                string anglePart = match.Groups["angle"].Value.Trim().ToLower();

                // Normalize DMS symbols if needed
                anglePart = anglePart.Replace("°", "d").Replace("'", "m").Replace("\"", "s");

                // Try DMS first, fallback to decimal degrees
                double angle = 0.0;
                if (!TryHandleDMS(anglePart, out angle))
                {
                    // Try as decimal degrees (e.g., "45d")
                    if (anglePart.EndsWith("d"))
                        double.TryParse(anglePart.TrimEnd('d'), out angle);
                    else
                        double.TryParse(anglePart, out angle);
                }

                // If still not valid, fail
                if (angle == 0.0 && !anglePart.StartsWith("0"))
                    return false;

                // Convert to azimuth
                if (dir1 == "N" && dir2 == "E")
                    degrees = angle;
                else if (dir1 == "N" && dir2 == "W")
                    degrees = 360 - angle;
                else if (dir1 == "S" && dir2 == "E")
                    degrees = 180 - angle;
                else if (dir1 == "S" && dir2 == "W")
                    degrees = 180 + angle;
                else
                    return false;

                return true;
            }

            return false;
        }

        private bool TryHandleDMS(string angleRaw, out double degrees)
        {
            degrees = 0.0;

            // Match both compact and spaced DMS: "45d30m15s" or "45d 30m 15s"
            var dmsMatch = Regex.Match(angleRaw, @"^(?<deg>\d+)d\s*(?<min>\d+)?m?\s*(?<sec>\d+)?s?$", RegexOptions.IgnoreCase);
            if (dmsMatch.Success)
            {
                double deg = double.Parse(dmsMatch.Groups["deg"].Value);
                double min = dmsMatch.Groups["min"].Success && !string.IsNullOrEmpty(dmsMatch.Groups["min"].Value)
                    ? double.Parse(dmsMatch.Groups["min"].Value) : 0;
                double sec = dmsMatch.Groups["sec"].Success && !string.IsNullOrEmpty(dmsMatch.Groups["sec"].Value)
                    ? double.Parse(dmsMatch.Groups["sec"].Value) : 0;
                degrees = deg + min / 60.0 + sec / 3600.0;
                return true;
            }

            return false;
        }

        private bool TryHandleRadians(string angleRaw, out double radians)
        {
            if (angleRaw.EndsWith("r"))
            {
                return double.TryParse(angleRaw.TrimEnd('r'), out radians);
            }

            // Handle pi expressions (e.g., 2pi, pi/4, 3pi/2, -pi/2)
            var piPattern = @"^(?<sign>-)?(?:(?<num>\d+(\.\d+)?)\s*)?pi(?:\s*/\s*(?<den>\d+(\.\d+)?))?$";
            var piMatch = Regex.Match(angleRaw, piPattern, RegexOptions.IgnoreCase);
            if (piMatch.Success)
            {
                double sign = piMatch.Groups["sign"].Success ? -1.0 : 1.0;
                double numerator = piMatch.Groups["num"].Success ? double.Parse(piMatch.Groups["num"].Value) : 1.0;
                double denominator = piMatch.Groups["den"].Success ? double.Parse(piMatch.Groups["den"].Value) : 1.0;
                radians = sign * numerator * Math.PI / denominator;
                return true;
            }

            radians = 0;
            return false;
        }

        private Vector3D ComputeVector(double distance, double radians)
        {
            return new Vector3D(
                distance * Math.Cos(radians),
                distance * Math.Sin(radians),
                0.0
            );
        }

        private double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
