using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCAD;
using OpenCAD.Geometry;
using UI.Commands.Editing.Fillet;

namespace UI.Commands.Tests;

[TestClass]
public class FilletCommandTests
{
    OpenCADDocument _document = new OpenCADDocument();

    [TestMethod]
    public void FilletSolver_HorizontalVerticalLines_Radius2()
    {
        // Arrange
        var lineA = new Line(_document, new Point3D(0, 0, 0), new Point3D(10, 0, 0));   // horizontal
        var lineB = new Line(_document, new Point3D(10, 0, 0), new Point3D(10, 10, 0)); // vertical

        var pickA = new Point3D(5, 0, 0);   // somewhere on line A
        var pickB = new Point3D(10, 5, 0);  // somewhere on line B

        var solver = new FilletSolver();

        // Act
        bool ok = solver.TrySolve(
            lineA, lineB,
            pickA, pickB,
            radius: 2,
            out var solution);

        // Assert
        Assert.IsTrue(ok, "Solver failed to compute fillet.");

        // Expected tangent points
        var expectedTA = new Point3D(8, 0, 0);
        var expectedTB = new Point3D(10, 2, 0);

        // Expected center
        var expectedCenter = new Point3D(8, 2, 0);

        // Check arc center
        Assert.AreEqual(expectedCenter.X, solution.FilletArc.Center.X, 1e-6);
        Assert.AreEqual(expectedCenter.Y, solution.FilletArc.Center.Y, 1e-6);

        // Check radius
        Assert.AreEqual(2, solution.FilletArc.Radius, 1e-6);

        // Check tangent points
        var arcStart = solution.FilletArc.StartPoint;
        var arcEnd = solution.FilletArc.EndPoint;

        Assert.AreEqual(expectedTA.X, arcStart.X, 1e-6);
        Assert.AreEqual(expectedTA.Y, arcStart.Y, 1e-6);

        Assert.AreEqual(expectedTB.X, arcEnd.X, 1e-6);
        Assert.AreEqual(expectedTB.Y, arcEnd.Y, 1e-6);
    }
}
