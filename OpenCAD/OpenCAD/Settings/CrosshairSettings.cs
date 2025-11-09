using System.Drawing;
using OpenCAD;

namespace OpenCAD.Settings // CHANGED
{
    /// <summary>
    /// Settings for the crosshair cursor display.
    /// </summary>
    public class CrosshairSettings : OpenCADObject
    {
        private const int COLOR_INDEX = 0;
        private const int LINETYPE_INDEX = 0;
        private const int LINEWEIGHT_INDEX = 0;
        private const int PICKBOXSIZE_INDEX = 0;

        public CrosshairSettings() : this(null!)
        {
        }

        public CrosshairSettings(OpenCADDocument document)
        {
            // Initialize default crosshair values using properties
            properties.TryAdd((int)PropertyType.Color, 
                new Property(PropertyType.Color, "Crosshair Color", Color.LightBlue));
            
            properties.TryAdd((int)PropertyType.LineType, 
                new Property(PropertyType.LineType, "Crosshair Line Type", LineType.Continuous));
            
            properties.TryAdd((int)PropertyType.LineWeight, 
                new Property(PropertyType.LineWeight, "Crosshair Line Weight", LineWeight.Hairline));
            
            properties.TryAdd((int)PropertyType.Size, 
                new Property(PropertyType.Size, "Pickbox Size", 5.0));
            _document = document;
        }

        /// <summary>
        /// Gets or sets the color of the crosshair cursor.
        /// Default: LightBlue
        /// </summary>
        public Color Color
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Color, out var prop))
                    return (Color)prop.GetValue(COLOR_INDEX);
                return Color.LightBlue;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Color,
                    new Property(PropertyType.Color, "Crosshair Color", value),
                    (key, oldValue) => new Property(PropertyType.Color, "Crosshair Color", value)
                );
            }
        }

        /// <summary>
        /// Gets or sets the line type of the crosshair cursor.
        /// Default: Continuous
        /// </summary>
        public LineType LineType
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.LineType, out var prop))
                    return (LineType)prop.GetValue(LINETYPE_INDEX);
                return LineType.Continuous;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineType,
                    new Property(PropertyType.LineType, "Crosshair Line Type", value),
                    (key, oldValue) => new Property(PropertyType.LineType, "Crosshair Line Type", value)
                );
            }
        }

        /// <summary>
        /// Gets or sets the line weight of the crosshair cursor.
        /// Default: Hairline (thinnest)
        /// </summary>
        public LineWeight LineWeight
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.LineWeight, out var prop))
                    return (LineWeight)prop.GetValue(LINEWEIGHT_INDEX);
                return LineWeight.Hairline;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineWeight,
                    new Property(PropertyType.LineWeight, "Crosshair Line Weight", value),
                    (key, oldValue) => new Property(PropertyType.LineWeight, "Crosshair Line Weight", value)
                );
            }
        }

        /// <summary>
        /// Gets or sets the pickbox size in pixels.
        /// The pickbox is the small square at the crosshair center used for object selection.
        /// Default: 5.0 pixels
        /// </summary>
        public double PickboxSize
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Size, out var prop))
                    return (double)prop.GetValue(PICKBOXSIZE_INDEX);
                return 5.0;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Size,
                    new Property(PropertyType.Size, "Pickbox Size", value),
                    (key, oldValue) => new Property(PropertyType.Size, "Pickbox Size", value)
                );
            }
        }
    }
}