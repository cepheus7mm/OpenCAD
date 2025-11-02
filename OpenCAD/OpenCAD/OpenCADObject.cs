using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenCAD
{
    public class OpenCADObject
    {
        protected ConcurrentDictionary<int, Property> properties = new();
        protected ConcurrentDictionary<Guid, OpenCADObject> children = new();

        protected bool _isDrawable = false;
        private Guid _id = Guid.NewGuid();

        // Layer reference (nullable - objects may not be on a layer yet)
        private Layer? _layer;

        public bool IsDrawable => _isDrawable;

        public void Add(OpenCADObject obj)
        {
            children.TryAdd(obj.ID, obj);
        }

        public Guid ID => _id;

        /// <summary>
        /// Remove a child object by reference
        /// </summary>
        public bool Remove(OpenCADObject obj)
        {
            return children.TryRemove(obj.ID, out _);
        }

        /// <summary>
        /// Remove a child object by ID
        /// </summary>
        public bool Remove(Guid id)
        {
            return children.TryRemove(id, out _);
        }

        /// <summary>
        /// Gets or sets the layer this object belongs to.
        /// </summary>
        public Layer? Layer
        {
            get => _layer;
            set => _layer = value;
        }

        /// <summary>
        /// Gets the effective color for rendering this object (RGBA with alpha support).
        /// Returns the object's color if set, otherwise returns the layer's color.
        /// </summary>
        public Color GetEffectiveColor()
        {
            // Check if object has a color override
            if (properties.TryGetValue((int)PropertyType.Color, out var colorProp))
            {
                return (Color)colorProp.GetValue(0);
            }

            // Fall back to layer color
            if (_layer != null)
            {
                return _layer.Color;
            }

            // Default fallback - fully opaque white
            return Color.FromArgb(255, 255, 255, 255);
        }

        /// <summary>
        /// Gets the effective line type for rendering this object.
        /// Returns the object's line type if set, otherwise returns the layer's line type.
        /// </summary>
        public LineType GetEffectiveLineType()
        {
            // Check if object has a line type override
            if (properties.TryGetValue((int)PropertyType.LineType, out var lineTypeProp))
            {
                var lineType = (LineType)lineTypeProp.GetValue(0);

                // If explicitly set to ByLayer, use layer's line type
                if (lineType != LineType.ByLayer)
                    return lineType;
            }

            // Fall back to layer line type
            if (_layer != null)
            {
                return _layer.LineType;
            }

            // Default fallback
            return LineType.Continuous;
        }

        /// <summary>
        /// Gets the effective line weight for rendering this object.
        /// Returns the object's line weight if set, otherwise returns the layer's line weight.
        /// </summary>
        public LineWeight GetEffectiveLineWeight()
        {
            // Check if object has a line weight override
            if (properties.TryGetValue((int)PropertyType.LineWeight, out var lineWeightProp))
            {
                var lineWeight = (LineWeight)lineWeightProp.GetValue(0);

                // If explicitly set to ByLayer, use layer's line weight
                if (lineWeight != LineWeight.ByLayer)
                    return lineWeight;
            }

            // Fall back to layer line weight
            if (_layer != null)
            {
                return _layer.LineWeight;
            }

            // Default fallback
            return LineWeight.Default;
        }

        /// <summary>
        /// Sets the color override for this object with RGB (fully opaque).
        /// </summary>
        public void SetColor(int red, int green, int blue)
        {
            SetColor(Color.FromArgb(255, red, green, blue));
        }

        /// <summary>
        /// Sets the color override for this object with RGBA (supports transparency).
        /// </summary>
        public void SetColor(int alpha, int red, int green, int blue)
        {
            SetColor(Color.FromArgb(alpha, red, green, blue));
        }

        /// <summary>
        /// Sets the color override for this object.
        /// Set to null to use layer color (ByLayer).
        /// </summary>
        public void SetColor(Color? color)
        {
            if (color.HasValue)
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Color,
                    new Property(PropertyType.Color, "Color", color.Value),
                    (key, oldValue) => new Property(PropertyType.Color, "Color", color.Value)
                );
            }
            else
            {
                // Remove color override - will use layer color
                properties.TryRemove((int)PropertyType.Color, out _);
            }
        }

        /// <summary>
        /// Sets the line type override for this object.
        /// Set to LineType.ByLayer or null to use layer line type.
        /// </summary>
        public void SetLineType(LineType? lineType)
        {
            if (lineType.HasValue)
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineType,
                    new Property(PropertyType.LineType, "LineType", lineType.Value),
                    (key, oldValue) => new Property(PropertyType.LineType, "LineType", lineType.Value)
                );
            }
            else
            {
                // Remove line type override - will use layer line type
                properties.TryRemove((int)PropertyType.LineType, out _);
            }
        }

        /// <summary>
        /// Sets the line weight override for this object.
        /// Set to LineWeight.ByLayer or null to use layer line weight.
        /// </summary>
        public void SetLineWeight(LineWeight? lineWeight)
        {
            if (lineWeight.HasValue)
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineWeight,
                    new Property(PropertyType.LineWeight, "LineWeight", lineWeight.Value),
                    (key, oldValue) => new Property(PropertyType.LineWeight, "LineWeight", lineWeight.Value)
                );
            }
            else
            {
                // Remove line weight override - will use layer line weight
                properties.TryRemove((int)PropertyType.LineWeight, out _);
            }
        }

        /// <summary>
        /// Gets whether this object should be visible based on its layer's visibility.
        /// </summary>
        public bool IsVisible()
        {
            if (_layer != null && !_layer.IsVisible)
                return false;

            return true;
        }

        /// <summary>
        /// Gets whether this object is selectable based on its layer's locked state.
        /// </summary>
        public bool IsSelectable()
        {
            if (_layer != null && _layer.IsLocked)
                return false;

            return true;
        }

        /// <summary>
        /// Gets all child objects.
        /// </summary>
        public IEnumerable<OpenCADObject> GetChildren()
        {
            return children.Values;
        }

        /// <summary>
        /// Gets a child object by ID.
        /// </summary>
        public OpenCADObject? GetChild(Guid id)
        {
            children.TryGetValue(id, out var child);
            return child;
        }

        /// <summary>
        /// Gets all properties of this object.
        /// </summary>
        public IEnumerable<Property> GetProperties()
        {
            return properties.Values;
        }

        /// <summary>
        /// Gets a property by PropertyType.
        /// </summary>
        public Property? GetProperty(PropertyType propertyType)
        {
            properties.TryGetValue((int)propertyType, out var property);
            return property;
        }
    }
}