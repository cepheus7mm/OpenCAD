using OpenCAD;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Settings
{
    /// <summary>
    /// Contains user-definable settings for viewport display elements.
    /// Settings are organized into groups (crosshair, grid, snap, etc.) stored as child objects.
    /// </summary>
    public class ViewportSettings : ObservableSettings
    {
        public ViewportSettings() : this(null!)
        {
            
        }

        public ViewportSettings(OpenCADDocument document)
        {
            LinetypeScale = 1.0;

            // Create and add the crosshair settings group
            var crosshairSettings = new CrosshairSettings(document);
            Add(crosshairSettings);

            // Create and add the grid settings group
            var gridSettings = new GridSettings(document);
            Add(gridSettings);

            // Create and add the snap settings group
            var snapSettings = new SnapSettings(document);
            Add(snapSettings);

            // Create and add the unit settings group
            var unitSettings = new UnitSettings(document);
            Add(unitSettings);
            _document = document;
        }

        /// <summary>
        /// Gets or sets whether the grid is visible.
        /// Default: true
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double LinetypeScale
        {
            get => GetPropertyValue<double>(PropertyType.DoubleLength, nameof(LinetypeScale));
            set
            {
                if (value > 0)
                {
                    SetPropertyValue(PropertyType.DoubleLength, nameof(LinetypeScale), OpenCADStrings.LinetypeScale, value);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the crosshair display settings.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public CrosshairSettings? Crosshair
        {
            get => GetChildren().OfType<CrosshairSettings>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the grid display settings.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public GridSettings? Grid
        {
            get => GetChildren().OfType<GridSettings>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the snap settings.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public SnapSettings? Snap
        {
            get => GetChildren().OfType<SnapSettings>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the unit settings.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public UnitSettings? Unit
        {
            get => GetChildren().OfType<UnitSettings>().FirstOrDefault();
        }
    }
}