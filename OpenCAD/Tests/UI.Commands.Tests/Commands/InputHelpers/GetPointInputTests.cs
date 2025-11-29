using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using OpenCAD.Geometry;
using UI.Controls.Viewport;

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
            var getTask = helper.GetPointOrKeywordAsync("Specify point:", allowLastPoint: false, basePoint: null, keywords: null, cancellationToken: CancellationToken.None);

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
            Assert.AreEqual(picked.X, result.Point!.X, 1e-9);
            Assert.AreEqual(picked.Y, result.Point!.Y, 1e-9);
            Assert.AreEqual(picked.Z, result.Point!.Z, 1e-9);

            // Verify that last point was stored and output message was produced
            ContextMock.Verify(c => c.SetLastPoint(It.Is<Point3D>(p => Math.Abs(p.X - picked.X) < 1e-9 && Math.Abs(p.Y - picked.Y) < 1e-9 && Math.Abs(p.Z - picked.Z) < 1e-9)), Times.AtLeastOnce);
            ContextMock.Verify(c => c.OutputMessage(It.IsAny<string>()), Times.AtLeastOnce);

            // After completion cleanup should have run and point picking disabled
            Assert.IsFalse(ViewModel.IsPointPickingMode, "Point picking mode should be disabled after completion and cleanup.");
        }
    }
}
