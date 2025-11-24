using OpenCAD;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Settings
{
    public class SnapSettings : ObservableSettings
    {
        public SnapSettings() : this(null!)
        {
        }

        public SnapSettings(OpenCADDocument document)
        {
            SnapEnabled = true;
            SnapSpacing = 0.25;
        }

        [JsonIgnore, XmlIgnore]
        public bool SnapEnabled
        {
            get => GetPropertyValue<bool>(PropertyType.Boolean, nameof(SnapEnabled));
            set
            {
                SetPropertyValue(PropertyType.Boolean, nameof(SnapEnabled), OpenCADStrings.SnapEnabled, value);
                OnPropertyChanged();
            }
        }

        [JsonIgnore, XmlIgnore]
        public double SnapSpacing
        {
            get => GetPropertyValue<double>(PropertyType.DoubleLength, nameof(SnapSpacing));
            set
            {
                SetPropertyValue(PropertyType.DoubleLength, nameof(SnapSpacing), OpenCADStrings.SnapSpacing, value);
                OnPropertyChanged();
            }
        }
    }
}