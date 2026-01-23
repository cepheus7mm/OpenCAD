using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using OpenCAD.SegmentSource;
using OpenCAD.Styles.LineTypes;
using System.Drawing;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    public abstract class GeometryBase : OpenCADObject, IDrawable
    {
        protected const double MIDPOINT_PARAMETER = 0.5;
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
            _parent = doc;

            // Assign to the document's current layer if document exists
            if (_document != null)
            {
                Layer = _document.CurrentLayer;
                Color = _document.CurrentColor;
                LineTypeID = _document.CurrentLineTypeID ?? OpenCADDocument.ContinuousLineTypeID;
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
        public Vector4 ColorVector => new Vector4(Color.R / 255f, Color.G / 255f, Color.B / 255f, Color.A / 255f);

        [JsonIgnore]
        public uint LineTypeID
        {
            get
            {
                // Try to get the object's own line type property
                var lineType = GetPropertyValue<uint?>(PropertyType.UInt, nameof(LineTypeID));
                if (lineType.HasValue && lineType.Value != uint.MaxValue)
                    return lineType.Value;

                // If not set or ByLayer, try to get the layer's line type
                if (Layer != null)
                    return Layer.LineTypeID;

                if (Document != null)
                {
                    // If the document has a default line type, use it
                    var docDefaultLineType = Document.CurrentLineTypeID;
                    if (docDefaultLineType.HasValue && docDefaultLineType != OpenCADDocument.LineTypeByLayer)
                        return docDefaultLineType.Value;
                }

                // Fallback to Continuous
                return OpenCADDocument.ContinuousLineTypeID;
            }
            set => SetPropertyValue<uint?>(PropertyType.UInt, nameof(LineTypeID), OpenCADStrings.LineType, value);
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

        [JsonIgnore, XmlIgnore]
        public bool IsPreviewGeometry { get; set; } = false;

        public abstract double Length { get; }

        public abstract double Angle { get; }

        public abstract Extents GetExtents();

        /// <summary>
        /// Retrieves a geographic point of the specified type relative to a given 3D reference point.
        /// </summary>
        /// <param name="referencePoint">The 3D point from which the geographic point is determined.</param>
        /// <param name="geoPointType">The type of geographic point to retrieve relative to the reference point.</param>
        /// <returns>A <see cref="GeoPoint"/> representing the requested geographic point if found; otherwise, <see
        /// langword="null"/>.</returns>
        //public abstract IEnumerable<GeoPoint> GetGeoPoints(Point3D referencePoint, GeoPointModes geoPointType);

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

        public virtual void SetBasicPropertiesFrom(GeometryBase sourceGeometry)
        {
            if (sourceGeometry == null)
                return;
            // Copy basic properties
            this.Layer = sourceGeometry.Layer;
            this.Color = sourceGeometry.Color;
            this.LineTypeID = sourceGeometry.LineTypeID;
            this.LineWeight = sourceGeometry.LineWeight;
        }

        public unsafe virtual LinetypeGpuData GetLinetypeGpuData()
        {
            var continuousLineType = new LinetypeGpuData();
            continuousLineType.Pattern[0] = 1.0f;
            continuousLineType.PatternCount = 1;
            continuousLineType.PatternLength = 1.0f;

            var lineTypes = Document?.GetLineTypesContainer();
            return lineTypes?.GetScaledLineType(LineTypeID, (float)LinetypeScale) ?? continuousLineType;
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
