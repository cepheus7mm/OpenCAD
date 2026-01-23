using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using OpenCAD.Styles.LineTypes;

namespace OpenCAD.Settings // CHANGED
{
    /// <summary>
    /// Settings for the crosshair cursor display.
    /// </summary>
    public class CrosshairSettings : OpenCADObject
    {
        private const uint DefaultPickboxSize = 5;

        public CrosshairSettings() : this(null!)
        {
        }

        public CrosshairSettings(OpenCADDocument document)
        {
            // Initialize default crosshair values using properties
            Color = Color.LightBlue;
            LineTypeID = OpenCADDocument.ContinuousLineTypeID;
            LineWeight = LineWeight.Hairline;
            PickboxSize = DefaultPickboxSize;
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
        public uint LineTypeID
        {
            get => GetPropertyValue<uint>(PropertyType.UInt, nameof(LineTypeID));
            set => SetPropertyValue(PropertyType.UInt, nameof(LineTypeID), OpenCADStrings.LineType, value);
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
            get => GetPropertyValue<double>(PropertyType.DoubleLength, nameof(PickboxSize));
            set => SetPropertyValue(PropertyType.DoubleLength, nameof(PickboxSize), OpenCADStrings.PickboxSize, value);
        }
    }
}
