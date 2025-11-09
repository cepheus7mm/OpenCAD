using System;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD
{
    /// <summary>
    /// Represents a layer in the CAD document.
    /// Layers organize drawable objects and define default visual properties.
    /// </summary>
    public class OpenCADLayer : OpenCADObject
    {
        // Define property indices for the Boolean type (since we have multiple boolean properties)
        private const int VISIBLE_INDEX = 0;
        private const int LOCKED_INDEX = 1;
        
        // Property index for layer name in the String property collection
        private const int LAYER_NAME_INDEX = 0;

        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public OpenCADLayer() : base()
        {
            _isDrawable = false;
        }

        /// <summary>
        /// Creates a new layer with specified properties.
        /// </summary>
        /// <param name="name">The name of the layer. Must be unique within the document.</param>
        /// <param name="color">The default color for objects on this layer.</param>
        /// <param name="lineType">The default line type for objects on this layer.</param>
        /// <param name="lineWeight">The default line weight for objects on this layer.</param>
        public OpenCADLayer(string name, Color color, LineType lineType, LineWeight lineWeight, OpenCADDocument document) 
            : base(document)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Layer name cannot be null or empty.", nameof(name));

            _isDrawable = false;
            
            // Initialize layer name using the base Name property
            Name = name;
            
            // Initialize layer-specific properties
            properties.TryAdd((int)PropertyType.Color, new Property(PropertyType.Color, OpenCADStrings.LayerColor, color));
            properties.TryAdd((int)PropertyType.LineType, new Property(PropertyType.LineType, OpenCADStrings.LayerLineType, lineType));
            properties.TryAdd((int)PropertyType.LineWeight, new Property(PropertyType.LineWeight, OpenCADStrings.LayerLineWeight, lineWeight));
            
            // Store both boolean values in a single Boolean property
            properties.TryAdd((int)PropertyType.Boolean, new Property(PropertyType.Boolean, 
                (OpenCADStrings.LayerIsVisible, true),
                (OpenCADStrings.LayerIsLocked, false)
            ));
            _document = document;
        }

        /// <summary>
        /// Gets or sets the layer name. Must be unique within the document.
        /// This property shadows the base Name property to enforce validation for layers.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public new string Name
        {
            get
            {
                // Use base implementation
                return base.Name ?? string.Empty;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Layer name cannot be null or empty.", nameof(value));
                
                // Use base implementation
                base.Name = value;
            }
        }

        /// <summary>
        /// Gets or sets the default color for objects on this layer.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Color Color
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Color, out var prop))
                    return (Color)prop.GetValue(0);
                return Color.White;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Color,
                    new Property(PropertyType.Color, OpenCADStrings.LayerColor, value),
                    (key, oldValue) => new Property(PropertyType.Color, OpenCADStrings.LayerColor, value)
                );
            }
        }

        /// <summary>
        /// Gets or sets the default line type for objects on this layer.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public OpenCAD.LineType LineType
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.LineType, out var prop))
                    return (OpenCAD.LineType)prop.GetValue(0);
                return OpenCAD.LineType.Continuous;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineType,
                    new Property(PropertyType.LineType, OpenCADStrings.LayerLineType, value),
                    (key, oldValue) => new Property(PropertyType.LineType, OpenCADStrings.LayerLineType, value)
                );
            }
        }

        /// <summary>
        /// Gets or sets the default line weight for objects on this layer.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public OpenCAD.LineWeight LineWeight
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.LineWeight, out var prop))
                    return (OpenCAD.LineWeight)prop.GetValue(0);
                return OpenCAD.LineWeight.Default;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineWeight,
                    new Property(PropertyType.LineWeight, OpenCADStrings.LayerLineWeight, value),
                    (key, oldValue) => new Property(PropertyType.LineWeight, OpenCADStrings.LayerLineWeight, value)
                );
            }
        }

        /// <summary>
        /// Gets or sets whether the layer is visible.
        /// When false, objects on this layer will not be displayed.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsVisible
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                    return (bool)prop.GetValue(VISIBLE_INDEX);
                return true;
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                {
                    prop.SetValue(VISIBLE_INDEX, value);
                }
                else
                {
                    // If property doesn't exist yet, create it with both boolean values
                    properties.TryAdd((int)PropertyType.Boolean, new Property(PropertyType.Boolean, 
                        (OpenCADStrings.LayerIsVisible, value), 
                        (OpenCADStrings.LayerIsLocked, false)));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the layer is locked.
        /// When true, objects on this layer cannot be edited or selected.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsLocked
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                    return (bool)prop.GetValue(LOCKED_INDEX);
                return false;
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                {
                    prop.SetValue(LOCKED_INDEX, value);
                }
                else
                {
                    // If property doesn't exist yet, create it with both boolean values
                    properties.TryAdd((int)PropertyType.Boolean, new Property(PropertyType.Boolean, 
                        (OpenCADStrings.LayerIsVisible, true), 
                        (OpenCADStrings.LayerIsLocked, value)));
                }
            }
        }

        public override string ToString()
        {
            return $"Layer: {Name} (Color: {Color.Name}, LineType: {LineType}, LineWeight: {LineWeight})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is OpenCADLayer other)
                return ID == other.ID;
            return false;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
    }
}