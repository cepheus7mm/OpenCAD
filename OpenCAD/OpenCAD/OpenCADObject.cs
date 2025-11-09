using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD
{
    public class OpenCADObject : IJsonOnDeserialized
    {
        [JsonIgnore, XmlIgnore]
        protected ConcurrentDictionary<int, Property> properties = new();

        [JsonIgnore, XmlIgnore]
        protected ConcurrentDictionary<Guid, OpenCADObject> children = new();

        [JsonIgnore, XmlIgnore]
        protected OpenCADDocument? _document;

        [JsonIgnore, XmlIgnore]
        protected OpenCADObject? _parent;

        [JsonIgnore, XmlIgnore]
        protected bool _isDrawable = false;

        [JsonIgnore, XmlIgnore]
        private Guid _id = Guid.NewGuid();

        public Dictionary<int, Property> SerializedProperties
        {
            get => properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            set
            {
                properties = value != null
                    ? new ConcurrentDictionary<int, Property>(value)
                    : new ConcurrentDictionary<int, Property>();
            }
        }

        public Dictionary<Guid, OpenCADObject> SerializedChildren
        {
            get => children.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            set
            {
                children = value != null
                    ? new ConcurrentDictionary<Guid, OpenCADObject>(value)
                    : new ConcurrentDictionary<Guid, OpenCADObject>();
            }
        }

        public Guid ID
        {
            get => _id;
            set => _id = value;
        }

        public bool IsDrawable
        {
            get => _isDrawable;
            set => _isDrawable = value;
        }

        [JsonIgnore, XmlIgnore]
        public string? Name
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.String, out var prop))
                {
                    var nameValue = prop.Values.FirstOrDefault(v => v.Name == OpenCADStrings.Name);
                    return nameValue?.Value as string;
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    if (properties.TryGetValue((int)PropertyType.String, out var prop))
                    {
                        var existingNameValue = prop.Values.FirstOrDefault(v => v.Name == OpenCADStrings.Name);
                        if (existingNameValue != null)
                            existingNameValue.Value = value;
                        else
                            prop.AddValue(OpenCADStrings.Name, value);
                    }
                    else
                    {
                        properties.TryAdd((int)PropertyType.String,
                            new Property(PropertyType.String, OpenCADStrings.Name, value));
                    }
                }
                else
                {
                    if (properties.TryGetValue((int)PropertyType.String, out var prop))
                    {
                        var nameIndex = prop.Values.FindIndex(v => v.Name == OpenCADStrings.Name);
                        if (nameIndex >= 0)
                        {
                            prop.RemoveValueAt(nameIndex);
                            if (prop.Values.Count == 0)
                                properties.TryRemove((int)PropertyType.String, out _);
                        }
                    }
                }
            }
        }

        [JsonIgnore, XmlIgnore]
        public OpenCADLayer? Layer
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Layer, out var layerProp))
                {
                    var layerId = (Guid)layerProp.GetValue(0);
                    return null;
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Layer,
                        new Property(PropertyType.Layer, OpenCADStrings.Layer, value.ID),
                        (_, _) => new Property(PropertyType.Layer, OpenCADStrings.Layer, value.ID)
                    );
                }
                else
                {
                    properties.TryRemove((int)PropertyType.Layer, out _);
                }
            }
        }

        [JsonIgnore, XmlIgnore]
        public OpenCADObject? Parent
        {
            get => _parent;
            internal set => _parent = value;
        }

        [JsonIgnore, XmlIgnore]
        public OpenCADDocument? Document
        {
            get => _document;
            internal set => _document = value;
        }

        public OpenCADObject(OpenCADDocument? document = null)
        {
            _document = document;
        }

        public OpenCADObject() { }

        // Called by System.Text.Json after this object is populated
        public void OnDeserialized()
        {
            // Ensure internal collections exist and clear cross-object refs.
            properties ??= new ConcurrentDictionary<int, Property>(SerializedProperties ?? new Dictionary<int, Property>());
            children ??= new ConcurrentDictionary<Guid, OpenCADObject>(SerializedChildren ?? new Dictionary<Guid, OpenCADObject>());

            _document = null;
            _parent = null;
        }

        public void Add(OpenCADObject obj)
        {
            if (children.TryAdd(obj.ID, obj))
            {
                obj._parent = this;
                obj._document = _document;
            }
        }

        public bool Remove(OpenCADObject obj)
        {
            if (children.TryRemove(obj.ID, out _))
            {
                obj._parent = null;
                return true;
            }
            return false;
        }

        public bool Remove(Guid id)
        {
            if (children.TryRemove(id, out var child))
            {
                child._parent = null;
                return true;
            }
            return false;
        }

        public IEnumerable<OpenCADObject> GetChildren() => children.Values;

        public OpenCADObject? GetChild(Guid id)
        {
            children.TryGetValue(id, out var child);
            return child;
        }

        public Guid? GetLayerId()
        {
            if (properties.TryGetValue((int)PropertyType.Layer, out var layerProp))
                return (Guid)layerProp.GetValue(0);
            return null;
        }

        public IEnumerable<Property> GetProperties() => properties.Values;

        public Property? GetProperty(PropertyType propertyType)
        {
            properties.TryGetValue((int)propertyType, out var property);
            return property;
        }

        public Color GetEffectiveColor()
        {
            if (properties.TryGetValue((int)PropertyType.Color, out var colorProp))
                return (Color)colorProp.GetValue(0);
            return Color.FromArgb(255, 255, 255, 255);
        }

        public Color GetEffectiveColor(OpenCADLayer? layer)
        {
            if (properties.TryGetValue((int)PropertyType.Color, out var colorProp))
                return (Color)colorProp.GetValue(0);
            return layer?.Color ?? Color.FromArgb(255, 255, 255, 255);
        }

        public LineType GetEffectiveLineType()
        {
            if (properties.TryGetValue((int)PropertyType.LineType, out var lineTypeProp))
            {
                var lineType = (LineType)lineTypeProp.GetValue(0);
                if (lineType != LineType.ByLayer) return lineType;
            }
            return LineType.Continuous;
        }

        public LineType GetEffectiveLineType(OpenCADLayer? layer)
        {
            if (properties.TryGetValue((int)PropertyType.LineType, out var lineTypeProp))
            {
                var lineType = (LineType)lineTypeProp.GetValue(0);
                if (lineType != LineType.ByLayer) return lineType;
            }
            return layer?.LineType ?? LineType.Continuous;
        }

        public LineWeight GetEffectiveLineWeight()
        {
            if (properties.TryGetValue((int)PropertyType.LineWeight, out var lwProp))
            {
                var lw = (LineWeight)lwProp.GetValue(0);
                if (lw != LineWeight.ByLayer) return lw;
            }
            return LineWeight.Default;
        }

        public LineWeight GetEffectiveLineWeight(OpenCADLayer? layer)
        {
            if (properties.TryGetValue((int)PropertyType.LineWeight, out var lwProp))
            {
                var lw = (LineWeight)lwProp.GetValue(0);
                if (lw != LineWeight.ByLayer) return lw;
            }
            return layer?.LineWeight ?? LineWeight.Default;
        }

        public void SetColor(int red, int green, int blue) => SetColor(Color.FromArgb(255, red, green, blue));
        public void SetColor(int alpha, int red, int green, int blue) => SetColor(Color.FromArgb(alpha, red, green, blue));

        public void SetColor(Color? color)
        {
            if (color.HasValue)
            {
                properties.AddOrUpdate(
                    (int)PropertyType.Color,
                    new Property(PropertyType.Color, OpenCADStrings.Color, color.Value),
                    (_, _) => new Property(PropertyType.Color, OpenCADStrings.Color, color.Value)
                );
            }
            else
            {
                properties.TryRemove((int)PropertyType.Color, out _);
            }
        }

        public void SetLineType(LineType? lineType)
        {
            if (lineType.HasValue)
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineType,
                    new Property(PropertyType.LineType, OpenCADStrings.LineType, lineType.Value),
                    (_, _) => new Property(PropertyType.LineType, OpenCADStrings.LineType, lineType.Value)
                );
            }
            else
            {
                properties.TryRemove((int)PropertyType.LineType, out _);
            }
        }

        public void SetLineWeight(LineWeight? lineWeight)
        {
            if (lineWeight.HasValue)
            {
                properties.AddOrUpdate(
                    (int)PropertyType.LineWeight,
                    new Property(PropertyType.LineWeight, OpenCADStrings.LineWeight, lineWeight.Value),
                    (_, _) => new Property(PropertyType.LineWeight, OpenCADStrings.LineWeight, lineWeight.Value)
                );
            }
            else
            {
                properties.TryRemove((int)PropertyType.LineWeight, out _);
            }
        }

        public bool IsVisible(OpenCADLayer? layer = null) => layer == null || layer.IsVisible;
        public bool IsSelectable(OpenCADLayer? layer = null) => layer == null || !layer.IsLocked;

        public void CompleteDeserialization()
        {
            System.Diagnostics.Debug.WriteLine($"  CompleteDeserialization for {GetType().Name} (ID: {ID})");

            try
            {
                if (SerializedProperties != null && SerializedProperties.Count > 0)
                    properties = new ConcurrentDictionary<int, Property>(SerializedProperties);

                if (SerializedChildren != null && SerializedChildren.Count > 0)
                    children = new ConcurrentDictionary<Guid, OpenCADObject>(SerializedChildren);

                _isDrawable = IsDrawable;
                _id = ID;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    ERROR in CompleteDeserialization: {ex.Message}");
                throw;
            }
        }
    }
}