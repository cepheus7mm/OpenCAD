using OpenCAD;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace OpenCAD.Settings
{
    public enum UnitSystem
    {
        Imperial,
        Metric
    }

    public enum LinearType
    {
        FeetAndInches,
        DecimalFeet,
        DecimalInches,
        DecimalMillimeters,
        DecimalCentimeters,
        DecimalMeters
    }

    public enum AngularType
    {
        DegreesMinsSecs,
        DegreesDecimal,
        Radians,
        Gradians,
        Bearings
    }

    public class UnitSettings : OpenCADObject
    {
        public UnitSettings() : this(null!)
        {
        }

        public UnitSettings(OpenCADDocument document)
        {
            SystemOfUnits = UnitSystem.Imperial;
            LinearUnits = LinearType.DecimalFeet;
            LinearDecimalPlaces = 4;
            AngularUnits = AngularType.DegreesDecimal;
            AngularDecimalPlaces = 2;
        }

        [JsonIgnore, XmlIgnore]
        public UnitSystem SystemOfUnits
        {
            get => GetPropertyValue<UnitSystem>(PropertyType.SystemOfUnits, nameof(SystemOfUnits));
            set => SetPropertyValue(PropertyType.SystemOfUnits, nameof(SystemOfUnits), OpenCADStrings.SystemOfUnits, value);
        }

        [JsonIgnore, XmlIgnore]
        public LinearType LinearUnits
        {
            get => GetPropertyValue<LinearType>(PropertyType.LinearUnits, nameof(LinearUnits));
            set => SetPropertyValue(PropertyType.LinearUnits, nameof(LinearUnits), OpenCADStrings.LinearUnits, value);
        }

        [JsonIgnore, XmlIgnore]
        public uint LinearDecimalPlaces
        {
            get => GetPropertyValue<uint>(PropertyType.UInt, nameof(LinearDecimalPlaces));
            set => SetPropertyValue(PropertyType.UInt, nameof(LinearDecimalPlaces), OpenCADStrings.LinearDecimalPlaces, value);
        }

        [JsonIgnore, XmlIgnore]
        public AngularType AngularUnits
        {
            get => GetPropertyValue<AngularType>(PropertyType.AngularUnits, nameof(AngularUnits));
            set => SetPropertyValue(PropertyType.AngularUnits, nameof(AngularUnits), OpenCADStrings.AngularUnits, value);
        }

        [JsonIgnore, XmlIgnore]
        public uint AngularDecimalPlaces
        {
            get => GetPropertyValue<uint>(PropertyType.UInt, nameof(AngularDecimalPlaces));
            set => SetPropertyValue(PropertyType.UInt, nameof(AngularDecimalPlaces), OpenCADStrings.AngularDecimalPlaces, value);
        }

        public string LengthToString(double length)
        {
            // Handle sign here and delegate positive value to helpers
            bool negative = length < 0;
            double absLength = Math.Abs(length);

            string inner = LinearUnits switch
            {
                LinearType.FeetAndInches => LengthFeetAndInches(absLength),
                LinearType.DecimalFeet => $"{absLength.ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Feet}",
                LinearType.DecimalInches => $"{(absLength * 12.0).ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Inches}",
                LinearType.DecimalMillimeters => $"{(absLength).ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Millimeters}",
                LinearType.DecimalCentimeters => $"{(absLength).ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Centimeters}",
                LinearType.DecimalMeters => $"{(absLength).ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Meters}",
                _ => absLength.ToString(),
            };

            return negative ? "-" + inner : inner;
        }

        public double StringToLength(string lengthString)
        {
            if (string.IsNullOrWhiteSpace(lengthString))
                throw new ArgumentNullException(nameof(lengthString));

            lengthString = lengthString.Trim();

            // Top-level sign handling: strip optional leading '+' or '-' and apply sign after parsing
            int sign = 1;
            if (lengthString.StartsWith("-", StringComparison.Ordinal))
            {
                sign = -1;
                lengthString = lengthString.Substring(1).Trim();
            }
            else if (lengthString.StartsWith("+", StringComparison.Ordinal))
            {
                lengthString = lengthString.Substring(1).Trim();
            }

            double parsed = LinearUnits switch
            {
                LinearType.FeetAndInches      => ParseFeetAndInches(lengthString),
                LinearType.DecimalFeet        => ParseDecimalFeet(lengthString),
                LinearType.DecimalInches      => ParseDecimalInches(lengthString),
                LinearType.DecimalMillimeters => ParseDecimalMillimeters(lengthString),
                LinearType.DecimalCentimeters => ParseDecimalCentimeters(lengthString),
                LinearType.DecimalMeters      => ParseDecimalMeters(lengthString),
                _ => throw new NotSupportedException($"Unsupported LinearMeasurementType: {LinearUnits}")
            };

            return sign * parsed;
        }

        private double ParseFeetAndInches(string lengthString)
        {
            return LengthFeetAndInchesToDouble(lengthString);
        }

        private double ParseDecimalFeet(string lengthString)
        {
            lengthString = lengthString.Replace(OpenCADStrings.Feet, "", StringComparison.OrdinalIgnoreCase).Trim();
            return double.Parse(lengthString);
        }

        private double ParseDecimalInches(string lengthString)
        {
            lengthString = lengthString.Replace(OpenCADStrings.Inches, "", StringComparison.OrdinalIgnoreCase).Trim();
            return double.Parse(lengthString) / 12.0;
        }

        private double ParseDecimalMillimeters(string lengthString)
        {
            lengthString = lengthString.Replace(OpenCADStrings.Millimeters, "", StringComparison.OrdinalIgnoreCase).Trim();
            return double.Parse(lengthString);
        }

        private double ParseDecimalCentimeters(string lengthString)
        {
            lengthString = lengthString.Replace(OpenCADStrings.Centimeters, "", StringComparison.OrdinalIgnoreCase).Trim();
            return double.Parse(lengthString);
        }

        private double ParseDecimalMeters(string lengthString)
        {
            lengthString = lengthString.Replace(OpenCADStrings.Meters, "", StringComparison.OrdinalIgnoreCase).Trim();
            return double.Parse(lengthString);
        }

        /// <summary>
        /// Parses a string in feet and inches format (e.g., 5' 7.25" or 5 ft 7.25 in) to a double value in feet.
        /// </summary>
        public static double LengthFeetAndInchesToDouble(string lengthString)
        {
            // Accepts: 5'7.25", 5' 7.25", 5 ft 7.25 in, 67.25", etc.
            double feet = 0, inches = 0;
            var feetMatch = System.Text.RegularExpressions.Regex.Match(lengthString, @"(-?\d+(\.\d+)?)\s*(ft|')", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var inchMatch = System.Text.RegularExpressions.Regex.Match(lengthString, @"(-?\d+(\.\d+)?)\s*(in|\"")", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (feetMatch.Success)
                feet = double.Parse(feetMatch.Groups[1].Value.Trim());
            if (inchMatch.Success)
                inches = double.Parse(inchMatch.Groups[1].Value.Trim());

            // If only inches are provided (e.g., 67.25"), feet will be 0
            return feet + (inches / 12.0);
        }

        private string LengthFeetAndInches(double length)
        {
            // length passed in is positive; top-level caller handles sign.
            int feet = (int)length;
            double inches = (length - feet) * 12.0;
            return $"{feet}{OpenCADStrings.Feet} {inches.ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Inches}";
        }

        /// <summary>
        /// Converts an angle to a string based on the current angular units
        /// </summary>
        /// <param name="angle">An angle in radians</param>
        /// <returns>A string representation of the angle</returns>
        public string AngleToString(double angle)
        {
            // Capture sign at top level, delegate absolute value to helpers, then reapply sign as a leading '-'
            bool negative = angle < 0;
            double absAngle = Math.Abs(angle);

            string inner = AngularUnits switch
            {
                AngularType.DegreesMinsSecs => ConvertToDMS(absAngle),
                AngularType.DegreesDecimal => $"{RadiansToDegrees(absAngle).ToString($"F{AngularDecimalPlaces}")}{OpenCADStrings.Degrees}",
                AngularType.Radians => $"{absAngle.ToString($"F{AngularDecimalPlaces}")}{OpenCADStrings.Radians}",
                AngularType.Gradians => $"{RadiansToGradians(absAngle).ToString($"F{AngularDecimalPlaces}")}{OpenCADStrings.Gradians}",
                AngularType.Bearings => ConvertToBearing(absAngle),
                _ => absAngle.ToString(),
            };

            return negative ? "-" + inner : inner;
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        public static double RadiansToGradians(double radians)
        {
            return radians * (200.0 / Math.PI);
        }

        /// <summary>
        /// Converts an angle in radians to Degrees Minutes Seconds format
        /// </summary>
        /// <param name="angle">an angle in radians</param>
        /// <returns>string in Degrees Minutes Seconds format</returns>
        private string ConvertToDMS(double angle)
        {
            double angleDeg = RadiansToDegrees(angle);
            int degrees = (int)Math.Floor(angleDeg);
            double minFull = (angleDeg - degrees) * 60.0;
            int minutes = (int)Math.Floor(minFull);
            double seconds = (minFull - minutes) * 60.0;

            // Round seconds to the specified decimal places
            double rounding = Math.Pow(10, AngularDecimalPlaces);
            seconds = Math.Round(seconds * rounding) / rounding;

            // Handle rounding up
            if (seconds >= 60.0)
            {
                seconds -= 60.0;
                minutes += 1;
            }
            if (minutes >= 60)
            {
                minutes -= 60;
                degrees += 1;
            }

            return $"{degrees}{OpenCADStrings.Degrees} {minutes}{OpenCADStrings.Minutes} {seconds.ToString($"F{AngularDecimalPlaces}")}{OpenCADStrings.Seconds}";
        }

        private string ConvertToBearing(double angle)
        {
            // Normalize angle to [0, 2π)
            double angleRad = angle % (2 * Math.PI);
            if (angleRad < 0)
                angleRad += 2 * Math.PI;

            string quadrant;
            double bearingAngleRad;

            if (angleRad <= Math.PI / 2)
            {
                quadrant = OpenCADStrings.QuadNorthEast;
                bearingAngleRad = angleRad;
            }
            else if (angleRad <= Math.PI)
            {
                quadrant = OpenCADStrings.QuadSouthEast;
                bearingAngleRad = Math.PI - angleRad;
            }
            else if (angleRad <= 3 * Math.PI / 2)
            {
                quadrant = OpenCADStrings.QuadSouthWest;
                bearingAngleRad = angleRad - Math.PI;
            }
            else
            {
                quadrant = OpenCADStrings.QuadNorthWest;
                bearingAngleRad = 2 * Math.PI - angleRad;
            }
            return $"{quadrant[0]} {ConvertToDMS(bearingAngleRad)} {quadrant[1]}";
        }

        internal string FormatLength(double length)
        {
            throw new NotImplementedException();
        }

        internal string FormatAngle(double angle)
        {
            throw new NotImplementedException();
        }

        internal string Vector3DToString(object value)
        {
            throw new NotImplementedException();
        }

        public double StringToAngle(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentNullException(nameof(str));

            str = str.Trim();

            int sign = 1;
            if (str.StartsWith("-", StringComparison.Ordinal))
            {
                sign = -1;
                str = str[1..].Trim();
            }
            else if (str.StartsWith("+", StringComparison.Ordinal))
            {
                str = str[1..].Trim();
            }

            double parsed = ParseAngleAuto(str);

            return sign * parsed;
        }

        private double ParseAngleAuto(string s)
        {
            // 1. Bearings: look for N/S/E/W tokens
            if (LooksLikeBearing(s))
                return ParseBearings(s);

            // 2. DMS: look for ° ' " or colon patterns
            if (LooksLikeDms(s))
                return ParseDegreesMinsSecs(s);

            // 4. Gradians: look for "g" suffix or explicit pattern
            if (LooksLikeGradians(s))
                return ParseGradians(s);

            // 3. Radians: look for "rad" suffix or something like "π"
            if (LooksLikeRadians(s))
                return ParseRadians(s);

            // 5. Fallback: plain decimal degrees
            return ParseDegreesDecimal(s);
        }

        private bool LooksLikeBearing(string s)
        {
            s = s.ToUpperInvariant();
            return (s.Contains('N') || s.Contains('S')) && (s.Contains('E') || s.Contains('W')) && !s.Contains("DEG");
        }

        private bool LooksLikeDms(string s)
        {
            s = s.ToUpperInvariant();
            return (s.Contains('°') || s.Contains('D')) && (s.Contains('\'') || s.Contains('M')) && (s.Contains('"') || s.Contains('S')) || s.Contains(':');
        }

        private bool LooksLikeRadians(string s)
        {
            s = s.ToLowerInvariant();
            return s.EndsWith("rad") || s.Contains("radians") || s.EndsWith("r") || s.Contains('π') || s.Contains("pi");
        }

        private bool LooksLikeGradians(string s)
        {
            s = s.ToLowerInvariant();
            return (s.EndsWith("gon") || s.EndsWith("g") || s.Contains("gradians")) && !s.Contains("deg");
        }

        private double ParseDegreesMinsSecs(string str)
        {
            str = str.Trim();

            // Regex that matches:
            //  - degrees with optional ° or d
            //  - optional minutes with ' or m
            //  - optional seconds with " or s
            var match = Regex.Match(
                  str,
                  @"^\s*
                  (?<deg>-?\d+(?:\.\d+)?)\s*(°|d)?\s*
                  (?<min>\d+(?:\.\d+)?)?\s*(' |m)?\s*
                  (?<sec>\d+(?:\.\d+)?)?\s*(\""|s)?
                  \s*$",
                   RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

            if (!match.Success)
                throw new FormatException("Invalid DMS angle format.");

            double deg = double.Parse(match.Groups["deg"].Value);

            double min = match.Groups["min"].Success
                ? double.Parse(match.Groups["min"].Value)
                : 0.0;

            double sec = match.Groups["sec"].Success
                ? double.Parse(match.Groups["sec"].Value)
                : 0.0;

            double angleDeg = deg + (min / 60.0) + (sec / 3600.0);
            return angleDeg * Math.PI / 180.0;
        }

        private double ParseDegreesDecimal(string str)
        {
            str = str.Trim();

            // Strip any degree-like suffix using regex
            str = Regex.Replace(str, @"(°|deg|degree|degrees|d)$", "", RegexOptions.IgnoreCase).Trim();

            // Fix trailing decimal point
            if (str.EndsWith(".", StringComparison.Ordinal))
                str += "0";

            double degVal = double.Parse(str);
            return degVal * Math.PI / 180.0;
        }

        private double ParseRadians(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentNullException(nameof(str));

            str = str.Trim().ToLowerInvariant();

            // Normalize common tokens
            str = str.Replace("radians", "")
                     .Replace("radian", "")
                     .Replace("rad", "")
                     .Replace("r", "")     // careful: only safe after lowercasing
                     .Trim();

            // Replace unicode pi with ascii pi
            str = str.Replace("π", "pi");

            // If the string contains "pi", treat it as a pi-expression
            if (str.Contains("pi"))
            {
                // Replace "pi" with a token we can evaluate
                // Examples:
                //   "pi"       → "1*pi"
                //   "3pi/2"    → "3*pi/2"
                //   "-pi/4"    → "-1*pi/4"
                //   "pi/ 2"    → "1*pi/2"

                // Insert explicit multiplication before pi when needed
                str = Regex.Replace(str, @"(?<![\w])pi", "1*pi");   // leading pi
                str = Regex.Replace(str, @"(\d)pi", "$1*pi");       // 3pi → 3*pi

                // Now evaluate the expression safely
                return EvaluatePiExpression(str);
            }

            // Otherwise, it's a plain numeric radian value
            return double.Parse(str);
        }

        private double EvaluatePiExpression(string expr)
        {
            // Split on '/'
            var parts = expr.Split('/');

            double numerator = EvaluatePiTerm(parts[0]);

            if (parts.Length == 1)
                return numerator;

            if (parts.Length == 2)
            {
                double denominator = EvaluatePiTerm(parts[1]);
                return numerator / denominator;
            }

            throw new FormatException("Invalid radian expression.");
        }

        private double EvaluatePiTerm(string term)
        {
            term = term.Trim();

            // Split on '*'
            var factors = term.Split('*');

            double result = 1.0;

            foreach (var f in factors)
            {
                string t = f.Trim();

                if (t == "pi")
                    result *= Math.PI;
                else
                    result *= double.Parse(t);
            }

            return result;
        }

        private double ParseGradians(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentNullException(nameof(str));

            str = str.Trim().ToLowerInvariant();

            // Remove common gradian indicators
            str = str
                .Replace("gon", "")
                .Replace("gradians", "")
                .Replace("grads", "")
                .Replace("grad", "")
                .Replace("g", "")
                .Trim();

            // Handle trailing decimal point like "150."
            if (str.EndsWith(".", StringComparison.Ordinal))
                str += "0";

            double gradVal = double.Parse(str);
            return gradVal * Math.PI / 200.0;
        }

        private double ParseBearings(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                throw new ArgumentNullException(nameof(str));

            str = str.Trim().ToUpperInvariant();

            // Extract leading N/S
            if (str.Length < 2)
                throw new FormatException("Invalid bearing format.");

            char ns = str[0];
            if (ns != 'N' && ns != 'S')
                throw new FormatException("Bearing must start with N or S.");

            // Extract trailing E/W
            char ew = str[^1];
            if (ew != 'E' && ew != 'W')
                throw new FormatException("Bearing must end with E or W.");

            // Extract the angle portion between them
            string anglePart = str.Substring(1, str.Length - 2).Trim();

            // Parse the angle using your DMS parser
            double angleRad = ParseDegreesMinsSecs(anglePart);

            // Convert bearing to azimuth (radians)
            // Quadrants:
            //   NE: 0° + angle
            //   SE: 180° - angle
            //   SW: 180° + angle
            //   NW: 360° - angle

            if (ns == 'N' && ew == 'E')
                return angleRad;

            if (ns == 'S' && ew == 'E')
                return Math.PI - angleRad;

            if (ns == 'S' && ew == 'W')
                return Math.PI + angleRad;

            if (ns == 'N' && ew == 'W')
                return (2 * Math.PI) - angleRad;

            throw new FormatException("Invalid bearing quadrant.");
        }
    }
}