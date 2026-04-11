using OpenCAD.Styles.LineTypes;
using System;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Styles
{
    /// <summary>
    /// Represents a layer in the CAD document.
    /// Layers organize drawable objects and define default visual properties.
    /// </summary>
    public class OpenCADLayer : OpenCADObject
    {
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
        /// <param name="lineTypeID">The default line type for objects on this layer.</param>
        /// <param name="lineWeight">The default line weight for objects on this layer.</param>
        public OpenCADLayer(string name, Color color, uint lineTypeID, LineWeight lineWeight, OpenCADDocument document) 
            : base(document)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Layer name cannot be null or empty.", nameof(name));

            _isDrawable = false;
            
            // Initialize layer name using the base Name property
            Name = name;
            Color = color;
            LineTypeID = lineTypeID;
            LineWeight = lineWeight;
            IsLocked = false;
            IsVisible = true;
        }

        /// <summary>
        /// Gets or sets the default color for objects on this layer.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Color Color
        {
            get => GetPropertyValue<Color>(PropertyType.Color, nameof(Color));
            set => SetPropertyValue(PropertyType.Color, nameof(Color), OpenCADStrings.LayerColor, value);
        }

        /// <summary>
        /// Gets or sets the default line type ID for objects on this layer.
        /// This is the uint rendering ID, not the Guid.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public uint LineTypeID
        {
            get => GetPropertyValue<uint>(PropertyType.UInt, nameof(LineTypeID));
            set => SetPropertyValue(PropertyType.UInt, nameof(LineTypeID), OpenCADStrings.LineTypeID, value);
        }

        /// <summary>
        /// Gets or sets the default line type for objects on this layer.
        /// This is a convenience property that resolves the line type from the document.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public OpenCADLineType? LineType
        {
            get
            {
                if (_document == null)
                    return null;

                // Get line type from document's line types container by rendering ID
                return _document.GetLineTypes().FirstOrDefault(x => x.LineTypeID == LineTypeID);
            }
            set => LineTypeID = value?.LineTypeID ?? 0;
        }

        /// <summary>
        /// Gets or sets the default line weight for objects on this layer.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public LineWeight LineWeight
        {
            get => GetPropertyValue<LineWeight>(PropertyType.LineWeight, nameof(LineWeight));
            set => SetPropertyValue(PropertyType.LineWeight, nameof(LineWeight), OpenCADStrings.LayerLineWeight, value);    
        }

        /// <summary>
        /// Gets or sets whether the layer is visible.
        /// When false, objects on this layer will not be displayed.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsVisible
        {
            get => GetPropertyValue<bool>(PropertyType.Boolean, nameof(IsVisible));
            set => SetPropertyValue(PropertyType.Boolean, nameof(IsVisible), OpenCADStrings.LayerIsVisible, value);
        }

        /// <summary>
        /// Gets or sets whether the layer is locked.
        /// When true, objects on this layer cannot be edited or selected.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsLocked
        {
            get => GetPropertyValue<bool>(PropertyType.Boolean, nameof(IsLocked));
            set => SetPropertyValue(PropertyType.Boolean, nameof(IsLocked), OpenCADStrings.LayerIsLocked, value);
        }

        public override string ToString()
        {
            return $"Layer: {Name} (Color: {Color.Name}, LineType: {LineType?.Name ?? "None"}, LineWeight: {LineWeight})";
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