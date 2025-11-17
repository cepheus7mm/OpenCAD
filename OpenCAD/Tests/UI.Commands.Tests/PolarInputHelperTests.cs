using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands;
using UI.Helpers;

namespace UI.Commands.Tests
{
    [TestClass]
    public class PolarInputHelperTests
    {
        [TestInitialize]
        public void BeforeEach()
        {
            // Any setup code if necessary
        }

        [TestMethod]
        public void TestValidInput()
        {
            // Arrange
            string input = "@100<45d";
            var helper = new PolarInputHelper(input);

            // Act
            bool isValid = helper.IsValid;

            // Assert
            Assert.IsTrue(isValid);
            Assert.AreEqual(100, helper.Distance);
            Assert.AreEqual(45 * Math.PI / 180.0, helper.Angle);
            Assert.AreEqual(70.711, helper.Vector.X, 0.001);
            Assert.AreEqual(70.711, helper.Vector.Y, 0.001);
            Assert.AreEqual(0, helper.Vector.Z, 0.001);
        }

        [TestMethod]
        public void TestInvalidInput()
        {
            // Arrange
            string input = "@100<invalid";
            var helper = new PolarInputHelper(input);

            // Act
            bool isValid = helper.IsValid;

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void TestInvalidDistanceInput()
        {
            // Arrange
            string input = "@invalid<45d";
            var helper = new PolarInputHelper(input);

            // Act
            bool isValid = helper.IsValid;

            // Assert
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void TestValidDistanceInput()
        {
            // Arrange
            string input = "@100'6\"<45d";
            var helper = new PolarInputHelper(input);

            // Act
            bool isValid = helper.IsValid;

            // Assert
            Assert.IsTrue(isValid);
            Assert.AreEqual(100.5, helper.Distance);
            Assert.AreEqual(45 * Math.PI / 180.0, helper.Angle);
        }

        [TestMethod]
        public void TestFractionalInches()
        {
            // Arrange
            string input = "@100'6 1/16\"<45d";
            var helper = new PolarInputHelper(input);

            // Act
            bool isValid = helper.IsValid;

            // Assert
            Assert.IsTrue(isValid);
            Assert.AreEqual(100.5 + (1.0 / 16.0) / 12.0, helper.Distance);
            Assert.AreEqual(45 * Math.PI / 180.0, helper.Angle);
        }

        [TestMethod]
        [DataRow("0", 0)]
        [DataRow("pi/36", 5)]
        [DataRow("pi/18", 10)]
        [DataRow("pi/12", 15)]
        [DataRow("pi/9", 20)]
        [DataRow("5pi/36", 25)]
        [DataRow("pi/6", 30)]
        [DataRow("7pi/36", 35)]
        [DataRow("8pi/36", 40)]
        [DataRow("pi/4", 45)]
        [DataRow("pi/3", 60)]
        [DataRow("pi/2", 90)]
        [DataRow("2pi/3", 120)]
        [DataRow("5pi/6", 150)]
        [DataRow("pi", 180)]
        [DataRow("7pi/6", 210)]
        [DataRow("3pi/2", 270)]
        public void TestComplexAngle(string inputAngle, double expectedAngle)
        {
            // Arrange
            string input = "@100<" + inputAngle;
            var helper = new PolarInputHelper(input);

            // Act
            bool isValid = helper.IsValid;

            // Assert
            Assert.IsTrue(isValid);
            Assert.AreEqual(100, helper.Distance);
            Assert.AreEqual(expectedAngle * Math.PI / 180.0, helper.Angle, 0.000000001);
            Assert.AreEqual(100.0 * Math.Cos(helper.Angle), helper.Vector.X, 0.001);
            Assert.AreEqual(100.0 * Math.Sin(helper.Angle), helper.Vector.Y, 0.001);
            Assert.AreEqual(0, helper.Vector.Z, 0.001);
        }

        [TestMethod]
        public void TestNegativeDistance()
        {
            // Arrange
            string input = "@-50<30d";
            var helper = new PolarInputHelper(input);
            // Act
            bool isValid = helper.IsValid;
            // Assert
            Assert.IsTrue(isValid);
            Assert.AreEqual(-50, helper.Distance);
            Assert.AreEqual(30 * Math.PI / 180.0, helper.Angle);
            Assert.AreEqual(-50.0 * Math.Cos(helper.Angle), helper.Vector.X, 0.001);
            Assert.AreEqual(-50.0 * Math.Sin(helper.Angle), helper.Vector.Y, 0.001);
            Assert.AreEqual(0, helper.Vector.Z, 0.001);
        }

        [TestMethod]
        [DataRow("@100<N 45d E", 100, 45)]
        [DataRow("@100<N45dE", 100, 45)]
        [DataRow("@100<n 45d e", 100, 45)]
        [DataRow("@100<S 30d W", 100, 210)]
        [DataRow("@100<S30dW", 100, 210)]
        [DataRow("@100<N 60d W", 100, 300)]
        [DataRow("@100<S 15d E", 100, 165)]
        [DataRow("@100<N45D30M0SE", 100, 45.5)]
        [DataRow("@100<N 45d 30m 0s E", 100, 45.5)]
        [DataRow("@100<N45D30ME", 100, 45.5)]
        [DataRow("@100<N 45d 30m E", 100, 45.5)]
        [DataRow("@100<S 0d E", 100, 180)]
        [DataRow("@100<N 0d E", 100, 0)]
        public void BearingAngleTest(string input, double expectedDistance, double expectedDegrees)
        {
            // Arrange
            var helper = new PolarInputHelper(input);

            // Act
            bool isValid = helper.IsValid;

            // Assert
            Assert.IsTrue(isValid, $"Input '{input}' should be valid.");
            Assert.AreEqual(expectedDistance, helper.Distance, 0.0001, $"Distance for '{input}'");
            Assert.AreEqual(expectedDegrees * Math.PI / 180.0, helper.Angle, 0.000001, $"Angle for '{input}'");
            Assert.AreEqual(expectedDistance * Math.Cos(helper.Angle), helper.Vector.X, 0.001, $"Vector.X for '{input}'");
            Assert.AreEqual(expectedDistance * Math.Sin(helper.Angle), helper.Vector.Y, 0.001, $"Vector.Y for '{input}'");
            Assert.AreEqual(0, helper.Vector.Z, 0.001, $"Vector.Z for '{input}'");
        }
    }
}
