using OpenCAD;
using System.Text.Json.Serialization;
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
            return LinearUnits switch
            {
                LinearType.FeetAndInches => LengthFeetAndInches(length),
                LinearType.DecimalFeet => $"{length.ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Feet}",
                LinearType.DecimalInches => $"{(length * 12.0).ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Inches}",
                LinearType.DecimalMillimeters => $"{(length).ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Millimeters}",
                LinearType.DecimalCentimeters => $"{(length).ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Centimeters}",
                LinearType.DecimalMeters => $"{(length).ToString($"F{LinearDecimalPlaces}")}{OpenCADStrings.Meters}",
                _ => length.ToString(),
            };
        }

        public double StringToLength(string lengthString)
        {
            if (string.IsNullOrWhiteSpace(lengthString))
                throw new ArgumentNullException(nameof(lengthString));

            lengthString = lengthString.Trim();

            return LinearUnits switch
            {
                LinearType.FeetAndInches      => ParseFeetAndInches(lengthString),
                LinearType.DecimalFeet        => ParseDecimalFeet(lengthString),
                LinearType.DecimalInches      => ParseDecimalInches(lengthString),
                LinearType.DecimalMillimeters => ParseDecimalMillimeters(lengthString),
                LinearType.DecimalCentimeters => ParseDecimalCentimeters(lengthString),
                LinearType.DecimalMeters      => ParseDecimalMeters(lengthString),
                _ => throw new NotSupportedException($"Unsupported LinearMeasurementType: {LinearUnits}")
            };
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
            return AngularUnits switch
            {
                AngularType.DegreesMinsSecs => ConvertToDMS(angle),
                AngularType.DegreesDecimal => $"{RadiansToDegrees(angle).ToString($"F{AngularDecimalPlaces}")}{OpenCADStrings.Degrees}",
                AngularType.Radians => $"{angle.ToString($"F{AngularDecimalPlaces}")}{OpenCADStrings.Radians}",
                AngularType.Gradians => $"{RadiansToGradians(angle).ToString($"F{AngularDecimalPlaces}")}{OpenCADStrings.Gradians}",
                AngularType.Bearings => ConvertToBearing(angle),
                _ => angle.ToString(),
            };
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

            return AngularUnits switch
            {
                AngularType.DegreesMinsSecs => ParseDegreesMinsSecs(str),
                AngularType.DegreesDecimal  => ParseDegreesDecimal(str),
                AngularType.Radians         => ParseRadians(str),
                AngularType.Gradians        => ParseGradians(str),
                AngularType.Bearings        => ParseBearings(str),
                _ => throw new NotSupportedException($"Unsupported AngularUnits: {AngularUnits}")
            };
        }

        private double ParseDegreesMinsSecs(string str)
        {
            // Example: "12° 34' 56.78\""
            var dmsMatch = System.Text.RegularExpressions.Regex.Match(
                str,
                @"(-?\d+)[^\d]+(\d+)[^\d]+([\d\.]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (dmsMatch.Success)
            {
                int deg = int.Parse(dmsMatch.Groups[1].Value);
                int min = int.Parse(dmsMatch.Groups[2].Value);
                double sec = double.Parse(dmsMatch.Groups[3].Value);
                double angleDeg = deg + (min / 60.0) + (sec / 3600.0);
                return angleDeg * Math.PI / 180.0;
            }
            throw new FormatException("Invalid DMS angle format.");
        }

        private double ParseDegreesDecimal(string str)
        {
            // Example: "123.45°"
            str = str.Replace(OpenCADStrings.Degrees, "", StringComparison.OrdinalIgnoreCase).Trim();
            double degVal = double.Parse(str);
            return degVal * Math.PI / 180.0;
        }

        private double ParseRadians(string str)
        {
            // Example: "2.13rad"
            str = str.Replace(OpenCADStrings.Radians, "", StringComparison.OrdinalIgnoreCase).Trim();
            return double.Parse(str);
        }

        private double ParseGradians(string str)
        {
            // Example: "150.00g"
            str = str.Replace(OpenCADStrings.Gradians, "", StringComparison.OrdinalIgnoreCase).Trim();
            double gradVal = double.Parse(str);
            return gradVal * Math.PI / 200.0;
        }

        private double ParseBearings(string str)
        {
            // Example: "N 12° 34' 56.78\" E"
            var bearingMatch = System.Text.RegularExpressions.Regex.Match(
                str,
                @"([NnSs])\s*(\d+)[^\d]+(\d+)[^\d]+([\d\.]+)[^\d]*([EeWw])",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (bearingMatch.Success)
            {
                char ns = char.ToUpperInvariant(bearingMatch.Groups[1].Value[0]);
                int deg = int.Parse(bearingMatch.Groups[2].Value);
                int min = int.Parse(bearingMatch.Groups[3].Value);
                double sec = double.Parse(bearingMatch.Groups[4].Value);
                char ew = char.ToUpperInvariant(bearingMatch.Groups[5].Value[0]);
                double angleDeg = deg + (min / 60.0) + (sec / 3600.0);
                double angleRad = angleDeg * Math.PI / 180.0;

                // Bearings: N/E is 0, S/E is 90, S/W is 180, N/W is 270
                if (ns == 'N' && ew == 'E')
                    return angleRad;
                if (ns == 'S' && ew == 'E')
                    return Math.PI - angleRad;
                if (ns == 'S' && ew == 'W')
                    return Math.PI + angleRad;
                if (ns == 'N' && ew == 'W')
                    return 2 * Math.PI - angleRad;
            }
            throw new FormatException("Invalid bearing angle format.");
        }
    }
}