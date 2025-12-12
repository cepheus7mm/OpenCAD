using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    public abstract class GeometryBase : OpenCADObject, IDrawable
    {
        protected Vector3D _normal = new(0, 0, 1);

        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public GeometryBase() : base()
        {
            _isDrawable = true;
        }

        public GeometryBase(OpenCADDocument? doc) : base(doc)
        {
            _isDrawable = true;
            _document = doc;
            _parent = doc;

            // Assign to the document's current layer if document exists
            if (_document != null)
            {
                Layer = _document.CurrentLayer;
                Color = _document.CurrentColor;
                LineType = _document.CurrentLineType ?? LineType.Continuous;
                LineWeight = _document.CurrentLineWeight ?? LineWeight.Default;
            }
        }

        [JsonIgnore]
        public Color Color
        {
            get
            {
                // Try to get the object's own color property
                var color = GetPropertyValue<Color?>(PropertyType.Color, nameof(Color));
                if (color.HasValue && color.Value.A > 0)
                    return color.Value;

                // If not set, try to get the layer's color
                if (Layer != null)
                    return Layer.Color;

                // Fallback to white
                return Color.FromArgb(255, 255, 255, 255);
            }
            set => SetPropertyValue<Color?>(PropertyType.Color, nameof(Color), OpenCADStrings.Color, value);
        }

        [JsonIgnore]
        public LineType LineType
        {
            get
            {
                // Try to get the object's own line type property
                var lineType = GetPropertyValue<LineType?>(PropertyType.LineType, nameof(LineType));
                if (lineType.HasValue && lineType.Value != LineType.ByLayer)
                    return lineType.Value;

                // If not set or ByLayer, try to get the layer's line type
                if (Layer != null)
                    return Layer.LineType;

                if (Document != null)
                {
                    // If the document has a default line type, use it
                    var docDefaultLineType = Document.CurrentLineType;
                    if (docDefaultLineType != null && docDefaultLineType != LineType.ByLayer)
                        return (LineType)docDefaultLineType;
                }

                // Fallback to Continuous
                return LineType.Continuous;
            }
            set => SetPropertyValue<LineType?>(PropertyType.LineType, nameof(LineType), OpenCADStrings.LineType, value);
        }

        [JsonIgnore]
        public double LinetypeScale => Document.GetViewportSettings().LinetypeScale;

        [JsonIgnore]
        public LineWeight LineWeight
        {
            get
            {
                // Try to get the object's own line weight property
                var lineWeight = GetPropertyValue<LineWeight?>(PropertyType.LineWeight, nameof(LineWeight));
                if (lineWeight.HasValue && lineWeight.Value != LineWeight.ByLayer)
                    return lineWeight.Value;

                // If not set or ByLayer, try to get the layer's line weight
                if (Layer != null)
                    return Layer.LineWeight;

                if (Document != null)
                {
                    // If the document has a default line weight, use it
                    var docDefaultLineWeight = Document.CurrentLineWeight;
                    if (docDefaultLineWeight != null && docDefaultLineWeight != LineWeight.ByLayer)
                        return (LineWeight)docDefaultLineWeight;
                }

                // Fallback to Default
                return LineWeight.Default;
            }
            set => SetPropertyValue<LineWeight?>(PropertyType.LineWeight, nameof(LineWeight), OpenCADStrings.LineWeight, value);
        }

        public abstract double Length { get; }

        public abstract double Angle { get; }

        public abstract Extents GetExtents();

        public abstract Vector3D? GetFirstDerivate(Point3D point);

        public abstract Vector3D? GetSecondDerivate(Point3D point);

        public abstract double GetParameterAtPoint(Point3D point);

        public abstract Point3D GetPointAtParameter(double parameter);

        public abstract Point3D GetClosestPointTo(Point3D point, bool extend = false);

        /// <summary>
        /// Retrieves a geographic point of the specified type relative to a given 3D reference point.
        /// </summary>
        /// <param name="referencePoint">The 3D point from which the geographic point is determined.</param>
        /// <param name="geoPointType">The type of geographic point to retrieve relative to the reference point.</param>
        /// <returns>A <see cref="GeoPoint"/> representing the requested geographic point if found; otherwise, <see
        /// langword="null"/>.</returns>
        public abstract IEnumerable<GeoPoint> GetGeoPoints(Point3D referencePoint, GeoPointModes geoPointType);

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

        #region Editing 

        /// <summary>
        /// Apply a 4x4 homogeneous transform to this geometry.
        /// Default throws — override in derived geometry classes.
        /// Return true if transform applied successfully.
        /// </summary>
        public abstract bool Transform(Matrix4D transformation);

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
                .Where(p => p.DistanceTo(referencePoint) <= aperture)
                .OrderBy(p => p.DistanceTo(referencePoint))
                .FirstOrDefault();
        }
        #endregion
    }
}
