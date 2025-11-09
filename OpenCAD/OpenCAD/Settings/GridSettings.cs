using System.Drawing;
using OpenCAD;

namespace OpenCAD.Settings // CHANGED
{
    /// <summary>
    /// Settings for grid display.
    /// </summary>
    public class GridSettings : OpenCADObject
    {
        private const int COLOR_INDEX = 0;
        private const int VISIBLE_INDEX = 0;
        private const int MAJORSPACING_INDEX = 0;
        private const int MINORSPACING_INDEX = 1;
        private const int MINORLINES_INDEX = 0;

        public GridSettings() : this(null!)
        {
        }

        public GridSettings(OpenCADDocument document)
        {
            // Initialize default grid values using properties
            properties.TryAdd((int)PropertyType.Color, 
                new Property(PropertyType.Color, "Grid Color", Color.Gray));
            
            properties.TryAdd((int)PropertyType.Boolean, 
                new Property(PropertyType.Boolean, "Show Grid", true));
            
            properties.TryAdd((int)PropertyType.Double, 
                new Property(PropertyType.Double, 
                    ("Major Spacing", 10.0),
                    ("Minor Spacing", 1.0)));
            
            properties.TryAdd((int)PropertyType.Integer, 
                new Property(PropertyType.Integer, "Minor Lines Per Major", 10));
            _document = document;
        }

        /// <summary>
        /// Gets or sets the color of the grid lines.
        /// Default: Gray
        /// </summary>
        public Color Color
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Color, out var prop))
                    return (Color)prop.GetValue(COLOR_INDEX);
                return Color.Gray;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Color,
                    new Property(PropertyType.Color, "Grid Color", value),
                    (key, oldValue) => new Property(PropertyType.Color, "Grid Color", value)
                );
            }
        }

        /// <summary>
        /// Gets or sets whether the grid is visible.
        /// Default: true
        /// </summary>
        public bool ShowGrid
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                    return (bool)prop.GetValue(VISIBLE_INDEX);
                return true;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Boolean,
                    new Property(PropertyType.Boolean, "Show Grid", value),
                    (key, oldValue) => new Property(PropertyType.Boolean, "Show Grid", value)
                );
            }
        }

        /// <summary>
        /// Gets or sets the spacing between major grid lines.
        /// Default: 10.0 units
        /// </summary>
        public double MajorSpacing
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Double, out var prop))
                    return (double)prop.GetValue(MAJORSPACING_INDEX);
                return 10.0;
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.Double, out var prop))
                {
                    var minorSpacing = (double)prop.GetValue(MINORSPACING_INDEX);
                    properties.AddOrUpdate(
                        (int)PropertyType.Double,
                        new Property(PropertyType.Double, 
                            ("Major Spacing", value),
                            ("Minor Spacing", minorSpacing)),
                        (key, oldValue) => new Property(PropertyType.Double, 
                            ("Major Spacing", value),
                            ("Minor Spacing", minorSpacing))
                    );
                }
                else
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Double,
                        new Property(PropertyType.Double, 
                            ("Major Spacing", value),
                            ("Minor Spacing", 1.0)),
                        (key, oldValue) => new Property(PropertyType.Double, 
                            ("Major Spacing", value),
                            ("Minor Spacing", 1.0))
                    );
                }
            }
        }

        /// <summary>
        /// Gets or sets the spacing between minor grid lines.
        /// Default: 1.0 units
        /// </summary>
        public double MinorSpacing
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Double, out var prop))
                    return (double)prop.GetValue(MINORSPACING_INDEX);
                return 1.0;
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.Double, out var prop))
                {
                    var majorSpacing = (double)prop.GetValue(MAJORSPACING_INDEX);
                    properties.AddOrUpdate(
                        (int)PropertyType.Double,
                        new Property(PropertyType.Double, 
                            ("Major Spacing", majorSpacing),
                            ("Minor Spacing", value)),
                        (key, oldValue) => new Property(PropertyType.Double, 
                            ("Major Spacing", majorSpacing),
                            ("Minor Spacing", value))
                    );
                }
                else
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Double,
                        new Property(PropertyType.Double, 
                            ("Major Spacing", 10.0),
                            ("Minor Spacing", value)),
                        (key, oldValue) => new Property(PropertyType.Double, 
                            ("Major Spacing", 10.0),
                            ("Minor Spacing", value))
                    );
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of minor grid lines between major grid lines.
        /// Default: 10
        /// </summary>
        public int MinorLinesPerMajor
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Integer, out var prop))
                    return (int)prop.GetValue(MINORLINES_INDEX);
                return 10;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Integer,
                    new Property(PropertyType.Integer, "Minor Lines Per Major", value),
                    (key, oldValue) => new Property(PropertyType.Integer, "Minor Lines Per Major", value)
                );
            }
        }
    }
}