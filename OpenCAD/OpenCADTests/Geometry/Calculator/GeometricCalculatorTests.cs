using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Interfaces;
using System.Net;
using System.Security.Cryptography;

namespace OpenCADTests
{
    [TestClass]
    public class GeometricCalculatorTests
    {
        const double angle0 = 0;
        const double angle15 = Math.PI / 12;
        const double angle30 = Math.PI / 6;
        const double angle45 = Math.PI / 4;
        const double angle60 = Math.PI / 3;
        const double angle90 = Math.PI / 2;
        const double angle120 = 2 * Math.PI / 3;
        const double angle180 = Math.PI;
        const double angle270 = 3 * Math.PI / 2;
        const double angle360 = 2 * Math.PI;

        [TestMethod]
        public void Intersection_IntersectingLines_ReturnsIntersectionPoint()
        {
            var curve1 = new Line(new Point3D(0, 0, 0), new Point3D(10, 0, 0));
            var curve2 = new Line(new Point3D(5, -5, 0), new Point3D(5, 5, 0));

            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.IsTrue(pt1.IsValid);
            Assert.AreEqual(5, pt1.X, 1e-10);
            Assert.AreEqual(0, pt1.Y, 1e-10);
            Assert.AreEqual(0, pt1.Z, 1e-10);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_ParallelLines_ReturnsNotAPoint()
        {
            var curve1 = new Line(new Point3D(0, 0, 0), new Point3D(10, 0, 0));
            var curve2 = new Line(new Point3D(0, 1, 0), new Point3D(10, 1, 0));

            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_CoincidentLines_ReturnsNotAPoint()
        {
            var curve1 = new Line(new Point3D(0, 0, 0), new Point3D(10, 0, 0));
            var curve2 = new Line(new Point3D(0, 0, 0), new Point3D(10, 0, 0));

            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_SkewLines_ReturnsIntersectionInXY()
        {
            var curve1 = new Line(new Point3D(0, 0, 1), new Point3D(10, 0, 1));
            var curve2 = new Line(new Point3D(5, -5, 2), new Point3D(5, 5, 2));

            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.IsTrue(pt1.IsValid);
            Assert.AreEqual(5, pt1.X, 1e-10);
            Assert.AreEqual(0, pt1.Y, 1e-10);
            Assert.AreEqual(1, pt1.Z, 1e-10);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_LineIntersectsCircle_TwoPoints()
        {
            var curve1 = new Circle(new Point3D(0, 0, 0), 5);
            var curve2 = new Line(new Point3D(-10, 0, 0), new Point3D(10, 0, 0));


            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            var x1 = Math.Min(pt1.X, pt2.X);
            var x2 = Math.Max(pt1.X, pt2.X);
            Assert.IsTrue(pt1.IsValid);
            Assert.IsTrue(pt2.IsValid);
            Assert.AreEqual(-5, x1, 1e-10);
            Assert.AreEqual(0, pt1.Y, 1e-10);
            Assert.AreEqual(5, x2, 1e-10);
            Assert.AreEqual(0, pt2.Y, 1e-10);
        }

        [TestMethod]
        public void Intersection_LineTangentToCircle_OnePoint()
        {
            var curve1 = new Circle(new Point3D(0, 0, 0), 5);
            var curve2 = new Line(new Point3D(-5, 5, 0), new Point3D(5, 5, 0));


            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.IsTrue(pt1.IsValid);
            Assert.AreEqual(0, pt1.X, 1e-10);
            Assert.AreEqual(5, pt1.Y, 1e-10);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_LineMissesCircle_NoPoints()
        {
            var curve1 = new Circle(new Point3D(0, 0, 0), 5);
            var curve2 = new Line(new Point3D(-10, 10, 0), new Point3D(10, 10, 0));

            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_LineIntersectsArc_OnlyWithinArcSweep()
        {
            var curve1 = new Arc(new Point3D(0, 0, 0), 5, 0, Math.PI); // upper half
            var curve2 = new Line(new Point3D(-10, 0, 0), new Point3D(10, 0, 0));


            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            var x1 = Math.Min(pt1.X, pt2.X);
            var x2 = Math.Max(pt1.X, pt2.X);
            Assert.IsTrue(pt1.IsValid);
            Assert.IsTrue(pt2.IsValid);
            Assert.AreEqual(-5, x1, 1e-10);
            Assert.AreEqual(0, pt1.Y, 1e-10);
            Assert.AreEqual(5, x2, 1e-10);
            Assert.AreEqual(0, pt2.Y, 1e-10);
        }

        [TestMethod]
        public void Intersection_LineIntersectsArc_ReturnsAllCircleIntersections()
        {
            var curve1 = new Arc(new Point3D(0, 0, 0), 5, 0, Math.PI); // upper half
            var curve2 = new Line(new Point3D(-10, 0, 0), new Point3D(10, 0, 0));


            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            // Arc is treated as full circle, so both intersection points are returned
            var x1 = Math.Min(pt1.X, pt2.X);
            var x2 = Math.Max(pt1.X, pt2.X);
            Assert.IsTrue(pt1.IsValid);
            Assert.IsTrue(pt2.IsValid);
            Assert.AreEqual(-5, x1, 1e-10);
            Assert.AreEqual(0, pt1.Y, 1e-10);
            Assert.AreEqual(5, x2, 1e-10);
            Assert.AreEqual(0, pt2.Y, 1e-10);

            // Caller can filter using IsPointOnArc if needed
            bool pt1OnArc = GeometricCalculator.IsPointOnArc(pt1, curve1);
            bool pt2OnArc = GeometricCalculator.IsPointOnArc(pt2, curve1);
            Assert.IsTrue(pt1OnArc);
            Assert.IsTrue(pt2OnArc);
        }

        [TestMethod]
        public void IsPointOnArc_PointWithinSweep_ReturnsTrue()
        {
            var curve1 = new Arc(new Point3D(0, 0, 0), 5, 0, Math.PI); // upper half
            var pointOnArc = new Point3D(5, 0, 0); // 0° - on curve1

            bool result = GeometricCalculator.IsPointOnArc(pointOnArc, curve1);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsPointOnArc_PointOutsideSweep_ReturnsFalse()
        {
            var arc = new Arc(new Point3D(0, 0, 0), 5, 0, Math.PI); // upper half (0° to 180°)
            var pointOffArc = new Point3D(0, -5, 0); // 270° - not on arc

            bool result = GeometricCalculator.IsPointOnArc(pointOffArc, arc);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Intersection_CirclesIntersect_TwoPoints()
        {
            var curve1 = new Circle(new Point3D(0, 0, 0), 5);
            var curve2 = new Circle(new Point3D(8, 0, 0), 5);


            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.IsTrue(pt1.IsValid);
            Assert.IsTrue(pt2.IsValid);
            Assert.AreEqual(4, pt1.X, 1e-10);
            Assert.AreEqual(3, Math.Abs(pt1.Y), 1e-10); // Should be ±3
            Assert.AreEqual(4, pt2.X, 1e-10);
            Assert.AreEqual(3, Math.Abs(pt2.Y), 1e-10);
        }

        [TestMethod]
        public void Intersection_CirclesTangent_OnePoint()
        {
            var curve1 = new Circle(new Point3D(0, 0, 0), 5);
            var curve2 = new Circle(new Point3D(10, 0, 0), 5);


            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.IsTrue(pt1.IsValid);
            Assert.AreEqual(5, pt1.X, 1e-10);
            Assert.AreEqual(0, pt1.Y, 1e-10);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_CirclesNoIntersection_TooFarApart()
        {
            var curve1 = new Circle(new Point3D(0, 0, 0), 5);
            var curve2 = new Circle(new Point3D(20, 0, 0), 5);


            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_CirclesNoIntersection_Concentric()
        {
            var curve1 = new Circle(new Point3D(0, 0, 0), 10);
            var curve2 = new Circle(new Point3D(0, 0, 0), 5);


            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_TwoArcs_ReturnsAllCircleIntersections()
        {
            var curve1 = new Arc(new Point3D(0, 0, 0), 5, 0, Math.PI); // upper half
            var curve2 = new Arc(new Point3D(8, 0, 0), 5, Math.PI / 2, 3 * Math.PI / 2); // left half


            var pts = GeometricCalculator.Intersection(curve1, curve2);
            var pt1 = pts.FirstOrDefault();
            var pt2 = pts.Skip(1).FirstOrDefault();

            // Both intersection points returned (treated as full circles)
            Assert.IsTrue(pt1.IsValid);
            Assert.IsTrue(pt2.IsValid);
            Assert.AreEqual(4, pt1.X, 1e-10);
            Assert.AreEqual(4, pt2.X, 1e-10);

            // Caller can filter using IsPointOnArc
            bool pt1OnArc1 = GeometricCalculator.IsPointOnArc(pt1, curve1);
            bool pt1OnArc2 = GeometricCalculator.IsPointOnArc(pt1, curve2);
            bool pt2OnArc1 = GeometricCalculator.IsPointOnArc(pt2, curve1);
            bool pt2OnArc2 = GeometricCalculator.IsPointOnArc(pt2, curve2);

            // Upper point (positive Y) should be on both arcs
            if (pt1.Y > 0)
            {
                Assert.IsTrue(pt1OnArc1 && pt1OnArc2);
                Assert.IsFalse(pt2OnArc1); // Lower point not on arc1's upper half
            }
            else
            {
                Assert.IsTrue(pt2OnArc1 && pt2OnArc2);
                Assert.IsFalse(pt1OnArc1);
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(Math.PI)]
        [DataRow(Math.PI / 2)]
        [DataRow(Math.PI / 3)]
        [DataRow(Math.PI / 4)]
        [DataRow(Math.PI / 6)]
        public void GetBulgeFromThreePoints_Returns_ZeroForCollinearPoints(double angle)
        {
            var p1 = new Point3D(0, 0, 0);
            var p2 = new Point3D(Math.Cos(angle), Math.Sin(angle), 0);
            var p3 = new Point3D(2 * Math.Cos(angle), 2 * Math.Sin(angle), 0);
            double bulge = GeometricCalculator.GetBulgeFromThreePoints(p1, p2, p3);
            Assert.AreEqual(0, bulge, 1e-10);
        }

        [TestMethod]
        [DataRow(0, 0, 0, 1, Math.PI / 2, 1, 1)]
        public void GetBulgeFromThreePoints_Returns_CorrectBulgeForArc(double p1x, double p1y, double a1, double d1, double a2, double d2, double expectedBulge)
        {
            var p1 = new Point3D(p1x, p1y, 0);
            var p2 = new Point3D(p1x + d1 * Math.Cos(a1), p1y + d1 * Math.Sin(a1), 0);
            var p3 = new Point3D(p1x + d2 * Math.Cos(a2), p1y + d2 * Math.Sin(a2), 0);

        }

        [TestMethod]
        [DataRow(0, 0, 1)]                 // Unit circle at origin
        [DataRow(5, -3, 2.5)]              // Arbitrary center, radius 2.5
        [DataRow(-10, 4, 7.75)]            // Larger radius, offset center
        public void TryGetCircleThroughThreePoints_ReturnsCorrectCenterAndRadius(double cx, double cy, double r)
        {
            // Pick three angles that are guaranteed non-collinear
            double a1 = 0;
            double a2 = Math.PI / 3;
            double a3 = Math.PI * 0.85;

            var p1 = new Point3D(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1), 0);
            var p2 = new Point3D(cx + r * Math.Cos(a2), cy + r * Math.Sin(a2), 0);
            var p3 = new Point3D(cx + r * Math.Cos(a3), cy + r * Math.Sin(a3), 0);

            bool ok = GeometricCalculator.TryGetCircleThroughThreePoints(p1, p2, p3,
                                                                         out Point3D center,
                                                                         out double radius);

            Assert.IsTrue(ok, "Expected circle to be solvable.");

            Assert.AreEqual(cx, center.X, 1e-10, "Center X mismatch.");
            Assert.AreEqual(cy, center.Y, 1e-10, "Center Y mismatch.");
            Assert.AreEqual(r, radius, 1e-10, "Radius mismatch.");
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(Math.PI)]
        [DataRow(Math.PI / 2)]
        [DataRow(Math.PI / 3)]
        [DataRow(Math.PI / 4)]
        [DataRow(Math.PI / 6)]
        public void TryGetCircleThroughThreePoints_ReturnsFalseForCollinearPoints(double angle)
        {
            // Three points on the same ray → guaranteed colinear
            var p1 = new Point3D(0, 0, 0);
            var p2 = new Point3D(Math.Cos(angle), Math.Sin(angle), 0);
            var p3 = new Point3D(2 * Math.Cos(angle), 2 * Math.Sin(angle), 0);

            bool ok = GeometricCalculator.TryGetCircleThroughThreePoints(
                p1, p2, p3,
                out Point3D center,
                out double radius);

            Assert.IsFalse(ok, "Colinear points should not define a circle.");

            // Optional: verify outputs are safe defaults
            Assert.IsTrue(double.IsNaN(radius), "Radius should be NaN for invalid circle.");
            Assert.AreEqual(Point3D.NotAPoint.X, center.X, "Center should be NotAPoint for invalid circle.");
            Assert.AreEqual(Point3D.NotAPoint.Y, center.Y, "Center should be NotAPoint for invalid circle.");
            Assert.AreEqual(Point3D.NotAPoint.Z, center.Z, "Center should be NotAPoint for invalid circle.");
        }

        [TestMethod]
        public void GetBulgeFromThreePoints_QuarterCircle_ReturnsCorrectBulge()
        {
            // Unit circle centered at origin
            var start = new Point3D(1, 0, 0);
            var arc = new Point3D(0.70710678, 0.70710678, 0); // 45°
            var end = new Point3D(0, 1, 0);

            double bulge = GeometricCalculator.GetBulgeFromThreePoints(start, arc, end);

            double expected = Math.Tan(Math.PI / 8); // ≈ 0.414213562
            Assert.AreEqual(expected, bulge, 1e-10);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(Math.PI / 2)]
        [DataRow(-Math.PI / 2)]
        [DataRow(Math.PI)]
        [DataRow(-Math.PI + 1e-12)]
        public void NormalizeSigned_ReturnsSameValue_WhenAlreadyInRange(double angle)
        {
            double result = GeometricCalculator.NormalizeSigned(angle);
            Assert.AreEqual(angle, result, 1e-12);
        }

        [TestMethod]
        [DataRow(2 * Math.PI, 0)]
        [DataRow(3 * Math.PI, Math.PI)]
        [DataRow(5 * Math.PI / 2, Math.PI / 2)]
        public void NormalizeSigned_WrapsAnglesGreaterThanPi(double input, double expected)
        {
            double result = GeometricCalculator.NormalizeSigned(input);
            Assert.AreEqual(expected, result, 1e-12);
        }

        [TestMethod]
        [DataRow(-2 * Math.PI, 0)]
        [DataRow(-3 * Math.PI, Math.PI)]
        [DataRow(-5 * Math.PI / 2, -Math.PI / 2)]
        public void NormalizeSigned_WrapsAnglesLessThanMinusPi(double input, double expected)
        {
            double result = GeometricCalculator.NormalizeSigned(input);
            Assert.AreEqual(expected, result, 1e-12);
        }

        [TestMethod]
        [DataRow(20 * Math.PI, 0)]
        [DataRow(21 * Math.PI, Math.PI)]
        [DataRow(-19 * Math.PI, Math.PI)]
        public void NormalizeSigned_HandlesLargeMultiplesOfTwoPi(double input, double expected)
        {
            double result = GeometricCalculator.NormalizeSigned(input);
            Assert.AreEqual(expected, result, 1e-12);
        }



        private static double Deg(double d) => d * Math.PI / 180.0;

        [TestMethod]
        [DataRow(0, 45, 60, 60)]     // minor CCW
        [DataRow(315, 45, 60, 105)]    // reflex CCW (315→45→60)
        [DataRow(0, 60, 45, -315)]    // reflex CW (0→60→45)
        [DataRow(0, 300, 270, -90)]    // minor CW
        [DataRow(0, 60, 120, 120)]    // minor CCW
        [DataRow(0, 150, 210, 210)]    // reflex CCW
        [DataRow(0, 270, 30, -330)]    // reflex CW
        [DataRow(345, 10, 30, 45)]    // crosses 360 seam CCW
        [DataRow(15, 345, 330, -45)]    // crosses 360 seam CW
        [DataRow(-30, -60, -90, -60)]   // all negative, CW
        [DataRow(170, 179, -170, 20)]    // near +π seam CCW
        [DataRow(-170, -179, 170, -20)]    // near -π seam CW
        public void GetIncludedAngle_ReturnsExpected_InDegrees(
            double startDeg, double arcDeg, double endDeg, double expectedDeg)
        {
            double start = Deg(startDeg);
            double arc = Deg(arcDeg);
            double end = Deg(endDeg);
            double expected = Deg(expectedDeg);

            double included = GeometricCalculator.GetIncludedAngle(start, arc, end);

            Assert.AreEqual(expected, included, 1e-12);
        }

        [TestMethod]
        [DataRow(0, 0, 0, 45, 60, 0.2679491924311227)]   // +60° → tan(60/4)
        [DataRow(0, 0, 0, 60, 120, 0.5773502691896257)]   // +120° → tan(120/4)
        [DataRow(0, 0, 0, 270, 30, -7.59575411272515)]   // -330° → tan(-330/4)
        [DataRow(0, 0, 0, 300, 270, -0.4142135623730950)]   // -90° → tan(-90/4)
        [DataRow(0, 0, 345, 10, 30, 0.198912367379658)]   // +45° → tan(45/4)
        [DataRow(0, 0, 15, 345, 330, -0.198912367379658)] // -45° → tan(-45/4)
        [DataRow(0, 0, 170, 179, -170, 0.0874886635259240)]   // +20° → tan(20/4)
        [DataRow(0, 0, -170, -179, 170, -0.0874886635259240)]   // -20° → tan(-20/4)
        public void GetBulgeFromThreePoints_ReturnsExpected(
            double cx, double cy,
            double startDeg, double arcDeg, double endDeg,
            double expectedBulge)
        {
            double radius = 1.0;

            // Convert degrees → radians
            double start = Deg(startDeg);
            double arc = Deg(arcDeg);
            double end = Deg(endDeg);

            // Build points on the circle
            var center = new Point3D(cx, cy, 0);
            var pStart = new Point3D(cx + Math.Cos(start), cy + Math.Sin(start), 0);
            var pArc = new Point3D(cx + Math.Cos(arc), cy + Math.Sin(arc), 0);
            var pEnd = new Point3D(cx + Math.Cos(end), cy + Math.Sin(end), 0);

            double bulge = GeometricCalculator.GetBulgeFromThreePoints(pStart, pArc, pEnd);

            Assert.AreEqual(expectedBulge, bulge, 1e-12);
        }

        [TestMethod]
        public void GetBulgeFromThreePoints_Regression_PolylineArcExample_ReturnsExpectedBulge()
        {
            var startPoint = new Point3D(10, 3, 0);
            var arcPoint = new Point3D(12, 5, 0);
            var endPoint = new Point3D(12.75, 8, 0);

            var result = GeometricCalculator.TryGetCircleThroughThreePoints(startPoint, arcPoint, endPoint, out var center, out var radius);
            var startAngle = center.AngleTo(startPoint);
            var arcAngle = center.AngleTo(arcPoint);
            var endAngle = center.AngleTo(endPoint);
            var includedAngle = GeometricCalculator.GetIncludedAngle(startAngle, arcAngle, endAngle);

            double bulge = GeometricCalculator.GetBulgeFromThreePoints(startPoint, arcPoint, endPoint);

            // Expected included angle ≈ -2.2725565042306255 rad, so bulge = tan(included/4)
            const double expectedBulge = -0.6418596411365724;

            // Primary assertion: numerical value (captures "reversed curve" bug via sign too)
            Assert.AreEqual(expectedBulge, bulge, 1e-12);

            // Extra clarity: if this fails because the arc direction flips, this will point at it directly.
            Assert.IsLessThan(bulge, 0, "Expected negative bulge (clockwise / reversed orientation).");
        }
    }
}
