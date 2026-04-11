using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using OpenCAD.SegmentSource;
using OpenCAD.Styles;
using OpenCAD.Styles.LineTypes;
using System.Drawing;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD
{
    /// <summary>
    /// Base class for any object that can be drawn with visual properties (color, line type, line weight).
    /// Sits between <see cref="OpenCADObject"/> and the more specialized <see cref="GeometryBase"/>.
    /// </summary>
    public abstract class DrawableBase : OpenCADObject, IDrawable
    {
        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public DrawableBase() : base()
        {
            _isDrawable = true;
        }

        public DrawableBase(OpenCADDocument? doc) : base(doc)
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

        public virtual void SetBasicPropertiesFrom(DrawableBase sourceDrawable)
        {
            if (sourceDrawable == null)
                return;
            // Copy basic properties
            this.Layer = sourceDrawable.Layer;
            this.Color = sourceDrawable.Color;
            this.LineTypeID = sourceDrawable.LineTypeID;
            this.LineWeight = sourceDrawable.LineWeight;
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
    }
}