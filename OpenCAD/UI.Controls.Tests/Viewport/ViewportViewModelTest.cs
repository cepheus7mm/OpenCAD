using System.Numerics;
using System.Windows;
using System.Windows.Input;
using OpenCAD;
using OpenCAD.Geometry;
using UI.Controls.Viewport;

namespace UI.Controls.Tests.Viewport
{
    [TestClass]
    public sealed class ViewportViewModelTests
    {
        private ViewportViewModel _viewModel = null!;
        private OpenCADDocument _document = null!;

        [TestInitialize]
        public void BeforeEachTest()
        {
            _document = new OpenCADDocument();
            _viewModel = new ViewportViewModel(_document);
        }

        [TestMethod]
        public void HandleMouseDown_InSelectionMode_WithNoObjectHighlighted_ShouldSwitchToWindowSelection()
        {
            // Arrange
            _viewModel.CurrentInputMode = ViewportViewModel.InputMode.Selection;
            var mousePos = new Point(100, 100);
            var worldPos = new Vector3(10, 10, 0);

            // Ensure no object is highlighted
            Assert.IsNull(_viewModel.HighlightedObject);
            Assert.AreEqual(0, _viewModel.SelectedObjects.Count);

            // Act
            var result = _viewModel.HandleMouseDown(MouseButton.Left, mousePos, worldPos);

            // Assert
            Assert.AreEqual(ViewportViewModel.InputMode.WindowSelection, _viewModel.CurrentInputMode, 
                "CurrentInputMode should switch to WindowSelection when left-clicking in Selection mode with no highlighted object");
        }

        [TestMethod]
        public void HandleMouseDown_InSelectionMode_WithHighlightedObject_ShouldNotSwitchToWindowSelection()
        {
            // Arrange
            _viewModel.CurrentInputMode = ViewportViewModel.InputMode.Selection;
            var mousePos = new Point(100, 100);
            var worldPos = new Vector3(10, 10, 0);

            // Add an object to the document and highlight it
            var testObject = new Line(new Point3D(100, 100, 0), new Point3D(100, 101, 0), _document);
            _viewModel.AddObject(testObject);
            _viewModel.HighlightedObject = testObject;

            // Act
            var result = _viewModel.HandleMouseDown(MouseButton.Left, mousePos, worldPos);

            // Assert
            Assert.AreEqual(ViewportViewModel.InputMode.Selection, _viewModel.CurrentInputMode, 
                "CurrentInputMode should remain Selection when clicking on a highlighted object");
            Assert.AreEqual(1, _viewModel.SelectedObjects.Count, 
                "Object should be added to selection");
        }

        [TestMethod]
        public void HandleMouseDown_InPointPickingMode_WithLeftClick_ShouldNotSwitchToWindowSelection()
        {
            // Arrange
            _viewModel.EnablePointPickingMode();
            var mousePos = new Point(100, 100);
            var worldPos = new Vector3(10, 10, 0);

            // Act
            var result = _viewModel.HandleMouseDown(MouseButton.Left, mousePos, worldPos);

            // Assert
            Assert.AreEqual(ViewportViewModel.InputMode.PointPicking, _viewModel.CurrentInputMode, 
                "CurrentInputMode should remain PointPicking mode");
        }

        [TestMethod]
        public void WindowSelection_ShouldHighlightAllObjectsInsideRectangle()
        {
            // Arrange - Create objects at known positions
            var line1 = new Line(new Point3D(5, 5, 0), new Point3D(5, 10, 0), _document);   // Inside
            var line2 = new Line(new Point3D(15, 15, 0), new Point3D(15, 20, 0), _document); // Inside
            var line3 = new Line(new Point3D(50, 50, 0), new Point3D(50, 55, 0), _document); // Outside
            
            _viewModel.AddObject(line1);
            _viewModel.AddObject(line2);
            _viewModel.AddObject(line3);

            // Start window selection at (0, 0)
            var startMousePos = new Point(0, 0);
            var startWorldPos = new Vector3(0, 0, 0);
            
            // Switch to WindowSelection mode
            //_viewModel.CurrentInputMode = ViewportViewModel.InputMode.WindowSelection;
            var startResult = _viewModel.HandleMouseDown(MouseButton.Left, startMousePos, startWorldPos);
            
            // Define selection rectangle from (0,0) to (25,25) - should contain line1 and line2
            var endMousePos = new Point(25, 25);
            var endWorldPos = new Vector3(25, 25, 0);

            // Act - Simulate mouse move to update the window selection rectangle
            var moveResult = _viewModel.HandleMouseMove(
                endMousePos, 
                new OpenCAD.Geometry.Helpers.Vector3D(endWorldPos.X, endWorldPos.Y, endWorldPos.Z),
                MouseButtonState.Released, 
                MouseButtonState.Released, 
                1.0f, 
                out _);

            // Assert
            Assert.IsNotNull(_viewModel.WindowSelectionPreviewObjects, 
                "WindowSelectionPreviewObjects should not be null");
            Assert.AreEqual(2, _viewModel.WindowSelectionPreviewObjects.Count, 
                "Should highlight 2 objects (line1 and line2) inside the selection rectangle");
            Assert.IsTrue(_viewModel.WindowSelectionPreviewObjects.Contains(line1), 
                "line1 should be in preview objects");
            Assert.IsTrue(_viewModel.WindowSelectionPreviewObjects.Contains(line2), 
                "line2 should be in preview objects");
            Assert.IsFalse(_viewModel.WindowSelectionPreviewObjects.Contains(line3), 
                "line3 should NOT be in preview objects (it's outside the rectangle)");
        }

        [TestMethod]
        public void WindowSelection_OnMouseUp_ShouldSelectHighlightedObjects()
        {
            // Arrange - Create objects inside selection rectangle
            var line1 = new Line(new Point3D(5, 5, 0), new Point3D(5, 10, 0), _document);
            var line2 = new Line(new Point3D(15, 15, 0), new Point3D(15, 20, 0), _document);
            
            _viewModel.AddObject(line1);
            _viewModel.AddObject(line2);

            // Start window selection
            var startResult = _viewModel.HandleMouseDown(MouseButton.Left, new Point(0, 0), new Vector3(0, 0, 0));
            
            // Move mouse to define rectangle
            var moveResult = _viewModel.HandleMouseMove(
                new Point(25, 25),
                new OpenCAD.Geometry.Helpers.Vector3D(25, 25, 0),
                MouseButtonState.Released,
                MouseButtonState.Released,
                1.0f,
                out _);

            // Act - Release mouse button to finalize selection
            var upResult = _viewModel.HandleMouseUp(MouseButton.Left, new Point(25, 25), new Vector3(25, 25, 0));

            // Assert
            Assert.AreEqual(ViewportViewModel.InputMode.Selection, _viewModel.CurrentInputMode,
                "Should return to Selection mode after mouse up");
            Assert.AreEqual(2, _viewModel.SelectedObjects.Count,
                "Should have selected 2 objects");
            Assert.IsTrue(_viewModel.SelectedObjects.Contains(line1),
                "line1 should be selected");
            Assert.IsTrue(_viewModel.SelectedObjects.Contains(line2),
                "line2 should be selected");
            Assert.AreEqual(0, _viewModel.WindowSelectionPreviewObjects.Count,
                "Preview objects should be cleared after selection");
        }
    }
}
