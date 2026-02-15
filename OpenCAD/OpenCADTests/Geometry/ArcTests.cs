using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Geometry.Calculator;
using System.Numerics;

namespace OpenCADTests;

[TestClass]
public class ArcTests
{
    [TestMethod]
    [DataRow(0, Math.PI / 2, Math.PI / 2)]
    [DataRow(Math.PI / 2, Math.PI, Math.PI / 2)]
    [DataRow(Math.PI, 3 * Math.PI / 2, Math.PI / 2)]
    [DataRow(3 * Math.PI / 2, 2 * Math.PI, Math.PI / 2)]

    [DataRow(3 * Math.PI / 2, Math.PI / 2, Math.PI / 2)]
    [DataRow(7 * Math.PI / 4, Math.PI / 4, Math.PI / 2)]

    [DataRow(-Math.PI / 2, 0, Math.PI / 2)]
    [DataRow(-Math.PI, -Math.PI / 2, Math.PI / 2)]

    [DataRow(Math.PI / 2, 0, Math.PI / 2)]
    [DataRow(Math.PI, Math.PI / 2, Math.PI / 2)]

    [DataRow(0, 2 * Math.PI, Math.PI / 2)]
    [DataRow(0, 4 * Math.PI, Math.PI / 2)]

    [DataRow(0, Math.PI / 2, 2 * Math.PI)]
    [DataRow(0, Math.PI / 2, -2 * Math.PI)]
    [DataRow(0, Math.PI / 2, 5 * Math.PI / 2)]

    [DataRow(0, 1e-6, Math.PI / 2)]
    [DataRow(1e-6, 2e-6, Math.PI / 2)]
    public void ArcTransform_Rotate90Degrees(double startAngle, double endAngle, double rotationAngle)
    {
        var center = new Point3D(0, 0, 0);
        var arc = new Arc(center, 1, startAngle, endAngle);

        var sweepBefore = arc.GetSweepAngle();

        var m = TransformBuilder.Rotate(new Vector2(0, 0), rotationAngle);
        var result = (Arc)arc.Transform(m);

        var sweepAfter = result.GetSweepAngle();

        Assert.AreEqual(0, result.Center.X, 1e-9);
        Assert.AreEqual(0, result.Center.Y, 1e-9);

        Assert.AreEqual(1, result.Radius, 1e-9);
        var expectedStartAngle = GeometricCalculator.NormalizeUnsigned(startAngle + rotationAngle);
        Assert.AreEqual(expectedStartAngle, result.StartAngle, 1e-9);
        var expectedEndAngle = GeometricCalculator.NormalizeUnsigned(endAngle + rotationAngle);
        Assert.AreEqual(expectedEndAngle, result.EndAngle, 1e-9);

        Assert.IsLessThan(MathF.Tau, startAngle, "Start angle should be less than 2Pi");

        Assert.AreEqual(sweepBefore, sweepAfter, 1e-9);

        Assert.IsGreaterThanOrEqualTo(0, (result.SweepAngle) * sweepBefore,
            "Sweep direction changed");
    }

    [TestMethod]
    [DataRow(0, Math.PI / 2)]                     // Q1 arc
    [DataRow(Math.PI / 2, Math.PI)]               // Q2 arc
    [DataRow(Math.PI, 3 * Math.PI / 2)]           // Q3 arc
    [DataRow(3 * Math.PI / 2, 2 * Math.PI)]       // Q4 arc

    [DataRow(3 * Math.PI / 2, Math.PI / 2)]       // crosses seam
    [DataRow(7 * Math.PI / 4, Math.PI / 4)]       // small seam-crossing arc

    [DataRow(-Math.PI / 2, 0)]                    // negative start
    [DataRow(-Math.PI, -Math.PI / 2)]             // negative arc

    [DataRow(Math.PI / 2, 0)]                     // CW arc
    [DataRow(Math.PI, Math.PI / 2)]               // CW arc

    [DataRow(0, 2 * Math.PI)]                     // full circle
    [DataRow(0, 4 * Math.PI)]                     // double sweep

    [DataRow(0, 1e-6)]                             // tiny arc
    [DataRow(1e-6, 2e-6)]                           // tiny arc
    public void ArcTransform_MirrorVertical(double startAngle, double endAngle)
    {
        var center = new Point3D(0, 0, 0);
        var arc = new Arc(center, 1, startAngle, endAngle);

        double sweepBefore = arc.GetSweepAngle();

        // Mirror across vertical axis (x = 0)
        var m = TransformBuilder.Mirror(
            new Vector2(0, -10),   // any two points on x=0 line
            new Vector2(0, 10)
        );

        var result = (Arc)arc.Transform(m);

        double sweepAfter = result.GetSweepAngle();

        //
        // 1. Center should mirror correctly
        //
        Assert.AreEqual(-arc.Center.X, result.Center.X, 1e-9);
        Assert.AreEqual(arc.Center.Y, result.Center.Y, 1e-9);

        //
        // 2. Radius unchanged
        //
        Assert.AreEqual(arc.Radius, result.Radius, 1e-9);

        //
        // 3. Sweep direction MUST flip
        //
        Assert.AreEqual(sweepBefore, sweepAfter, 1e-9);

        //
        // 4. StartAngle must match mirrored start point
        //
        var originalStart = arc.EndPoint;
        var mirroredStart = originalStart.Transform(m);
        var expectedStartAngle = GeometricCalculator.NormalizeUnsigned(
            Math.Atan2(mirroredStart.Y - result.Center.Y,
                       mirroredStart.X - result.Center.X)
        );

        Assert.AreEqual(expectedStartAngle, result.StartAngle, 1e-9);

        //
        // 5. EndAngle must match mirrored end point
        //
        var originalEnd = arc.StartPoint;
        var mirroredEnd = originalEnd.Transform(m);
        var expectedEndAngle = GeometricCalculator.NormalizeUnsigned(
            Math.Atan2(mirroredEnd.Y - result.Center.Y,
                       mirroredEnd.X - result.Center.X)
        );

        Assert.AreEqual(expectedEndAngle, result.EndAngle, 1e-9);
    }

}
