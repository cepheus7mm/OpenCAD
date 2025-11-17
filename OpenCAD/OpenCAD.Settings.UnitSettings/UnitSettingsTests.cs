namespace OpenCAD.Settings.UnitSettingsTests
{

    [TestClass]
    public sealed class UnitSettingsTests
    {
        private UnitSettings _unitSettings = null;

        [TestInitialize]
        public void Initialize()
        {
            _unitSettings = new UnitSettings();
        }

        [TestMethod]
        [DataRow(1234.5678, "1234' 6.81\"", LinearType.FeetAndInches, 2u)]
        [DataRow(1234.5678, "1234.57mm", LinearType.DecimalMillimeters, 2u)]
        [DataRow(1234.56784123456789, "1234.5678'", LinearType.DecimalFeet, 4u)]
        [DataRow(1234.5678, "14814.81\"", LinearType.DecimalInches, 2u)]
        [DataRow(1234.5678, "1234.568cm", LinearType.DecimalCentimeters, 3u)]
        [DataRow(1234.5678, "1234.57m", LinearType.DecimalMeters, 2u)]
        public void FormatLength_ShouldReturnFormattedString_WhenCalled(double length, string expected, LinearType measurementType, uint decimalPlaces)
        {
            // Arrange
            _unitSettings.LinearUnits = measurementType;
            _unitSettings.LinearDecimalPlaces = decimalPlaces;

            // Act
            string result = _unitSettings.LengthToString(length);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(Math.PI / 6, "30° 0' 0.0000\"", AngularType.DegreesMinsSecs, 4u)] // 30 degrees
        [DataRow(Math.PI / 6, "30.00°", AngularType.DegreesDecimal, 2u)]
        [DataRow(Math.PI / 6, "0.52rad", AngularType.Radians, 2u)]
        [DataRow(Math.PI / 6, "33.333333gon", AngularType.Gradians, 6u)]
        [DataRow(Math.PI / 6, "N 30° 0' 0.00\" E", AngularType.Bearings, 2u)]
        public void AngleToString_ShouldReturnFormattedString_WhenCalled(double angle, string expected, AngularType angleType, uint decimalPlaces)
        {
            // Arrange
            _unitSettings.AngularUnits = angleType;
            _unitSettings.AngularDecimalPlaces = decimalPlaces;

            // Act
            string result = _unitSettings.AngleToString(angle);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow("1234' 6.8136\"", 1234.5678, LinearType.FeetAndInches, 4u)]
        [DataRow("1234.57mm", 1234.57, LinearType.DecimalMillimeters, 2u)]
        [DataRow("1234.5678'", 1234.5678, LinearType.DecimalFeet, 4u)]
        [DataRow("14814.81\"", 1234.5675, LinearType.DecimalInches, 2u)] // 14814.81 / 12 = 1234.5675
        [DataRow("1234.568cm", 1234.568, LinearType.DecimalCentimeters, 3u)]
        [DataRow("1234.57m", 1234.57, LinearType.DecimalMeters, 2u)]
        public void StringToLength_ShouldParseStringToDouble_WhenCalled(string input, double expected, LinearType measurementType, uint decimalPlaces)
        {
            // Arrange
            _unitSettings.LinearUnits = measurementType;
            _unitSettings.LinearDecimalPlaces = decimalPlaces;

            // Act
            double result = _unitSettings.StringToLength(input);

            // Assert
            Assert.AreEqual(expected, result, 0.0001, $"Failed for input: {input}");
        }

        [TestMethod]
        [DataRow("30° 0' 0.0000\"", Math.PI / 6, AngularType.DegreesMinsSecs, 4u)] // 30 degrees
        [DataRow("30.00°", Math.PI / 6, AngularType.DegreesDecimal, 2u)]
        [DataRow("0.52rad", 0.52, AngularType.Radians, 2u)]
        [DataRow("33.333333gon", Math.PI / 6, AngularType.Gradians, 6u)] // 33.333333 * pi / 200 = pi/6
        [DataRow("N 30° 0' 0.00\" E", Math.PI / 6, AngularType.Bearings, 2u)]
        public void StringToAngle_ShouldParseStringToRadians_WhenCalled(string input, double expected, AngularType angleType, uint decimalPlaces)
        {
            // Arrange
            _unitSettings.AngularUnits = angleType;
            _unitSettings.AngularDecimalPlaces = decimalPlaces;

            // Act
            double result = _unitSettings.StringToAngle(input);

            // Assert
            Assert.AreEqual(expected, result, 0.0001, $"Failed for input: {input}");
        }
    }
}
