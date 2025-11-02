using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD
{
    public enum PropertyType
    {
        Boolean,
        Integer,
        Double,
        String,
        Color,
        Point,
        Vector,
        Curve,
        Surface,
        Solid,
        Material,
        Texture,
        Layer,
        LineType,
        LineWeight
    }

    /// <summary>
    /// Represents a named value within a property.
    /// </summary>
    public class PropertyValue
    {
        public string Name { get; set; }
        public object Value { get; set; }

        public PropertyValue(string name, object value)
        {
            Name = name ?? string.Empty;
            Value = value;
        }

        public override string ToString()
        {
            return $"{Name}: {Value}";
        }
    }

    public class Property
    {
        public PropertyType Type { get; set; }
        
        /// <summary>
        /// Collection of named values for this property type.
        /// Each value has its own name for display purposes.
        /// </summary>
        public List<PropertyValue> Values { get; set; }

        /// <summary>
        /// Creates a property with a single named value.
        /// </summary>
        public Property(PropertyType type, string name, object value)
        {
            Type = type;
            Values = new List<PropertyValue> { new PropertyValue(name, value) };
        }

        /// <summary>
        /// Creates a property with multiple named values.
        /// </summary>
        public Property(PropertyType type, params (string name, object value)[] namedValues)
        {
            Type = type;
            Values = new List<PropertyValue>();
            foreach (var (name, value) in namedValues)
            {
                Values.Add(new PropertyValue(name, value));
            }
        }

        /// <summary>
        /// Creates a property from a collection of PropertyValue objects.
        /// </summary>
        public Property(PropertyType type, IEnumerable<PropertyValue> values)
        {
            Type = type;
            Values = new List<PropertyValue>(values);
        }

        /// <summary>
        /// Gets the value at the specified index.
        /// </summary>
        public object GetValue(int index)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Property has {Values.Count} value(s).");
            
            return Values[index].Value;
        }

        /// <summary>
        /// Gets the name at the specified index.
        /// </summary>
        public string GetName(int index)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Property has {Values.Count} value(s).");
            
            return Values[index].Name;
        }

        /// <summary>
        /// Gets the PropertyValue at the specified index.
        /// </summary>
        public PropertyValue GetPropertyValue(int index)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Property has {Values.Count} value(s).");
            
            return Values[index];
        }

        /// <summary>
        /// Sets the value at the specified index (keeps the existing name).
        /// </summary>
        public void SetValue(int index, object value)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Property has {Values.Count} value(s).");
            
            Values[index].Value = value;
        }

        /// <summary>
        /// Sets both the name and value at the specified index.
        /// </summary>
        public void SetValue(int index, string name, object value)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Property has {Values.Count} value(s).");
            
            Values[index].Name = name;
            Values[index].Value = value;
        }

        /// <summary>
        /// Adds a new named value to the collection.
        /// </summary>
        public void AddValue(string name, object value)
        {
            Values.Add(new PropertyValue(name, value));
        }

        /// <summary>
        /// Removes a value at the specified index.
        /// </summary>
        public void RemoveValueAt(int index)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Property has {Values.Count} value(s).");
            
            Values.RemoveAt(index);
        }

        /// <summary>
        /// Gets the number of values in this property.
        /// </summary>
        public int Count => Values.Count;

        /// <summary>
        /// Indexer to access values directly by index.
        /// </summary>
        public object this[int index]
        {
            get => GetValue(index);
            set => SetValue(index, value);
        }

        /// <summary>
        /// Gets a value by name (returns the first match).
        /// </summary>
        public object? GetValueByName(string name)
        {
            var propertyValue = Values.FirstOrDefault(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return propertyValue?.Value;
        }

        /// <summary>
        /// Checks if a value with the specified name exists.
        /// </summary>
        public bool HasValue(string name)
        {
            return Values.Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public override string ToString()
        {
            if (Values.Count == 1)
                return Values[0].ToString() ?? string.Empty;
            else
                return $"{Type} [{string.Join(", ", Values)}]";
        }
    }
}
