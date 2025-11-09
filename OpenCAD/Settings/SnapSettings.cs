using OpenCAD;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Settings // CHANGED
{
    public class SnapSettings : OpenCADObject
    {
        private const int ENABLED_INDEX = 0;
        private const int GRIDSNAP_INDEX = 1;
        private const int OBJECTSNAP_INDEX = 2;
        private const int SPACING_INDEX = 0;
        private const int APERTURE_INDEX = 0;

        public SnapSettings() : this(null!)
        {
        }

        public SnapSettings(OpenCADDocument document)
        {
            properties.TryAdd((int)PropertyType.Boolean,
                new Property(PropertyType.Boolean,
                    ("Snap Enabled", true),
                    ("Grid Snap", true),
                    ("Object Snap", true)));

            properties.TryAdd((int)PropertyType.Double,
                new Property(PropertyType.Double, "Snap Spacing", 0.25));

            properties.TryAdd((int)PropertyType.Size,
                new Property(PropertyType.Size, "Aperture Size", 10.0));
            _document = document;
        }

        public bool SnapEnabled
        {
            get => properties.TryGetValue((int)PropertyType.Boolean, out var prop) ? (bool)prop.GetValue(ENABLED_INDEX) : true;
            set
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                {
                    var gridSnap = (bool)prop.GetValue(GRIDSNAP_INDEX);
                    var objectSnap = (bool)prop.GetValue(OBJECTSNAP_INDEX);
                    properties.AddOrUpdate(
                        (int)PropertyType.Boolean,
                        new Property(PropertyType.Boolean,
                            ("Snap Enabled", value),
                            ("Grid Snap", gridSnap),
                            ("Object Snap", objectSnap)),
                        (_, _) => new Property(PropertyType.Boolean,
                            ("Snap Enabled", value),
                            ("Grid Snap", gridSnap),
                            ("Object Snap", objectSnap)));
                }
                else
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Boolean,
                        new Property(PropertyType.Boolean,
                            ("Snap Enabled", value),
                            ("Grid Snap", true),
                            ("Object Snap", true)),
                        (_, _) => new Property(PropertyType.Boolean,
                            ("Snap Enabled", value),
                            ("Grid Snap", true),
                            ("Object Snap", true)));
                }
            }
        }

        public bool GridSnapEnabled
        {
            get => properties.TryGetValue((int)PropertyType.Boolean, out var prop) ? (bool)prop.GetValue(GRIDSNAP_INDEX) : true;
            set
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                {
                    var enabled = (bool)prop.GetValue(ENABLED_INDEX);
                    var objectSnap = (bool)prop.GetValue(OBJECTSNAP_INDEX);
                    properties.AddOrUpdate(
                        (int)PropertyType.Boolean,
                        new Property(PropertyType.Boolean,
                            ("Snap Enabled", enabled),
                            ("Grid Snap", value),
                            ("Object Snap", objectSnap)),
                        (_, _) => new Property(PropertyType.Boolean,
                            ("Snap Enabled", enabled),
                            ("Grid Snap", value),
                            ("Object Snap", objectSnap)));
                }
                else
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Boolean,
                        new Property(PropertyType.Boolean,
                            ("Snap Enabled", true),
                            ("Grid Snap", value),
                            ("Object Snap", true)),
                        (_, _) => new Property(PropertyType.Boolean,
                            ("Snap Enabled", true),
                            ("Grid Snap", value),
                            ("Object Snap", true)));
                }
            }
        }

        public bool ObjectSnapEnabled
        {
            get => properties.TryGetValue((int)PropertyType.Boolean, out var prop) ? (bool)prop.GetValue(OBJECTSNAP_INDEX) : true;
            set
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                {
                    var enabled = (bool)prop.GetValue(ENABLED_INDEX);
                    var gridSnap = (bool)prop.GetValue(GRIDSNAP_INDEX);
                    properties.AddOrUpdate(
                        (int)PropertyType.Boolean,
                        new Property(PropertyType.Boolean,
                            ("Snap Enabled", enabled),
                            ("Grid Snap", gridSnap),
                            ("Object Snap", value)),
                        (_, _) => new Property(PropertyType.Boolean,
                            ("Snap Enabled", enabled),
                            ("Grid Snap", gridSnap),
                            ("Object Snap", value)));
                }
                else
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Boolean,
                        new Property(PropertyType.Boolean,
                            ("Snap Enabled", true),
                            ("Grid Snap", true),
                            ("Object Snap", value)),
                        (_, _) => new Property(PropertyType.Boolean,
                            ("Snap Enabled", true),
                            ("Grid Snap", true),
                            ("Object Snap", value)));
                }
            }
        }

        public double SnapSpacing
        {
            get => properties.TryGetValue((int)PropertyType.Double, out var prop) ? (double)prop.GetValue(SPACING_INDEX) : 0.25;
            set => properties.AddOrUpdate(
                (int)PropertyType.Double,
                new Property(PropertyType.Double, "Snap Spacing", value),
                (_, _) => new Property(PropertyType.Double, "Snap Spacing", value));
        }

        public double ApertureSize
        {
            get => properties.TryGetValue((int)PropertyType.Size, out var prop) ? (double)prop.GetValue(APERTURE_INDEX) : 10.0;
            set => properties.AddOrUpdate(
                (int)PropertyType.Size,
                new Property(PropertyType.Size, "Aperture Size", value),
                (_, _) => new Property(PropertyType.Size, "Aperture Size", value));
        }

        [JsonIgnore, XmlIgnore]
        public new PropertyType PropertyType { get; set; }

        [JsonIgnore, XmlIgnore]
        public new object PropertyValue { get; set; }
    }
}