using OpenCAD;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Settings // CHANGED
{
    /// <summary>
    /// Settings for grid display.
    /// </summary>
    public class GridSettings : ObservableSettings
    {
        public GridSettings() : this(null!)
        {
        }

        public GridSettings(OpenCADDocument document)
        {
            // Initialize default grid values using properties
            Color = Color.Gray;
            ShowGrid = true;
            MajorSpacing = 10.0;
            MinorSpacing = 1.0;
            
            _document = document;
        }

        /// <summary>
        /// Gets or sets the color of the grid lines.
        /// Default: Gray
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Color Color
        {
            get => GetPropertyValue<Color>(PropertyType.Color, nameof(Color));
            set
            {
                SetPropertyValue(PropertyType.Color, nameof(Color), OpenCADStrings.GridColor, value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether the grid is visible.
        /// Default: true
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool ShowGrid
        {
            get => GetPropertyValue<bool>(PropertyType.Boolean, nameof(ShowGrid));
            set
            {
                SetPropertyValue(PropertyType.Boolean, nameof(ShowGrid), OpenCADStrings.ShowGrid, value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the spacing between major grid lines.
        /// Default: 10.0 units
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double MajorSpacing
        {
            get => GetPropertyValue<double>(PropertyType.Double, nameof(MajorSpacing));
            set
            {
                SetPropertyValue(PropertyType.Double, nameof(MajorSpacing), OpenCADStrings.MajorSpacing, value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the spacing between minor grid lines.
        /// Default: 1.0 units
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double MinorSpacing
        {
            get => GetPropertyValue<double>(PropertyType.Double, nameof(MinorSpacing));
            set
            {
                SetPropertyValue(PropertyType.Double, nameof(MinorSpacing), OpenCADStrings.MinorSpacing, value);
                OnPropertyChanged();
            }
        }
    }
}