using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using OpenCAD.Geometry;
using UI.Controls.Viewport;
using UI.Commands.InputHelpers;

namespace UI.Commands.Tests
{
    [TestClass]
    public class GetPointInputTests : InputHelperTestsBase
    {
        [TestMethod]
        public async Task GetPointOrKeywordAsync_CompletesWhenViewportRaisesPointPicked()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var picked = new Point3D(10.0, 20.0, 3.0);

            // Act - start the async request (it will configure VM via PostToUI which runs synchronously in tests)
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:",
                AllowLastPoint = false
            });

            // After starting, point picking mode should be enabled (PostToUI invoked synchronously)
            Assert.IsTrue(ViewModel.IsPointPickingMode, "Point picking mode should be enabled after starting GetPointOrKeywordAsync.");

            // Simulate user picking a point in the viewport via the public API.
            // Can't invoke the event from outside the class, so call the method that raises it.
            var worldPos = new System.Numerics.Vector3((float)picked.X, (float)picked.Y, (float)picked.Z);
            ViewModel.HandleMouseDown(System.Windows.Input.MouseButton.Left, new System.Windows.Point(0, 0), worldPos);

            // Await completion with a timeout so test doesn't hang if something goes wrong
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(2));

            // Assert
            Assert.IsNotNull(result, "Result should not be null.");
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType, "Result type should be Point.");
            Assert.IsNotNull(result.Point, "Point payload should be set.");
            Assert.AreEqual(picked.X, result.Point.Value.X, 1e-9);
            Assert.AreEqual(picked.Y, result.Point.Value.Y, 1e-9);
            Assert.AreEqual(picked.Z, result.Point.Value.Z, 1e-9);

            // Verify that last point was stored and output message was produced
            ContextMock.Verify(c => c.SetLastPoint(It.Is<Point3D>(p => Math.Abs(p.X - picked.X) < 1e-9 && Math.Abs(p.Y - picked.Y) < 1e-9 && Math.Abs(p.Z - picked.Z) < 1e-9)), Times.AtLeastOnce);
            ContextMock.Verify(c => c.OutputMessage(It.IsAny<string>()), Times.AtLeastOnce);

            // After completion cleanup should have run and point picking disabled
            Assert.IsFalse(ViewModel.IsPointPickingMode, "Point picking mode should be disabled after completion and cleanup.");
        }

        #region Keyboard Input Tests - Coordinate Parsing

        [TestMethod]
        public async Task ProcessKeyboardInput_ParsesAbsoluteCoordinates_SpaceSeparated()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act - simulate typing space-separated coordinates
            var processed = helper.ProcessKeyboardInput("10 20 30");

            // Assert
            Assert.IsTrue(processed, "Input should be processed");

            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);
            Assert.IsNotNull(result.Point);
            Assert.AreEqual(10.0, result.Point.Value.X, 1e-9);
            Assert.AreEqual(20.0, result.Point.Value.Y, 1e-9);
            Assert.AreEqual(30.0, result.Point.Value.Z, 1e-9);

            // Verify last point was set
            ContextMock.Verify(c => c.SetLastPoint(It.Is<Point3D>(p =>
                Math.Abs(p.X - 10.0) < 1e-9 &&
                Math.Abs(p.Y - 20.0) < 1e-9 &&
                Math.Abs(p.Z - 30.0) < 1e-9)), Times.Once);

            // Verify output message
            ContextMock.Verify(c => c.OutputMessage(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_ParsesAbsoluteCoordinates_CommaSeparated()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act - simulate typing comma-separated coordinates
            var processed = helper.ProcessKeyboardInput("10,20,30");

            // Assert
            Assert.IsTrue(processed, "Input should be processed");

            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);
            Assert.IsNotNull(result.Point);
            Assert.AreEqual(10.0, result.Point.Value.X, 1e-9);
            Assert.AreEqual(20.0, result.Point.Value.Y, 1e-9);
            Assert.AreEqual(30.0, result.Point.Value.Z, 1e-9);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_ParsesNegativeCoordinates()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act
            var processed = helper.ProcessKeyboardInput("-10 -20 -30");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);
            Assert.AreEqual(-10.0, result.Point.Value.X, 1e-9);
            Assert.AreEqual(-20.0, result.Point.Value.Y, 1e-9);
            Assert.AreEqual(-30.0, result.Point.Value.Z, 1e-9);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_ParsesDecimalCoordinates()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act
            var processed = helper.ProcessKeyboardInput("10.5 20.75 30.125");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);
            Assert.AreEqual(10.500, result.Point.Value.X, 1e-9);
            Assert.AreEqual(20.750, result.Point.Value.Y, 1e-9);
            Assert.AreEqual(30.125, result.Point.Value.Z, 1e-9);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_HandlesWhitespace()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act - extra whitespace should be handled
            var processed = helper.ProcessKeyboardInput("  10   20   30  ");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);
            Assert.AreEqual(10.0, result.Point.Value.X, 1e-9);
            Assert.AreEqual(20.0, result.Point.Value.Y, 1e-9);
            Assert.AreEqual(30.0, result.Point.Value.Z, 1e-9);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_RejectsInvalidCoordinateFormat()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act - invalid format (only 2 coordinates)
            var processed = helper.ProcessKeyboardInput("10 20");

            // Assert - should return Arbitrary result type for invalid coordinates
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Arbitrary, result.ResultType);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_RejectsNonNumericInput()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act - non-numeric input
            var processed = helper.ProcessKeyboardInput("abc def ghi");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Arbitrary, result.ResultType);
        }

        #endregion

        #region Keyboard Input Tests - Last Point Usage

        [TestMethod]
        public async Task ProcessKeyboardInput_UsesLastPointOnEmptyInput_WhenAllowed()
        {
            // Arrange
            var lastPoint = new Point3D(100, 200, 300);
            ContextMock.Setup(c => c.GetLastPoint()).Returns(lastPoint);

            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:",
                AllowLastPoint = true
            });

            // Act - press Enter (empty input)
            var processed = helper.ProcessKeyboardInput("");

            // Assert
            Assert.IsTrue(processed, "Empty input with allowLastPoint should be processed");
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);
            Assert.AreEqual(lastPoint.X, result.Point.Value.X, 1e-9);
            Assert.AreEqual(lastPoint.Y, result.Point.Value.Y, 1e-9);
            Assert.AreEqual(lastPoint.Z, result.Point.Value.Z, 1e-9);

            // Verify GetLastPoint was called
            ContextMock.Verify(c => c.GetLastPoint(), Times.AtLeastOnce);

            // Verify output message with last point
            ContextMock.Verify(c => c.OutputMessage(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_ReturnsNullOnEmptyInput_WhenLastPointNotAllowed()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:",
                AllowLastPoint = false
            });

            // Act - press Enter (empty input)
            var processed = helper.ProcessKeyboardInput("%");

            // Assert
            Assert.IsTrue(processed, "Input should be processed");
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Cancel, result.ResultType);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_ReturnsNullOnEmptyInput_WhenNoLastPointAvailable()
        {
            // Arrange
            ContextMock.Setup(c => c.GetLastPoint()).Returns((Point3D?)null);

            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:",
                AllowLastPoint = true
            });

            // Act - press Enter but no last point exists
            var processed = helper.ProcessKeyboardInput("");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Cancel, result.ResultType);
        }

        #endregion

        #region Keyboard Input Tests - Polar Input

        [TestMethod]
        public async Task ProcessKeyboardInput_ParsesPolarCoordinates_DegreesFromLastPoint()
        {
            // Arrange
            var basePoint = new Point3D(10, 10, 0);
            ContextMock.Setup(c => c.GetLastPoint()).Returns(basePoint);

            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act - enter polar coordinates: distance 10 at 45 degrees
            var processed = helper.ProcessKeyboardInput("@10<45");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);

            // Expected: basePoint + (10*cos(45°), 10*sin(45°), 0)
            double angle45Rad = 45 * Math.PI / 180.0;
            double expectedX = 10 + 10 * Math.Cos(angle45Rad);
            double expectedY = 10 + 10 * Math.Sin(angle45Rad);

            Assert.AreEqual(expectedX, result.Point.Value.X, 1e-6);
            Assert.AreEqual(expectedY, result.Point.Value.Y, 1e-6);
            Assert.AreEqual(0.0000000, result.Point.Value.Z, 1e-9);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_ParsesPolarCoordinates_WithBasePoint()
        {
            // Arrange
            var basePoint = new Point3D(5, 5, 0);

            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify second point:",
                BasePoint = basePoint
            });

            // Act - polar from base point
            var processed = helper.ProcessKeyboardInput("@10<90");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);

            // 90 degrees = straight up (Y direction)
            // Expected: (5, 15, 0)
            Assert.AreEqual(5.00, result.Point.Value.X, 1e-6);
            Assert.AreEqual(15.0, result.Point.Value.Y, 1e-6);
            Assert.AreEqual(0.00, result.Point.Value.Z, 1e-9);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_ParsesPolarCoordinates_FromOriginWhenNoBase()
        {
            // Arrange - no last point, no base point
            ContextMock.Setup(c => c.GetLastPoint()).Returns((Point3D?)null);

            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act - polar coordinates should use origin (0,0,0)
            var processed = helper.ProcessKeyboardInput("@10<0");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);

            // 0 degrees = positive X direction
            // Expected: (10, 0, 0)
            Assert.AreEqual(10.0, result.Point.Value.X, 1e-6);
            Assert.AreEqual(0.00, result.Point.Value.Y, 1e-9);
            Assert.AreEqual(0.00, result.Point.Value.Z, 1e-9);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_ParsesPolarCoordinates_NegativeDistance()
        {
            // Arrange
            var basePoint = new Point3D(10, 10, 0);
            ContextMock.Setup(c => c.GetLastPoint()).Returns(basePoint);

            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act - negative distance (opposite direction)
            var processed = helper.ProcessKeyboardInput("@-10<0");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);

            // -10 at 0° = move left instead of right
            // Expected: (0, 10, 0)
            Assert.AreEqual(0.00, result.Point.Value.X, 1e-6);
            Assert.AreEqual(10.0, result.Point.Value.Y, 1e-6);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_ParsesPolarCoordinates_WithDegreeSymbol()
        {
            // Arrange
            var basePoint = new Point3D(0, 0, 0);
            ContextMock.Setup(c => c.GetLastPoint()).Returns(basePoint);

            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            });

            // Act - with 'd' suffix for degrees
            var processed = helper.ProcessKeyboardInput("@10<45d");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);

            double angle45Rad = 45 * Math.PI / 180.0;
            double expectedX = 10 * Math.Cos(angle45Rad);
            double expectedY = 10 * Math.Sin(angle45Rad);

            Assert.AreEqual(expectedX, result.Point.Value.X, 1e-6);
            Assert.AreEqual(expectedY, result.Point.Value.Y, 1e-6);
        }

        #endregion

        #region Keyboard Input Tests - Keyword Handling

        [TestMethod]
        public async Task ProcessKeyboardInput_MatchesExactKeyword()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var keywords = new[] { "Arc", "Line", "Circle" };
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify next point:",
                Keywords = keywords
            });

            // Act - type exact keyword
            var processed = helper.ProcessKeyboardInput("Arc");

            // Assert
            Assert.IsTrue(processed, "Keyword should be processed");
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Keyword, result.ResultType);
            Assert.AreEqual("Arc", result.Keyword);

            // Verify keyword was echoed to output
            ContextMock.Verify(c => c.OutputMessage(It.IsRegex("Keyword.*Arc")), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_MatchesPartialKeyword()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var keywords = new[] { "Arc", "Line", "Circle" };
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify next point:",
                Keywords = keywords
            });

            // Act - type partial keyword (should match first one that starts with it)
            var processed = helper.ProcessKeyboardInput("A");

            // Assert
            Assert.IsTrue(processed);
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Keyword, result.ResultType);
            Assert.AreEqual("Arc", result.Keyword);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_KeywordIsCaseInsensitive()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var keywords = new[] { "Arc", "Line", "Circle" };
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify next point:",
                Keywords = keywords
            });

            // Act - lowercase input
            var processed = helper.ProcessKeyboardInput("arc");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Keyword, result.ResultType);
            Assert.AreEqual("Arc", result.Keyword);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_MatchesPartialKeyword_CaseInsensitive()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var keywords = new[] { "Arc", "Line", "Circle" };
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify next point:",
                Keywords = keywords
            });

            // Act - partial lowercase
            var processed = helper.ProcessKeyboardInput("l");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Keyword, result.ResultType);
            Assert.AreEqual("Line", result.Keyword);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_NoMatchReturnsArbitrary_WithoutKeywords()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:"
            }); // No keywords

            // Act - type something that's not a valid coordinate
            var processed = helper.ProcessKeyboardInput("invalid");

            // Assert
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Arbitrary, result.ResultType);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_NoMatchWithKeywords_TriesCoordinateParsing()
        {
            // Arrange
            var helper = CreateGetPointInput();
            var keywords = new[] { "Arc", "Line" };
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:",
                Keywords = keywords
            });

            // Act - type valid coordinates (should take precedence over failed keyword match)
            var processed = helper.ProcessKeyboardInput("10 20 30");

            // Assert - should parse as point, not try keyword matching
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Point, result.ResultType);
            Assert.AreEqual(10.0, result.Point.Value.X, 1e-9);
        }

        #endregion

        #region Keyboard Input Tests - Default Value & Arbitrary Input

        [TestMethod]
        public async Task ProcessKeyboardInput_ReturnsDefaultValue_OnEmptyInput()
        {
            // Arrange
            var defaultValue = 42.0;
            var helper = CreateGetPointInput();
            var getTask = helper.GetPointOrKeywordAsync(new InputParams
            {
                Prompt = "Specify point:",
                DefaultValue = defaultValue
            });

            // Act - empty input with default value set
            var processed = helper.ProcessKeyboardInput("");

            // Assert
            //Assert.IsTrue(processed);
            var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Keyword, result.ResultType);
            Assert.AreEqual(string.Empty, result.Keyword, "Empty keyword signals default acceptance");
        }

        //[TestMethod]
        //public async Task ProcessKeyboardInput_AllowsArbitraryInput_WhenEnabled()
        //{
        //    // Arrange
        //    var helper = CreateGetPointInput();
        //    helper.AllowArbitraryInput = true;
        //    var getTask = helper.GetPointOrKeywordAsync(new InputParams
        //    {
        //        Prompt = "Specify point:"
        //    });

        //    // Act - any non-empty text should be accepted
        //    var processed = helper.ProcessKeyboardInput("random text");

        //    // Assert
        //    //Assert.IsTrue(processed, "processed was false");
        //    var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
        //    Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Arbitrary, result.ResultType);
        //    Assert.AreEqual("random text", result.Keyword);
        //}

        //[TestMethod]
        //public async Task ProcessKeyboardInput_RejectsArbitraryInput_WhenDisabled()
        //{
        //    // Arrange
        //    var helper = CreateGetPointInput();
        //    helper.AllowArbitraryInput = false; // Default
        //    var getTask = helper.GetPointOrKeywordAsync(new InputParams
        //    {
        //        Prompt = "Specify point:"
        //    });

        //    // Act - arbitrary text that's not a valid coordinate
        //    var processed = helper.ProcessKeyboardInput("random text");

        //    // Assert - should still return Arbitrary type for invalid coordinates
        //    var result = await getTask.WaitAsync(TimeSpan.FromSeconds(1));
        //    Assert.AreEqual(UI.Commands.InputHelpers.InputResult.InputResultType.Arbitrary, result.ResultType);
        //}

        [TestMethod]
        public async Task ProcessKeyboardInput_ReturnsCancel_WhenNoPendingTask()
        {
            // Arrange
            var helper = CreateGetPointInput();
            // Don't start any async operation

            // Act - try to process input without a pending task
            var processed = helper.ProcessKeyboardInput("10 20 30");

            // Assert
            Assert.IsFalse(processed, "Should not process when no task is pending");
        }

        #endregion
    }
}