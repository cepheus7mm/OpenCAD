using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;

namespace OpenCADTests;

[TestClass]
public class ArcSolverTests
{
    private const double Radius = 1.0;
    private const double Tolerance = 1e-12;
    private static readonly Point3D Start = new Point3D(0, 0, 0);

    private static readonly List<double> TestAngles = new List<double>
    {
        Math.PI / 6,    // 30°
        Math.PI / 4,    // 45°
        Math.PI / 3,    // 60°
        Math.PI / 2,    // 90°
        Math.PI,        // 180°
        -Math.PI / 6,   // -30°
        -Math.PI / 4,   // -45°
        -Math.PI / 3,   // -60°
        -Math.PI / 2,   // -90°
        -Math.PI        // -180°
    };

    /// <summary>
    /// Helper method to compute center and endpoint from included angle.
    /// Center is calculated as cos(π/2 + angle/2), sin(π/2 + angle/2) for robustness.
    /// </summary>
    private (Point3D center, Point3D end) ComputeCenterAndEnd(double includedAngle)
    {
        double halfAngle = includedAngle / 2.0;
        double centerAngle = Math.PI / 2.0 + halfAngle;
        
        Point3D center = new Point3D(
            Radius * Math.Cos(centerAngle),
            Radius * Math.Sin(centerAngle),
            0);

        // Start is at angle (centerAngle - π/2) from center
        double startAngle = centerAngle - Math.PI;
        
        // End is at startAngle + includedAngle
        double endAngle = startAngle + includedAngle;
        
        Point3D end = new Point3D(
            center.X + Radius * Math.Cos(endAngle),
            center.Y + Radius * Math.Sin(endAngle),
            0);

        return (center, end);
    }

    /// <summary>
    /// Verifies geometric consistency of the arc solution.
    /// </summary>
    private void VerifyArcSolution(ArcSolution solution, Point3D? expectedEnd = null, double? expectedBulge = null)
    {
        // Verify start point is always (0,0,0)
        Assert.AreEqual(0.0, solution.Start.X, Tolerance, "Start X should be 0");
        Assert.AreEqual(0.0, solution.Start.Y, Tolerance, "Start Y should be 0");
        Assert.AreEqual(0.0, solution.Start.Z, Tolerance, "Start Z should be 0");

        // If expected endpoint provided, verify it
        if (expectedEnd.HasValue)
        {
            Assert.AreEqual(expectedEnd.Value.X, solution.End.X, Tolerance, "End X mismatch");
            Assert.AreEqual(expectedEnd.Value.Y, solution.End.Y, Tolerance, "End Y mismatch");
            Assert.AreEqual(expectedEnd.Value.Z, solution.End.Z, Tolerance, "End Z mismatch");
        }

        // If expected bulge provided, verify it
        if (expectedBulge.HasValue)
        {
            Assert.AreEqual(expectedBulge.Value, solution.Bulge, Tolerance, "Bulge mismatch");
        }

        // Verify radius consistency: distance from center to start ≈ distance from center to end ≈ radius
        double radiusToStart = Math.Sqrt(
            Math.Pow(solution.Start.X - solution.Center.X, 2) +
            Math.Pow(solution.Start.Y - solution.Center.Y, 2) +
            Math.Pow(solution.Start.Z - solution.Center.Z, 2));
        
        double radiusToEnd = Math.Sqrt(
            Math.Pow(solution.End.X - solution.Center.X, 2) +
            Math.Pow(solution.End.Y - solution.Center.Y, 2) +
            Math.Pow(solution.End.Z - solution.Center.Z, 2));

        Assert.AreEqual(solution.Radius, radiusToStart, Tolerance, "Radius to start mismatch");
        Assert.AreEqual(solution.Radius, radiusToEnd, Tolerance, "Radius to end mismatch");

        // Verify chord length consistency
        double calculatedChord = Math.Sqrt(
            Math.Pow(solution.End.X - solution.Start.X, 2) +
            Math.Pow(solution.End.Y - solution.Start.Y, 2) +
            Math.Pow(solution.End.Z - solution.Start.Z, 2));

        Assert.AreEqual(calculatedChord, solution.ChordLength, Tolerance, "Chord length mismatch");

        // Verify tangent perpendicularity
        // StartTangent should be perpendicular to radial vector (Start - Center)
        Vector3D radialStart = new Vector3D(
            solution.Start.X - solution.Center.X,
            solution.Start.Y - solution.Center.Y,
            solution.Start.Z - solution.Center.Z);

        double dotStart = Vector3D.Dot(solution.StartTangent, radialStart);
        Assert.AreEqual(0.0, dotStart, Tolerance, "Start tangent not perpendicular to radial");

        // EndTangent should be perpendicular to radial vector (End - Center)
        Vector3D radialEnd = new Vector3D(
            solution.End.X - solution.Center.X,
            solution.End.Y - solution.Center.Y,
            solution.End.Z - solution.Center.Z);

        double dotEnd = Vector3D.Dot(solution.EndTangent, radialEnd);
        Assert.AreEqual(0.0, dotEnd, Tolerance, "End tangent not perpendicular to radial");

        // Verify bulge/direction consistency
        Assert.AreEqual(solution.Bulge > 0, solution.IsCCW, "Bulge sign does not match IsCCW flag");
    }

    [TestMethod]
    public void TestSolveArc_StartEndBulge()
    {
        foreach (double angle in TestAngles)
        {
            // Compute geometry
            var (center, end) = ComputeCenterAndEnd(angle);
            double bulge = Math.Tan(angle / 4.0);

            // Solve arc
            var solution = ArcSolver.SolveArc(Start, end: end, bulge: bulge);

            // Verify (endpoint and bulge are inputs, so just verify consistency)
            VerifyArcSolution(solution);
        }
    }

    [TestMethod]
    public void TestSolveArc_StartEndCenter()
    {
        foreach (double angle in TestAngles)
        {
            // Compute geometry
            var (center, end) = ComputeCenterAndEnd(angle);

            // Solve arc
            var solution = ArcSolver.SolveArc(Start, end: end, center: center);

            // Verify (endpoint and center are inputs, so just verify consistency)
            VerifyArcSolution(solution);
        }
    }

    [TestMethod]
    public void TestSolveArc_StartCenterIncludedAngle()
    {
        foreach (double angle in TestAngles)
        {
            // Compute geometry
            var (center, expectedEnd) = ComputeCenterAndEnd(angle);
            double expectedBulge = Math.Tan(angle / 4.0);

            // Solve arc
            var solution = ArcSolver.SolveArc(Start, center: center, includedAngle: angle);

            // Verify expected endpoint and bulge
            VerifyArcSolution(solution, expectedEnd, expectedBulge);
        }
    }

    [TestMethod]
    public void TestSolveArc_StartCenterArcLength()
    {
        foreach (double angle in TestAngles)
        {
            // Compute geometry
            var (center, expectedEnd) = ComputeCenterAndEnd(angle);
            double arcLength = Radius * angle; // L = R * θ (signed)
            double expectedBulge = Math.Tan(angle / 4.0);

            // Solve arc
            var solution = ArcSolver.SolveArc(Start, center: center, arcLength: arcLength);

            // Verify expected endpoint and bulge
            VerifyArcSolution(solution, expectedEnd, expectedBulge);
        }
    }

    [TestMethod]
    public void TestSolveArc_StartEndRadius()
    {
        foreach (double angle in TestAngles)
        {
            // Compute geometry
            var (center, end) = ComputeCenterAndEnd(angle);

            // Solve arc - note: may choose different center, so we provide includedAngle as hint
            var solution = ArcSolver.SolveArc(Start, end: end, radius: Radius, includedAngle: angle);

            // Verify (endpoint is input, just verify consistency)
            VerifyArcSolution(solution);
        }
    }

    [TestMethod]
    public void TestSolveArc_StartRadiusIncludedAngle()
    {
        foreach (double angle in TestAngles)
        {
            // Compute geometry
            var (center, expectedEnd) = ComputeCenterAndEnd(angle);
            double expectedBulge = Math.Tan(angle / 4.0);

            // Solve arc - uses default direction (+X), so we need to provide direction
            // The start tangent should be perpendicular to the radial from center to start
            Vector3D radial = new Vector3D(Start.X - center.X, Start.Y - center.Y, 0);
            Vector3D direction = new Vector3D(-radial.Y, radial.X, 0).Normalized; // Perpendicular (CCW)
            if (angle < 0)
            {
                direction = new Vector3D(radial.Y, -radial.X, 0).Normalized; // Perpendicular (CW)
            }

            // Solve arc
            var solution = ArcSolver.SolveArc(Start, radius: Radius, includedAngle: angle, direction: direction);

            // Verify expected endpoint and bulge
            VerifyArcSolution(solution, expectedEnd, expectedBulge);
        }
    }

    [TestMethod]
    public void TestSolveArc_StartEndIncludedAngle()
    {
        foreach (double angle in TestAngles)
        {
            // Compute geometry
            var (center, end) = ComputeCenterAndEnd(angle);
            double expectedBulge = Math.Tan(angle / 4.0);

            // Solve arc
            var solution = ArcSolver.SolveArc(Start, end: end, includedAngle: angle);

            // Verify expected bulge
            VerifyArcSolution(solution, expectedBulge: expectedBulge);
        }
    }

    [TestMethod]
    public void TestSolveArc_StartRadiusArcLength()
    {
        foreach (double angle in TestAngles)
        {
            // Compute geometry
            var (center, expectedEnd) = ComputeCenterAndEnd(angle);
            double arcLength = Radius * angle; // L = R * θ (signed)
            double expectedBulge = Math.Tan(angle / 4.0);

            // Solve arc - uses default direction (+X), so we need to provide direction
            Vector3D radial = new Vector3D(Start.X - center.X, Start.Y - center.Y, 0);
            Vector3D direction = new Vector3D(-radial.Y, radial.X, 0).Normalized; // Perpendicular (CCW)
            if (angle < 0)
            {
                direction = new Vector3D(radial.Y, -radial.X, 0).Normalized; // Perpendicular (CW)
            }

            // Solve arc
            var solution = ArcSolver.SolveArc(Start, radius: Radius, arcLength: arcLength, direction: direction);

            // Verify expected endpoint and bulge
            VerifyArcSolution(solution, expectedEnd, expectedBulge);
        }
    }

    [TestMethod]
    public void TestSolveArc_StartArcLengthIncludedAngle()
    {
        foreach (double angle in TestAngles)
        {
            // Compute geometry
            var (center, expectedEnd) = ComputeCenterAndEnd(angle);
            double arcLength = Radius * angle; // L = R * θ (signed)
            double expectedBulge = Math.Tan(angle / 4.0);

            // Solve arc - uses default direction (+X), so we need to provide direction
            Vector3D radial = new Vector3D(Start.X - center.X, Start.Y - center.Y, 0);
            Vector3D direction = new Vector3D(-radial.Y, radial.X, 0).Normalized; // Perpendicular (CCW)
            if (angle < 0)
            {
                direction = new Vector3D(radial.Y, -radial.X, 0).Normalized; // Perpendicular (CW)
            }

            // Solve arc
            var solution = ArcSolver.SolveArc(Start, arcLength: arcLength, includedAngle: angle, direction: direction);

            // Verify expected endpoint and bulge
            VerifyArcSolution(solution, expectedEnd, expectedBulge);
        }
    }

    [TestMethod]
    public void TestSolveArc_StartEndStraight()
    {
        Point3D end = new Point3D(10, 0, 0);
        
        var solution = ArcSolver.SolveArc(Start, end: end);

        // Verify it's a straight segment
        Assert.AreEqual(0.0, solution.Bulge, Tolerance, "Straight segment should have bulge of 0");
        Assert.AreEqual(false, solution.IsCCW, "Straight segment should not be CCW");
        Assert.AreEqual(10.0, solution.ChordLength, Tolerance, "Chord length mismatch");
        Assert.AreEqual(10.0, solution.ArcLength, Tolerance, "Arc length should equal chord for straight");
    }
}
