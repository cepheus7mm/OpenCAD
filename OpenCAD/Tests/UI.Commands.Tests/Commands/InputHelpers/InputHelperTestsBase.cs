using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using UI.Commands;
using UI.Controls.Viewport;
using OpenCAD.Geometry;

namespace UI.Commands.Tests
{
    /// <summary>
    /// Base class for input helper tests.
    /// Provides common mocks and a concrete ViewModel instance.
    /// </summary>
    [TestClass]
    public class InputHelperTestsBase
    {
        protected Mock<ICommandContext> ContextMock = null!;
        protected ViewportViewModel ViewModel = null!;

        [TestInitialize]
        public void BeforeEach()
        {
            // Strict mock to catch unexpected calls; setup expected members below.
            ContextMock = new Mock<ICommandContext>(MockBehavior.Strict);

            // PostToUI should invoke the action immediately in tests (synchronous).
            ContextMock.Setup(c => c.PostToUI(It.IsAny<Action>()))
                .Callback<Action>(a => a())
                .Verifiable();

            // Allow SetCommandPrompt to be called with any string.
            ContextMock.Setup(c => c.SetCommandPrompt(It.IsAny<string>()))
                .Verifiable();

            // OutputMessage used by helpers to echo things to history.
            ContextMock.Setup(c => c.OutputMessage(It.IsAny<string>()))
                .Verifiable();

            // GetLastPoint defaults to null unless a test overrides it.
            ContextMock.Setup(c => c.GetLastPoint())
                .Returns((Point3D?)null);

            // SetLastPoint should be accepted.
            ContextMock.Setup(c => c.SetLastPoint(It.IsAny<Point3D>()))
                .Verifiable();

            // Provide a concrete ViewModel (no WPF necessary for logic).
            ViewModel = new ViewportViewModel();
        }

        protected UI.Commands.InputHelpers.GetPointInput CreateGetPointInput()
        {
            return new UI.Commands.InputHelpers.GetPointInput(ContextMock.Object, ViewModel);
        }
    }
}