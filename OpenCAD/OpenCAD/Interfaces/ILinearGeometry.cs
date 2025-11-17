using OpenCAD.Geometry;

namespace OpenCAD.Interfaces
{
    /// <summary>
    /// Represents geometry that has a linear structure with start and end points.
    /// </summary>
    public interface ILinearGeometry : IDrawable
    {
        /// <summary>
        /// Gets the starting point of the linear geometry.
        /// </summary>
        Point3D StartPoint { get; }

        /// <summary>
        /// Gets the ending point of the linear geometry.
        /// </summary>
        Point3D EndPoint { get; }
    }
}