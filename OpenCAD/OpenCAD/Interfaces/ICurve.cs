using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;

namespace OpenCAD.Interfaces
{
    public interface ICurve
    {
        // --- Classification ---
        bool IsClosed { get; }
        bool IsPeriodic { get; }

        // --- Parameter domain ---
        double DomainStart { get; }
        double DomainEnd { get; }

        // --- Endpoints (always defined, even for closed curves) ---
        Point3D StartPoint { get; }
        Point3D EndPoint { get; }

        // --- Core evaluation ---
        Point3D GetPointAtParameter(double t);

        // First derivative (tangent)
        Vector3D GetFirstDerivativeAtParameter(double t);

        // Second derivative (curvature vector)
        Vector3D GetSecondDerivativeAtParameter(double t);

        // --- Parameter solving ---
        // Returns parameter t for the point on the curve closest to 'point'
        double GetClosestParameter(Point3D point, bool extend = false);

        // Returns the actual closest point
        Point3D GetClosestPoint(Point3D point, bool extend = false);

        // Returns parameter t for a point that lies exactly on the curve (if possible)
        double GetParameterAtPoint(Point3D point);

        ProjectionResult ProjectPoint(Point3D point);

        // --- Length ---
        double GetLength();
        double GetLength(double t0, double t1);

        // --- Trimming ---
        ICurve Trim(double t0, double t1);

        // Extend to a point using unclamped projection
        ICurve ExtendTo(Point3D point);

        // --- Transformations ---
        ICurve Transform(Matrix4D transform);

        /// <summary>
        /// Returns one or more offset curves at the given distance.
        /// Positive distance = left side of curve direction.
        /// Negative distance = right side.
        /// </summary>
        ICurve[] GetOffsetCurves(double distance);
    }
}
