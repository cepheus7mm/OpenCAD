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
        protected ConcurrentDictionary<int, List<Property>> properties = new();

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

        public Dictionary<int, List<Property>> SerializedProperties
        {
            get => properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            set
            {
                properties = value != null
                    ? new ConcurrentDictionary<int, List<Property>>(value)
                    : new ConcurrentDictionary<int, List<Property>>();
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

        private readonly object _propertyLock = new();

        [JsonIgnore, XmlIgnore]
        public string? Name
        {
            get => GetPropertyValue<string>(PropertyType.String, nameof(Name));
            set => SetPropertyValue(PropertyType.String, nameof(Name), OpenCADStrings.Name, value);
        }

        [JsonIgnore, XmlIgnore]
        public OpenCADLayer? Layer
        {
            get => GetLayer();
            set => SetPropertyValue(PropertyType.ID, nameof(LayerID), OpenCADStrings.LayerID, value?.ID);
        }

        [JsonIgnore, XmlIgnore]
        public Guid LayerID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(LayerID));
            set => SetPropertyValue(PropertyType.ID, nameof(LayerID), OpenCADStrings.LayerID, value);
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
            properties ??= new ConcurrentDictionary<int, List<Property>>(SerializedProperties ?? new Dictionary<int, List<Property>>());
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
            return Layer?.ID;
        }

        public IEnumerable<Property> GetProperties() => GetAllProperties();

        //public void CompleteDeserialization()
        //{
        //    //System.Diagnostics.Debug.WriteLine($"  CompleteDeserialization for {GetType().Name} (ID: {ID})");

        //    try
        //    {
        //        if (SerializedProperties != null && SerializedProperties.Count > 0)
        //            properties = new ConcurrentDictionary<int, List<Property>>(SerializedProperties);

        //        if (SerializedChildren != null && SerializedChildren.Count > 0)
        //            children = new ConcurrentDictionary<Guid, OpenCADObject>(SerializedChildren);

        //        _isDrawable = IsDrawable;
        //        _id = ID;
        //    }
        //    catch (Exception ex)
        //    {
        //        //System.Diagnostics.Debug.WriteLine($"    ERROR in CompleteDeserialization: {ex.Message}");
        //        throw;
        //    }
        //}

        protected T? GetPropertyValue<T>(PropertyType type, string compilerName)
        {
            if (properties.TryGetValue((int)type, out var propList))
            {
                lock (_propertyLock)
                {
                    var prop = propList.FirstOrDefault(p => p.CompilerName == compilerName);
                    if (prop != null && prop.Value is T tValue)
                        return tValue;
                }
            }
            return default;
        }

        protected void SetPropertyValue<T>(PropertyType type, string compilerName, string displayName, T? value)
        {
            lock (_propertyLock)
            {
                if (!properties.TryGetValue((int)type, out var propList))
                {
                    propList = new List<Property>();
                    properties[(int)type] = propList;
                }
                var prop = propList.FirstOrDefault(p => p.CompilerName == compilerName);
                if (value != null)
                {
                    if (prop != null)
                        prop.Value = value;
                    else
                        propList.Add(new Property(type, displayName, value, compilerName));
                }
                else
                {
                    if (prop != null)
                        propList.Remove(prop);
                }
            }
        }

        public List<Property> GetAllProperties()
        {
            List<Property> allProperties;
            lock (_propertyLock)
            {
                allProperties = properties.Values.SelectMany(list => list).ToList();
            }
            return allProperties;
        }

        private OpenCADLayer? GetLayer()
        {
            var document = _parent;
            while (document is not null && document is not OpenCADDocument)
            {
                document = document?.Parent;
            }
            if (document is null)
                return null;

            return ((OpenCADDocument)document).GetLayer(LayerID);
        }

        public OpenCADObject Clone(OpenCADDocument? document = null)
        {
            var clone = (OpenCADObject)MemberwiseClone();
            clone.ID = Guid.NewGuid();
            clone._document = document ?? _document;
            clone._parent = null;
            // Deep copy properties
            clone.properties = new ConcurrentDictionary<int, List<Property>>();
            foreach (var kvp in properties)
            {
                List<Property> propListCopy = kvp.Value.Select(p => p.Clone()).ToList();
                clone.properties[kvp.Key] = propListCopy;
            }
            // Deep copy children
            clone.children = new ConcurrentDictionary<Guid, OpenCADObject>();
            foreach (var child in children.Values)
            {
                var childClone = child.Clone(clone._document);
                childClone._parent = clone;
                clone.children[childClone.ID] = childClone;
            }
            return clone;
		}
	}
}