using OpenCAD.Interfaces;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    public class GeometryBase : OpenCADObject, IDrawable
    {
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

            // Assign to the document's current layer if document exists
            if (_document != null)
            {
                var currentLayer = _document.CurrentLayer;
                if (currentLayer != null)
                {
                    properties.TryAdd((int)PropertyType.Layer, new Property(PropertyType.Layer, OpenCADStrings.Layer, currentLayer.ID));
                }
            }
        }

        [JsonIgnore, XmlIgnore]
        public OpenCADLayer? Layer
        {
            get
            {
                // Return null if no layer is assigned (for temporary objects like crosshairs)
                if (!properties.TryGetValue((int)PropertyType.Layer, out var prop))
                    return null;

                // Return null if no document context
                if (_document == null)
                    return null;

                return _document.GetLayer((Guid)prop.GetValue(0));
            }
            set
            {
                if (value == null)
                {
                    // Remove layer assignment
                    properties.TryRemove((int)PropertyType.Layer, out _);
                }
                else
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Layer,
                        new Property(PropertyType.Layer, OpenCADStrings.Layer, value.ID),
                        (key, oldValue) => new Property(PropertyType.Layer, OpenCADStrings.Layer, value.ID)
                    );
                }
            }
        }

        [JsonIgnore]
        public Color Color
        {
            get
            {
                // First check if object has a color override
                if (properties.TryGetValue((int)PropertyType.Color, out var colorProp))
                {
                    return (Color)colorProp.GetValue(0);
                }

                // If not, get the color from the layer (if available)
                var layer = Layer;
                if (layer != null)
                    return layer.Color;

                // Default fallback for objects without layer context (like crosshair)
                return Color.White;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Color,
                    new Property(PropertyType.Color, OpenCADStrings.Color, value),
                    (key, oldValue) => new Property(PropertyType.Color, OpenCADStrings.Color, value)
                );
            }
        }

        [JsonIgnore]
        public LineType LineType
        {
            get
            {
                // First check if object has a line type override
                if (properties.TryGetValue((int)PropertyType.LineType, out var lineTypeProp))
                {
                    var lineType = (LineType)lineTypeProp.GetValue(0);

                    // If explicitly set to ByLayer, use layer's line type
                    if (lineType != LineType.ByLayer)
                        return lineType;
                }

                // If not, get the line type from the layer (if available)
                var layer = Layer;
                if (layer != null)
                    return layer.LineType;

                return LineType.Continuous; // Default for objects without layer
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineType,
                    new Property(PropertyType.LineType, OpenCADStrings.LineType, value),
                    (key, oldValue) => new Property(PropertyType.LineType, OpenCADStrings.LineType, value)
                );
            }
        }

        [JsonIgnore]
        public LineWeight LineWeight
        {
            get
            {
                // First check if object has a line weight override
                if (properties.TryGetValue((int)PropertyType.LineWeight, out var lineWeightProp))
                {
                    var lineWeight = (LineWeight)lineWeightProp.GetValue(0);

                    // If explicitly set to ByLayer, use layer's line weight
                    if (lineWeight != LineWeight.ByLayer)
                        return lineWeight;
                }

                // If not, get the line weight from the layer (if available)
                var layer = Layer;
                if (layer != null)
                    return layer.LineWeight;

                return LineWeight.Default; // Default for objects without layer
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineWeight,
                    new Property(PropertyType.LineWeight, OpenCADStrings.LineWeight, value),
                    (key, oldValue) => new Property(PropertyType.LineWeight, OpenCADStrings.LineWeight, value)
                );
            }
        }
    }
}