using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands;
using UI.Controls.Viewport;

namespace UI.Commands.Tests
{
    [TestClass]
    public class PointInputHelperTests
    {
        private Mock<ICommandContext>? _mockContext;
        private Mock<ViewportViewModel>? _mockViewModel;
        private PointInputHelper? _helper;

        [TestInitialize]
        public void Setup()
        {
            _mockContext = new Mock<ICommandContext>();
            _mockViewModel = new Mock<ViewportViewModel>();
            _helper = new PointInputHelper(_mockContext.Object, _mockViewModel.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _helper?.Cancel();
            _mockContext = null;
            _mockViewModel = null;
            _helper = null;
        }

        #region Constructor Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullContext_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var helper = new PointInputHelper(null!, _mockViewModel!.Object);
        }

        [TestMethod]
        public void Constructor_WithNullViewModel_DoesNotThrow()
        {
            // Arrange & Act
            var helper = new PointInputHelper(_mockContext!.Object, null);

            // Assert
            Assert.IsNotNull(helper);
        }

        [TestMethod]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var helper = new PointInputHelper(_mockContext!.Object, _mockViewModel!.Object);

            // Assert
            Assert.IsNotNull(helper);
        }

        #endregion

        #region GetPointAsync Tests

        [TestMethod]
        public async Task GetPointAsync_WithNullViewModel_ReturnsNull()
        {
            // Arrange
            var helperWithoutViewModel = new PointInputHelper(_mockContext!.Object, null);

            // Act
            var result = await helperWithoutViewModel.GetPointAsync("Test prompt");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPointAsync_WithValidPrompt_OutputsPromptMessage()
        {
            // Arrange
            var prompt = "Specify first point:";
            _mockContext!.Setup(c => c.GetLastPoint()).Returns((Point3D?)null);

            // Act
            var cts = new CancellationTokenSource(100);
            await _helper!.GetPointAsync(prompt, cancellationToken: cts.Token);

            // Assert
            _mockContext.Verify(c => c.OutputMessage(It.Is<string>(s => s.Contains(prompt))), Times.Once);
        }

        [TestMethod]
        public async Task GetPointAsync_WithAllowLastPoint_IncludesLastPointInPrompt()
        {
            // Arrange
            var prompt = "Specify next point:";
            var lastPoint = new Point3D(1.0, 2.0, 3.0);
            _mockContext!.Setup(c => c.GetLastPoint()).Returns(lastPoint);

            // Act
            var cts = new CancellationTokenSource(100);
            await _helper!.GetPointAsync(prompt, allowLastPoint: true, cancellationToken: cts.Token);

            // Assert
            _mockContext.Verify(c => c.OutputMessage(It.Is<string>(s =>
                s.Contains(prompt) &&
                s.Contains(lastPoint.X.ToString()) &&
                s.Contains(lastPoint.Y.ToString()) &&
                s.Contains(lastPoint.Z.ToString()))), Times.Once);
        }

        [TestMethod]
        public async Task GetPointAsync_WithCancellationToken_CancelsOperation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var task = _helper!.GetPointAsync("Test prompt", cancellationToken: cts.Token);

            // Act
            await Task.Delay(50);
            cts.Cancel();
            var result = await task;

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetPointAsync_EnablesAndDisablesPointPickingMode()
        {
            // Arrange
            var cts = new CancellationTokenSource(100);

            // Act
            await _helper!.GetPointAsync("Test prompt", cancellationToken: cts.Token);

            // Assert
            _mockViewModel!.Verify(v => v.EnablePointPickingMode(), Times.Once);
            _mockViewModel.Verify(v => v.DisablePointPickingMode(), Times.Once);
        }

        #endregion

        #region GetPointOrKeywordAsync Tests

        [TestMethod]
        public async Task GetPointOrKeywordAsync_WithKeywords_AcceptsFullMatch()
        {
            // Arrange
            var keywords = new[] { "Line", "Arc", "Circle" };
            var prompt = "Enter option:";

            var task = _helper!.GetPointOrKeywordAsync(prompt, keywords: keywords);

            // Act
            await Task.Delay(50);
            _helper.ProcessKeyboardInput("Line");
            var result = await task;

            // Assert
            Assert.IsTrue(result.IsKeyword);
            Assert.AreEqual("Line", result.Keyword);
            Assert.IsFalse(result.IsCancelled);
        }

        [TestMethod]
        public async Task GetPointOrKeywordAsync_WithKeywords_AcceptsPartialMatch()
        {
            // Arrange
            var keywords = new[] { "Close", "Continue" };
            var prompt = "Enter option:";

            var task = _helper!.GetPointOrKeywordAsync(prompt, keywords: keywords);

            // Act
            await Task.Delay(50);
            _helper.ProcessKeyboardInput("Cl"); // Should match "Close"
            var result = await task;

            // Assert
            Assert.IsTrue(result.IsKeyword);
            Assert.AreEqual("Close", result.Keyword);
        }

        [TestMethod]
        public async Task GetPointOrKeywordAsync_WithKeywords_RejectsInvalidKeyword()
        {
            // Arrange
            var keywords = new[] { "Line", "Arc" };
            var prompt = "Enter option:";

            var task = _helper!.GetPointOrKeywordAsync(prompt, keywords: keywords);

            // Act
            await Task.Delay(50);
            _helper.ProcessKeyboardInput("InvalidKeyword");
            var result = await task;

            // Assert
            Assert.IsFalse(result.IsKeyword);
            Assert.IsTrue(result.IsCancelled);
        }

        [TestMethod]
        public async Task GetPointOrKeywordAsync_WithNullViewModel_ReturnsCancelled()
        {
            // Arrange
            var helperWithoutViewModel = new PointInputHelper(_mockContext!.Object, null);

            // Act
            var result = await helperWithoutViewModel.GetPointOrKeywordAsync("Test prompt");

            // Assert
            Assert.IsTrue(result.IsCancelled);
            Assert.IsFalse(result.IsKeyword);
            Assert.IsFalse(result.IsPoint);
        }

        [TestMethod]
        public async Task GetPointOrKeywordAsync_WithBasePoint_EnablesPreviewMode()
        {
            // Arrange
            var basePoint = new Point3D(0, 0, 0);
            var cts = new CancellationTokenSource(100);

            // Act
            await _helper!.GetPointOrKeywordAsync("Test prompt", basePoint: basePoint, cancellationToken: cts.Token);

            // Assert
            _mockViewModel!.Verify(v => v.EnablePreviewMode(It.IsAny<Action<Point3D>>()), Times.Once);
            _mockViewModel.Verify(v => v.DisablePreviewMode(), Times.Once);
            _mockViewModel.Verify(v => v.AddTempPoint(basePoint), Times.Once);
            _mockViewModel.Verify(v => v.ClearTempPoints(), Times.AtLeastOnce);
        }

        #endregion

        #region ProcessKeyboardInput Tests

        [TestMethod]
        public void ProcessKeyboardInput_WithNoPendingTask_ReturnsFalse()
        {
            // Arrange
            var input = "1 2 3";

            // Act
            var result = _helper!.ProcessKeyboardInput(input);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithValidSpaceSeparatedCoordinates_ParsesPoint()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("10 20 30");
            var result = await task;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(10.0, result.X);
            Assert.AreEqual(20.0, result.Y);
            Assert.AreEqual(30.0, result.Z);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithCommaSeparatedCoordinates_ParsesPoint()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("5.5,10.5,15.5");
            var result = await task;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(5.5, result.X);
            Assert.AreEqual(10.5, result.Y);
            Assert.AreEqual(15.5, result.Z);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithMixedDelimiters_ParsesPoint()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("1.5 2.5,3.5");
            var result = await task;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1.5, result.X);
            Assert.AreEqual(2.5, result.Y);
            Assert.AreEqual(3.5, result.Z);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithInvalidFormat_ReturnsCancelled()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("invalid input");
            var result = await task;

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithEmptyInputAndAllowLastPoint_UsesLastPoint()
        {
            // Arrange
            var lastPoint = new Point3D(100, 200, 300);
            _mockContext!.Setup(c => c.GetLastPoint()).Returns(lastPoint);

            var task = _helper!.GetPointAsync("Test prompt", allowLastPoint: true);
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("");
            var result = await task;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(lastPoint.X, result.X);
            Assert.AreEqual(lastPoint.Y, result.Y);
            Assert.AreEqual(lastPoint.Z, result.Z);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithValidPoint_SetsLastPoint()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("1 2 3");
            await task;

            // Assert
            _mockContext!.Verify(c => c.SetLastPoint(It.Is<Point3D>(p =>
                p.X == 1.0 && p.Y == 2.0 && p.Z == 3.0)), Times.Once);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithValidPoint_OutputsPointMessage()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("1 2 3");
            await task;

            // Assert
            _mockContext!.Verify(c => c.OutputMessage(It.Is<string>(s =>
                s.Contains("1") && s.Contains("2") && s.Contains("3"))), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithMatchingKeyword_OutputsKeywordMessage()
        {
            // Arrange
            var keywords = new[] { "Close", "Undo" };
            var task = _helper!.GetPointOrKeywordAsync("Test prompt", keywords: keywords);
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("C"); // Should match "Close"
            var result = await task;

            // Assert
            Assert.IsTrue(result.IsKeyword);
            Assert.AreEqual("Close", result.Keyword);
            _mockContext!.Verify(c => c.OutputMessage(It.Is<string>(s =>
                s.Contains("Close"))), Times.Once);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithCaseInsensitiveKeyword_Matches()
        {
            // Arrange
            var keywords = new[] { "Circle" };
            var task = _helper!.GetPointOrKeywordAsync("Test prompt", keywords: keywords);
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("circle"); // Lowercase should match
            var result = await task;

            // Assert
            Assert.IsTrue(result.IsKeyword);
            Assert.AreEqual("Circle", result.Keyword);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithBasePoint_SetsPreviewPoint()
        {
            // Arrange
            var basePoint = new Point3D(0, 0, 0);
            var task = _helper!.GetPointOrKeywordAsync("Test prompt", basePoint: basePoint);
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("10 20 30");
            await task;

            // Assert
            _mockViewModel!.Verify(v => v.SetPreviewPoint(It.Is<Point3D>(p =>
                p.X == 10.0 && p.Y == 20.0 && p.Z == 30.0)), Times.Once);
        }

        #endregion

        #region Cancel Tests

        [TestMethod]
        public async Task Cancel_WithPendingTask_CancelsOperation()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.Cancel();
            var result = await task;

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Cancel_WithNoPendingTask_DoesNotThrow()
        {
            // Arrange & Act
            _helper!.Cancel();

            // Assert - no exception thrown
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task Cancel_MultipleTimes_DoesNotThrow()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.Cancel();
            _helper.Cancel();
            _helper.Cancel();

            var result = await task;

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region Edge Cases and Data-Driven Tests

        [TestMethod]
        [DataRow("0 0 0", 0.0, 0.0, 0.0)]
        [DataRow("-10 -20 -30", -10.0, -20.0, -30.0)]
        [DataRow("1.5 2.5 3.5", 1.5, 2.5, 3.5)]
        [DataRow("100.123 200.456 300.789", 100.123, 200.456, 300.789)]
        public async Task ProcessKeyboardInput_WithVariousValidFormats_ParsesCorrectly(
            string input, double expectedX, double expectedY, double expectedZ)
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput(input);
            var result = await task;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedX, result.X, 0.001);
            Assert.AreEqual(expectedY, result.Y, 0.001);
            Assert.AreEqual(expectedZ, result.Z, 0.001);
        }

        [TestMethod]
        [DataRow("1 2")] // Only 2 coordinates
        [DataRow("1 2 3 4")] // Too many coordinates
        [DataRow("a b c")] // Non-numeric
        [DataRow("1,2")] // Only 2 coordinates with comma
        [DataRow("")] // Empty (when not allowing last point)
        [DataRow("   ")] // Whitespace only
        public async Task ProcessKeyboardInput_WithInvalidFormats_ReturnsCancelled(string input)
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt", allowLastPoint: false);
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput(input);
            var result = await task;

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithNegativeCoordinates_ParsesCorrectly()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("-1.5 -2.5 -3.5");
            var result = await task;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(-1.5, result.X);
            Assert.AreEqual(-2.5, result.Y);
            Assert.AreEqual(-3.5, result.Z);
        }

        [TestMethod]
        public async Task ProcessKeyboardInput_WithVeryLargeNumbers_ParsesCorrectly()
        {
            // Arrange
            var task = _helper!.GetPointAsync("Test prompt");
            await Task.Delay(50);

            // Act
            _helper.ProcessKeyboardInput("999999.999 1000000.001 -999999.999");
            var result = await task;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(999999.999, result.X, 0.001);
            Assert.AreEqual(1000000.001, result.Y, 0.001);
            Assert.AreEqual(-999999.999, result.Z, 0.001);
        }

        #endregion

        #region PointOrKeywordResult Tests

        [TestMethod]
        public void PointOrKeywordResult_WithPoint_IsPointReturnsTrue()
        {
            // Arrange
            var result = new PointOrKeywordResult { Point = new Point3D(1, 2, 3) };

            // Act & Assert
            Assert.IsTrue(result.IsPoint);
            Assert.IsFalse(result.IsKeyword);
            Assert.IsFalse(result.IsCancelled);
        }

        [TestMethod]
        public void PointOrKeywordResult_WithKeyword_IsKeywordReturnsTrue()
        {
            // Arrange
            var result = new PointOrKeywordResult { Keyword = "Test" };

            // Act & Assert
            Assert.IsTrue(result.IsKeyword);
            Assert.IsFalse(result.IsPoint);
            Assert.IsFalse(result.IsCancelled);
        }

        [TestMethod]
        public void PointOrKeywordResult_WithCancelled_IsCancelledReturnsTrue()
        {
            // Arrange
            var result = new PointOrKeywordResult { IsCancelled = true };

            // Act & Assert
            Assert.IsTrue(result.IsCancelled);
            Assert.IsFalse(result.IsPoint);
            Assert.IsFalse(result.IsKeyword);
        }

        [TestMethod]
        public void PointOrKeywordResult_WithEmptyKeyword_IsKeywordReturnsFalse()
        {
            // Arrange
            var result = new PointOrKeywordResult { Keyword = "" };

            // Act & Assert
            Assert.IsFalse(result.IsKeyword);
        }

        [TestMethod]
        public void PointOrKeywordResult_Default_AllPropertiesFalse()
        {
            // Arrange
            var result = new PointOrKeywordResult();

            // Act & Assert
            Assert.IsFalse(result.IsPoint);
            Assert.IsFalse(result.IsKeyword);
            Assert.IsFalse(result.IsCancelled);
        }

        #endregion
    }
}