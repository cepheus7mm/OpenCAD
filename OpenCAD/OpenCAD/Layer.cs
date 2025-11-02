using System;
using System.Drawing;

namespace OpenCAD
{
    /// <summary>
    /// Represents a layer in the CAD document.
    /// Layers organize drawable objects and define default visual properties.
    /// </summary>
    public class Layer : OpenCADObject
    {
        // Define property indices for the Boolean type (since we have multiple boolean properties)
        private const int VISIBLE_INDEX = 0;
        private const int LOCKED_INDEX = 1;

        /// <summary>
        /// Creates a new layer with default properties.
        /// </summary>
        /// <param name="name">The name of the layer. Must be unique within the document.</param>
        public Layer(string name) : this(name, Color.White, LineType.Continuous, LineWeight.Default)
        {
        }

        /// <summary>
        /// Creates a new layer with specified properties.
        /// </summary>
        /// <param name="name">The name of the layer. Must be unique within the document.</param>
        /// <param name="color">The default color for objects on this layer.</param>
        /// <param name="lineType">The default line type for objects on this layer.</param>
        /// <param name="lineWeight">The default line weight for objects on this layer.</param>
        public Layer(string name, Color color, LineType lineType, LineWeight lineWeight)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Layer name cannot be null or empty.", nameof(name));

            _isDrawable = false; // Layers themselves are not drawable

            // Initialize properties using the collection-based system
            properties.TryAdd((int)PropertyType.String, new Property(PropertyType.String, "Name", name));
            properties.TryAdd((int)PropertyType.Color, new Property(PropertyType.Color, "Color", color));
            properties.TryAdd((int)PropertyType.LineType, new Property(PropertyType.LineType, "LineType", lineType));
            properties.TryAdd((int)PropertyType.LineWeight, new Property(PropertyType.LineWeight, "LineWeight", lineWeight));
            
            // Store both boolean values in a single Boolean property
            properties.TryAdd((int)PropertyType.Boolean, new Property(PropertyType.Boolean, 
                ("IsVisible", true),
                ("IsLocked", false)
            ));
        }

        /// <summary>
        /// Gets or sets the layer name. Must be unique within the document.
        /// </summary>
        public string Name
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.String, out var prop))
                    return (string)prop.GetValue(0);
                return string.Empty;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Layer name cannot be null or empty.", nameof(value));
                
                properties.AddOrUpdate(
                    (int)PropertyType.String,
                    new Property(PropertyType.String, "Name", value),
                    (key, oldValue) => new Property(PropertyType.String, "Name", value)
                );
            }
        }

        /// <summary>
        /// Gets or sets the default color for objects on this layer.
        /// </summary>
        public Color Color
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Color, out var prop))
                    return (Color)prop.GetValue(0);
                return Color.White;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Color,
                    new Property(PropertyType.Color, "Color", value),
                    (key, oldValue) => new Property(PropertyType.Color, "Color", value)
                );
            }
        }

        /// <summary>
        /// Gets or sets the default line type for objects on this layer.
        /// </summary>
        public LineType LineType
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.LineType, out var prop))
                    return (LineType)prop.GetValue(0);
                return LineType.Continuous;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineType,
                    new Property(PropertyType.LineType, "LineType", value),
                    (key, oldValue) => new Property(PropertyType.LineType, "LineType", value)
                );
            }
        }

        /// <summary>
        /// Gets or sets the default line weight for objects on this layer.
        /// </summary>
        public LineWeight LineWeight
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.LineWeight, out var prop))
                    return (LineWeight)prop.GetValue(0);
                return LineWeight.Default;
            }
            set
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineWeight,
                    new Property(PropertyType.LineWeight, "LineWeight", value),
                    (key, oldValue) => new Property(PropertyType.LineWeight, "LineWeight", value)
                );
            }
        }

        /// <summary>
        /// Gets or sets whether the layer is visible.
        /// When false, objects on this layer will not be displayed.
        /// </summary>
        public bool IsVisible
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                    return (bool)prop.GetValue(VISIBLE_INDEX);
                return true;
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                {
                    prop.SetValue(VISIBLE_INDEX, value);
                }
                else
                {
                    // If property doesn't exist yet, create it with both boolean values
                    properties.TryAdd((int)PropertyType.Boolean, new Property(PropertyType.Boolean, ("IsVisible", value), ("IsLocked", false)));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the layer is locked.
        /// When true, objects on this layer cannot be edited or selected.
        /// </summary>
        public bool IsLocked
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                    return (bool)prop.GetValue(LOCKED_INDEX);
                return false;
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.Boolean, out var prop))
                {
                    prop.SetValue(LOCKED_INDEX, value);
                }
                else
                {
                    // If property doesn't exist yet, create it with both boolean values
                    properties.TryAdd((int)PropertyType.Boolean, new Property(PropertyType.Boolean, ("IsVisible", true), ("IsLocked", value)));
                }
            }
        }

        public override string ToString()
        {
            return $"Layer: {Name} (Color: {Color.Name}, LineType: {LineType}, LineWeight: {LineWeight})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is Layer other)
                return ID == other.ID;
            return false;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
    }
}