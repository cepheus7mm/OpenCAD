using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using OpenCAD;

namespace OpenCAD.Settings // CHANGED
{
    /// <summary>
    /// Settings for the crosshair cursor display.
    /// </summary>
    public class CrosshairSettings : OpenCADObject
    {
        public CrosshairSettings() : this(null!)
        {
        }

        public CrosshairSettings(OpenCADDocument document)
        {
            // Initialize default crosshair values using properties
            Color = Color.LightBlue;
            LineType = LineType.Continuous;
            LineWeight = LineWeight.Hairline;
            PickboxSize = 5;
            _document = document;
        }

        /// <summary>
        /// Gets or sets the color of the crosshair cursor.
        /// Default: LightBlue
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Color Color
        {
            get => GetPropertyValue<Color>(PropertyType.Color, nameof(Color));
            set => SetPropertyValue(PropertyType.Color, nameof(Color), OpenCADStrings.Color, value);
        }

        /// <summary>
        /// Gets or sets the line type of the crosshair cursor.
        /// Default: Continuous
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public LineType LineType
        {
            get => GetPropertyValue<LineType>(PropertyType.LineType, nameof(LineType));
            set => SetPropertyValue(PropertyType.LineType, nameof(LineType), OpenCADStrings.LineType, value);
        }

        /// <summary>
        /// Gets or sets the line weight of the crosshair cursor.
        /// Default: Hairline (thinnest)
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public LineWeight LineWeight
        {
            get => GetPropertyValue<LineWeight>(PropertyType.LineWeight, nameof(LineWeight));
            set => SetPropertyValue(PropertyType.LineWeight, nameof(LineWeight), OpenCADStrings.LineWeight, value);
        }

        /// <summary>
        /// Gets or sets the pickbox size in pixels.
        /// The pickbox is the small square at the crosshair center used for object selection.
        /// Default: 5 pixels
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double PickboxSize
        {
            get => GetPropertyValue<double>(PropertyType.Double, nameof(PickboxSize));
            set => SetPropertyValue(PropertyType.Double, nameof(PickboxSize), OpenCADStrings.PickboxSize, value);
        }
    }
}
