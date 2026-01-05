using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;

namespace OpenCADTests
{
    [TestClass]
    public class GeometricCalculatorTests
    {
        [TestMethod]
        public void Intersection_IntersectingLines_ReturnsIntersectionPoint()
        {
            var line1 = new Line(null, new Point3D(0, 0, 0), new Point3D(10, 0, 0));
            var line2 = new Line(null, new Point3D(5, -5, 0), new Point3D(5, 5, 0));

            var (pt1, pt2) = GeometricCalculator.Intersection(line1, line2);

            Assert.IsTrue(pt1.IsValid);
            Assert.AreEqual(5, pt1.X, 1e-10);
            Assert.AreEqual(0, pt1.Y, 1e-10);
            Assert.AreEqual(0, pt1.Z, 1e-10);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_ParallelLines_ReturnsNotAPoint()
        {
            var line1 = new Line(null, new Point3D(0, 0, 0), new Point3D(10, 0, 0));
            var line2 = new Line(null, new Point3D(0, 1, 0), new Point3D(10, 1, 0));

            var (pt1, pt2) = GeometricCalculator.Intersection(line1, line2);

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_CoincidentLines_ReturnsNotAPoint()
        {
            var line1 = new Line(null, new Point3D(0, 0, 0), new Point3D(10, 0, 0));
            var line2 = new Line(null, new Point3D(0, 0, 0), new Point3D(10, 0, 0));

            var (pt1, pt2) = GeometricCalculator.Intersection(line1, line2);

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_SkewLines_ReturnsIntersectionInXY()
        {
            var line1 = new Line(null, new Point3D(0, 0, 1), new Point3D(10, 0, 1));
            var line2 = new Line(null, new Point3D(5, -5, 2), new Point3D(5, 5, 2));

            var (pt1, pt2) = GeometricCalculator.Intersection(line1, line2);

            Assert.IsTrue(pt1.IsValid);
            Assert.AreEqual(5, pt1.X, 1e-10);
            Assert.AreEqual(0, pt1.Y, 1e-10);
            Assert.AreEqual(1, pt1.Z, 1e-10);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_LineIntersectsCircle_TwoPoints()
        {
            var circle = new Circle(new Point3D(0, 0, 0), 5);
            var line = new Line(null, new Point3D(-10, 0, 0), new Point3D(10, 0, 0));

            var (pt1, pt2) = GeometricCalculator.Intersection(line, circle);
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
            var circle = new Circle(new Point3D(0, 0, 0), 5);
            var line = new Line(null, new Point3D(-5, 5, 0), new Point3D(5, 5, 0));

            var (pt1, pt2) = GeometricCalculator.Intersection(line, circle);

            Assert.IsTrue(pt1.IsValid);
            Assert.AreEqual(0, pt1.X, 1e-10);
            Assert.AreEqual(5, pt1.Y, 1e-10);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_LineMissesCircle_NoPoints()
        {
            var circle = new Circle(new Point3D(0, 0, 0), 5);
            var line = new Line(null, new Point3D(-10, 10, 0), new Point3D(10, 10, 0));

            var (pt1, pt2) = GeometricCalculator.Intersection(line, circle);

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_LineIntersectsArc_OnlyWithinArcSweep()
        {
            var arc = new Arc(new Point3D(0, 0, 0), 5, 0, Math.PI); // upper half
            var line = new Line(null, new Point3D(-10, 0, 0), new Point3D(10, 0, 0));

            var (pt1, pt2) = GeometricCalculator.Intersection(line, arc);

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
            var arc = new Arc(new Point3D(0, 0, 0), 5, 0, Math.PI); // upper half
            var line = new Line(null, new Point3D(-10, 0, 0), new Point3D(10, 0, 0));

            var (pt1, pt2) = GeometricCalculator.Intersection(line, arc);

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
            bool pt1OnArc = GeometricCalculator.IsPointOnArc(pt1, arc);
            bool pt2OnArc = GeometricCalculator.IsPointOnArc(pt2, arc);
            Assert.IsTrue(pt1OnArc);
            Assert.IsTrue(pt2OnArc);
        }

        [TestMethod]
        public void IsPointOnArc_PointWithinSweep_ReturnsTrue()
        {
            var arc = new Arc(new Point3D(0, 0, 0), 5, 0, Math.PI); // upper half
            var pointOnArc = new Point3D(5, 0, 0); // 0° - on arc

            bool result = GeometricCalculator.IsPointOnArc(pointOnArc, arc);

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
            var circle1 = new Circle(new Point3D(0, 0, 0), 5);
            var circle2 = new Circle(new Point3D(8, 0, 0), 5);

            var (pt1, pt2) = GeometricCalculator.Intersection(circle1, circle2);

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
            var circle1 = new Circle(new Point3D(0, 0, 0), 5);
            var circle2 = new Circle(new Point3D(10, 0, 0), 5);

            var (pt1, pt2) = GeometricCalculator.Intersection(circle1, circle2);

            Assert.IsTrue(pt1.IsValid);
            Assert.AreEqual(5, pt1.X, 1e-10);
            Assert.AreEqual(0, pt1.Y, 1e-10);
           Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_CirclesNoIntersection_TooFarApart()
        {
            var circle1 = new Circle(new Point3D(0, 0, 0), 5);
            var circle2 = new Circle(new Point3D(20, 0, 0), 5);

            var (pt1, pt2) = GeometricCalculator.Intersection(circle1, circle2);

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_CirclesNoIntersection_Concentric()
        {
            var circle1 = new Circle(new Point3D(0, 0, 0), 10);
            var circle2 = new Circle(new Point3D(0, 0, 0), 5);

            var (pt1, pt2) = GeometricCalculator.Intersection(circle1, circle2);

            Assert.AreEqual(pt1, Point3D.NotAPoint);
            Assert.AreEqual(pt2, Point3D.NotAPoint);
        }

        [TestMethod]
        public void Intersection_TwoArcs_ReturnsAllCircleIntersections()
        {
            var arc1 = new Arc(new Point3D(0, 0, 0), 5, 0, Math.PI); // upper half
            var arc2 = new Arc(new Point3D(8, 0, 0), 5, Math.PI / 2, 3 * Math.PI / 2); // left half

            var (pt1, pt2) = GeometricCalculator.Intersection(arc1, arc2);

            // Both intersection points returned (treated as full circles)
            Assert.IsTrue(pt1.IsValid);
            Assert.IsTrue(pt2.IsValid);
            Assert.AreEqual(4, pt1.X, 1e-10);
            Assert.AreEqual(4, pt2.X, 1e-10);

            // Caller can filter using IsPointOnArc
            bool pt1OnArc1 = GeometricCalculator.IsPointOnArc(pt1, arc1);
            bool pt1OnArc2 = GeometricCalculator.IsPointOnArc(pt1, arc2);
            bool pt2OnArc1 = GeometricCalculator.IsPointOnArc(pt2, arc1);
            bool pt2OnArc2 = GeometricCalculator.IsPointOnArc(pt2, arc2);

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
    }
}
