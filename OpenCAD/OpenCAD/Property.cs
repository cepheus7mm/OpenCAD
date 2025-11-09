using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Runtime.Serialization;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using OpenCAD.Geometry;

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
        LineWeight,
        Size
    }

    /// <summary>
    /// Represents a named value within a property.
    /// Uses IJsonOnDeserialized to rehydrate Value and to sanitize SerializedValue (no JsonElement/$id left).
    /// </summary>
    public class PropertyValue : IJsonOnDeserialized
    {
        public string Name { get; set; }

        [JsonIgnore, XmlIgnore]
        private object? _value;

        [JsonIgnore, XmlIgnore]
        public object? Value
        {
            get => _value;
            set
            {
                _value = value;

                if (value is Color color)
                {
                    // Keep compact ARGB int storage
                    SerializedValue = color.ToArgb();
                    SerializedValueType = "Color";
                }
                else if (value is Guid guid)
                {
                    SerializedValue = guid.ToString();
                    SerializedValueType = "Guid";
                }
                else if (value is Point3D point)
                {
                    SerializedValue = new Dictionary<string, double>
                    {
                        ["X"] = point.X,
                        ["Y"] = point.Y,
                        ["Z"] = point.Z
                    };
                    SerializedValueType = "Point3D";
                }
                else
                {
                    SerializedValue = value;
                    SerializedValueType = value?.GetType().Name ?? "null";
                }
            }
        }

        // Serialized payload and a hint to reconstruct Value
        public object? SerializedValue { get; set; }
        public string? SerializedValueType { get; set; }

        // Called automatically by System.Text.Json (.NET 8)
        public void OnDeserialized()
        {
            try
            {
                // 1) Normalize JsonElement -> plain CLR types and remove any $id/$ref/$values/$type tokens.
                if (SerializedValue is JsonElement je)
                {
                    SerializedValue = SanitizeJsonElement(je);
                }

                // 2) Rehydrate the runtime Value from the sanitized payload + type hint
                switch (SerializedValueType)
                {
                    case "Color":
                        _value = RehydrateColor(SerializedValue);
                        break;

                    case "Guid":
                        _value = SerializedValue is string gs && Guid.TryParse(gs, out var g) ? g : Guid.Empty;
                        break;

                    case "Point3D":
                        _value = RehydratePoint3D(SerializedValue);
                        break;

                    case "Boolean":
                        _value = SerializedValue != null ? Convert.ToBoolean(SerializedValue) : null;
                        break;

                    case "Int32":
                        _value = SerializedValue != null ? Convert.ToInt32(SerializedValue) : null;
                        break;

                    case "Double":
                        _value = SerializedValue != null ? Convert.ToDouble(SerializedValue) : null;
                        break;

                    case "String":
                        _value = SerializedValue?.ToString() ?? string.Empty;
                        break;

                    case "LineType":
                        _value = DeserializeEnum<LineType>(SerializedValue);
                        break;

                    case "LineWeight":
                        _value = DeserializeEnum<LineWeight>(SerializedValue);
                        break;

                    default:
                        // Fallback: keep the sanitized payload as-is
                        _value = SerializedValue;
                        break;
                }

                if (_value == null && SerializedValue != null)
                    _value = SerializedValue; // final fallback
            }
            catch
            {
                _value = SerializedValue; // safe fallback on any error
            }
        }

        private static Color RehydrateColor(object? raw)
        {
            switch (raw)
            {
                case int argb:
                    return Color.FromArgb(argb);
                case long l:
                    return Color.FromArgb(unchecked((int)l));
                case double d:
                    return Color.FromArgb(Convert.ToInt32(d));
                case string s:
                    if (s.StartsWith("#", StringComparison.Ordinal))
                        s = s[1..];
                    if (uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var a))
                        return Color.FromArgb(unchecked((int)a));
                    break;
            }
            return Color.White;
        }

        private static Point3D RehydratePoint3D(object? raw)
        {
            double x = 0, y = 0, z = 0;

            if (raw is Dictionary<string, object?> od)
            {
                if (od.TryGetValue("X", out var xv)) x = Convert.ToDouble(xv);
                if (od.TryGetValue("Y", out var yv)) y = Convert.ToDouble(yv);
                if (od.TryGetValue("Z", out var zv)) z = Convert.ToDouble(zv);
            }
            else if (raw is Dictionary<string, double> dd)
            {
                dd.TryGetValue("X", out x);
                dd.TryGetValue("Y", out y);
                dd.TryGetValue("Z", out z);
            }

            return new Point3D(x, y, z);
        }

        private static T? DeserializeEnum<T>(object? raw) where T : struct
        {
            if (raw is string s && Enum.TryParse<T>(s, out var ev)) return ev;
            if (raw is int i && Enum.IsDefined(typeof(T), i)) return (T)Enum.ToObject(typeof(T), i);
            if (raw is double d)
            {
                var iv = Convert.ToInt32(d);
                if (Enum.IsDefined(typeof(T), iv)) return (T)Enum.ToObject(typeof(T), iv);
            }
            return default;
        }

        // Convert JsonElement to plain CLR types and strip metadata: $id, $ref, $values, $type
        private static object? SanitizeJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    // Prefer int when possible, else double
                    if (element.TryGetInt64(out var l))
                    {
                        if (l >= int.MinValue && l <= int.MaxValue) return (int)l;
                        return l;
                    }
                    return element.GetDouble();

                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null: return null;

                case JsonValueKind.Array:
                    {
                        var list = new List<object?>(element.GetArrayLength());
                        foreach (var item in element.EnumerateArray())
                            list.Add(SanitizeJsonElement(item));
                        return list;
                    }

                case JsonValueKind.Object:
                    {
                        // Handle arrays encoded as {"$id":"n","$values":[...]} by ReferenceHandler.Preserve
                        if (element.TryGetProperty("$values", out var valuesEl))
                        {
                            var list = new List<object?>(valuesEl.ValueKind == JsonValueKind.Array ? valuesEl.GetArrayLength() : 4);
                            if (valuesEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in valuesEl.EnumerateArray())
                                    list.Add(SanitizeJsonElement(item));
                            }
                            return list;
                        }

                        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
                        foreach (var prop in element.EnumerateObject())
                        {
                            // Drop metadata that would collide on next save
                            if (prop.Name is "$id" or "$ref" or "$type")
                                continue;

                            dict[prop.Name] = SanitizeJsonElement(prop.Value);
                        }
                        return dict;
                    }

                default:
                    return element.ToString();
            }
        }

        public PropertyValue()
        {
            Name = string.Empty;
            _value = null;
        }

        public PropertyValue(string name, object value)
        {
            Name = name ?? string.Empty;
            Value = value;
        }

        public override string ToString() => $"{Name}: {Value}";
    }

    public class Property
    {
        public PropertyType Type { get; set; }
        public List<PropertyValue> Values { get; set; }

        public Property() => Values = new List<PropertyValue>();

        public Property(PropertyType type, string name, object value)
        {
            Type = type;
            Values = new List<PropertyValue> { new PropertyValue(name, value) };
        }

        public Property(PropertyType type, params (string name, object value)[] namedValues)
        {
            Type = type;
            Values = new List<PropertyValue>();
            foreach (var (name, value) in namedValues)
                Values.Add(new PropertyValue(name, value));
        }

        public Property(PropertyType type, IEnumerable<PropertyValue> values)
        {
            Type = type;
            Values = new List<PropertyValue>(values);
        }

        public object GetValue(int index)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Values[index].Value ?? throw new InvalidOperationException($"Value at index {index} is null.");
        }

        public string GetName(int index)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Values[index].Name;
        }

        public PropertyValue GetPropertyValue(int index)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return Values[index];
        }

        public void SetValue(int index, object value)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            Values[index].Value = value;
        }

        public void SetValue(int index, string name, object value)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            Values[index].Name = name;
            Values[index].Value = value;
        }

        public void AddValue(string name, object value) => Values.Add(new PropertyValue(name, value));

        public void RemoveValueAt(int index)
        {
            if (index < 0 || index >= Values.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            Values.RemoveAt(index);
        }

        public int Count => Values.Count;

        public object this[int index]
        {
            get => GetValue(index);
            set => SetValue(index, value);
        }

        public object? GetValueByName(string name) =>
            Values.FirstOrDefault(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

        public bool HasValue(string name) =>
            Values.Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        public override string ToString()
        {
            return Values.Count == 1
                ? Values[0].ToString() ?? string.Empty
                : $"{Type} [{string.Join(", ", Values)}]";
        }
    }
}