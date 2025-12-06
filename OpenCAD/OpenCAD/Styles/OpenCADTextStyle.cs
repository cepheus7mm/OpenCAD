using System;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD
{
    /// <summary>
    /// Represents a text stle in the CAD document.
    /// </summary>
    public class OpenCADTextStyle : OpenCADObject
    {
        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public OpenCADTextStyle() : base()
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
        public OpenCADTextStyle(string name, string fontFamily, double fontSize, OpenCADDocument document) 
            : base(document)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Layer name cannot be null or empty.", nameof(name));

            _isDrawable = false;
            
            // Initialize style
            Name = name;
            FontFamily = fontFamily;
            FontSize = fontSize;
            IsBold = false;
            IsUnderlined = false;
            IsItalic = false;
            _document = document;
        }

        /// <summary>
        /// Gets or sets the font family.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public string FontFamily
        {
            get => GetPropertyValue<string>(PropertyType.String, nameof(FontFamily)) ?? string.Empty;
            set => SetPropertyValue(PropertyType.String, nameof(FontFamily), OpenCADStrings.FontFamily, value);
        }

        /// <summary>
        /// Gets or sets the font size.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double FontSize
        {
            get => GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(FontSize));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(FontSize), OpenCADStrings.FontSize, value);
        }

        /// <summary>
        /// Gets or sets whether the font is underlined.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsUnderlined
        {
            get => GetPropertyValue<bool>(PropertyType.Boolean, nameof(IsUnderlined));
            set => SetPropertyValue(PropertyType.Boolean, nameof(IsUnderlined), OpenCADStrings.Underline, value);
        }

        /// <summary>
        /// Gets or sets whether the font is bold.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsBold
        {
            get => GetPropertyValue<bool>(PropertyType.Boolean, nameof(IsBold));
            set => SetPropertyValue(PropertyType.Boolean, nameof(IsBold), OpenCADStrings.Bold, value);
        }

        /// <summary>
        /// Gets or sets whether the font is italicized.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsItalic
        {
            get => GetPropertyValue<bool>(PropertyType.Boolean, nameof(IsItalic));
            set => SetPropertyValue(PropertyType.Boolean, nameof(IsItalic), OpenCADStrings.Italic, value);
        }

        public override string ToString()
        {
            return $"Text Style: {Name} (Font Family: {FontFamily}, Font Size: {FontSize})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is OpenCADTextStyle other)
                return ID == other.ID;
            return false;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
    }
}