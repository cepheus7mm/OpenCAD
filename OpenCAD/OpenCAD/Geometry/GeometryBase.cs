using OpenCAD.Geometry.Helpers;
using OpenCAD.Geometry.Helpers.GeoPoints;
using OpenCAD.Interfaces;

namespace OpenCAD.Geometry
{
    public abstract class GeometryBase : DrawableBase
    {
        protected const double MIDPOINT_PARAMETER = 0.5;
        protected Vector3D _normal = new(0, 0, 1);

        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public GeometryBase() : base()
        {
        }

        public GeometryBase(OpenCADDocument? doc) : base(doc)
        {
        }

        public abstract double Length { get; }

        public abstract double Angle { get; }

        public abstract Extents GetExtents();

        public bool ToStringLength(out string length)
        {
            if (Document is null || double.IsNaN(Length) || double.IsInfinity(Length))
            {
                length = OpenCADStrings.UndefinedValue;
                return false;
            }
            length = Document.ValueToString(Length, OpenCADDocument.UnitFormatType.Linear);
            return true;
        }

        public bool ToStringAngle(out string length)
        {
            if (Document is null || double.IsNaN(Angle) || double.IsInfinity(Angle))
            {
                length = OpenCADStrings.UndefinedValue;
                return false;
            }
            length = Document.ValueToString(Angle, OpenCADDocument.UnitFormatType.Angular);
            return true;
        }

        internal void SetNormal(Vector3D normal)
        {
            _normal = normal.Length > 1e-12 ? normal.Normalized : normal;
        }

        #region Editing

        #endregion

        #region

        // support methods

        /// <summary>
        /// Finds the candidate geographic point that is closest to the specified reference point, optionally within a
        /// given maximum distance.
        /// </summary>
        /// <remarks>If multiple candidates are equally close to the reference point, the first one
        /// encountered is returned. If no candidates are within the specified aperture, the method returns
        /// null.</remarks>
        /// <param name="referencePoint">The reference point to which distances are measured. Cannot be null.</param>
        /// <param name="candidates">A collection of candidate geographic points to search. Must not be empty.</param>
        /// <param name="aperture">The maximum allowed distance from the reference point, in the same units as the points' coordinates. Only
        /// candidates within this distance are considered.</param>
        /// <returns>The candidate geographic point closest to the reference point and within the specified aperture, or null if
        /// no such candidate exists.</returns>
        internal GeoPoint? ClosestTo(Point3D referencePoint, IEnumerable<GeoPoint> candidates, double aperture)
        {
            if (!candidates.Any() || referencePoint == null)
                return null;

            return candidates
                .Where(p => p.Position.DistanceTo(referencePoint) <= aperture)
                .OrderBy(p => p.Position.DistanceTo(referencePoint))
                .FirstOrDefault();
        }

        #endregion
    }
}
