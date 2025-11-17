using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Runtime.Serialization;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using OpenCAD.Geometry;
using OpenCAD.Settings;

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
        ID,
        UInt,
        SystemOfUnits,
        LinearUnits,
        AngularUnits,
    }

    public class Property
    {
        private object _value;
        private string _compilerName;
        private string _name;

        public PropertyType Type { get; set; }

        public Property(PropertyType type, string name, object value, string compilerName)
        {
            Type = type;
            _name = name;
            _value = value;
            _compilerName = compilerName;
        }

        public string CompilerName => _compilerName;

        public object Value
        {
            get => _value;
            set => _value = value;
        }

        public string Name => _name;

        public string ToStringRepresentation(OpenCADDocument? document)
        {
            return Type switch
            {
                PropertyType.Boolean => ((bool)_value).ToString(),
                PropertyType.Integer => ((int)_value).ToString(),
                PropertyType.Double => document is null ? ((double)_value).ToString() : document.ValueToString((double)_value, OpenCADDocument.UnitFormatType.Linear),
                PropertyType.String => (string)_value,
                PropertyType.Color => document is null ? ((Color)_value).ToArgb().ToString("X8") : document.ColorToString((Color)_value),
                PropertyType.Point => document is null ? ((Point3D)_value).ToString() : document.PointToString((Point3D)_value),
                PropertyType.Vector => document is null ? ((Vector3D)_value).ToString() : document.VectorToString((Vector3D)_value),
                _ => _value.ToString() ?? string.Empty,
            };
        }

        public void FromStringRepresentation(string strValue, OpenCADDocument? document)
        {
            switch (Type)
            {
                case PropertyType.Boolean:
                    _value = bool.Parse(strValue);
                    break;
                case PropertyType.Integer:
                    _value = int.Parse(strValue);
                    break;
                case PropertyType.UInt:
                    _value = uint.Parse(strValue);
                    break;
                case PropertyType.Double:
                    _value = document is null ? double.Parse(strValue) : document.StringToValue(strValue, OpenCADDocument.UnitFormatType.Linear);
                    break;
                case PropertyType.String:
                    _value = strValue;
                    break;
                case PropertyType.Color:
                    _value = document is null ? Color.FromArgb(int.Parse(strValue, System.Globalization.NumberStyles.HexNumber)) : document.StringToColor(strValue);
                    break;
                case PropertyType.Point:
                    _value = document is null ? Point3D.ParseFromPropertyString(strValue) : document.StringToPoint(strValue);
                    break;
                case PropertyType.Vector:
                    _value = document is null ? Vector3D.ParseFromPropertyString(strValue) : document.StringToVector(strValue);
                    break;
                default:
                    throw new NotSupportedException($"FromStringRepresentation is not supported for PropertyType {Type}");
            }
        }

    }

    public class PropertyJsonConverter : JsonConverter<Property>
    {
        public override Property? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Read the start object
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            PropertyType? type = null;
            string? name = null;
            object? value = null;
            string? compilerName = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                string propName = reader.GetString()!;
                reader.Read();

                switch (propName)
                {
                    case nameof(Property.Type):
                        type = Enum.Parse<PropertyType>(reader.GetString()!);
                        break;
                    case nameof(Property.Name):
                        name = reader.GetString();
                        break;
                    case nameof(Property.Value):
                        if (type == null)
                            throw new JsonException("Type must be read before Value.");

                        value = type switch
                        {
                            PropertyType.ID => reader.TokenType == JsonTokenType.String
                                ? Guid.Parse(reader.GetString()!)
                                : throw new JsonException("Expected string for Guid"),
                            PropertyType.Boolean => reader.GetBoolean(),
                            PropertyType.Integer => reader.GetInt32(),
                            PropertyType.Double => reader.GetDouble(),
                            PropertyType.String => reader.GetString(),
                            PropertyType.Color => reader.TokenType == JsonTokenType.Number
                                ? Color.FromArgb(reader.GetInt32())
                                : throw new JsonException("Expected number for Color (ARGB)"),
                            PropertyType.LineType => reader.TokenType == JsonTokenType.Number
                                ? (LineType)reader.GetInt32()
                                : (LineType)Enum.Parse(typeof(LineType), reader.GetString()!),
                            PropertyType.LineWeight => reader.TokenType == JsonTokenType.Number
                                ? (LineWeight)reader.GetInt32()
                                : (LineWeight)Enum.Parse(typeof(LineWeight), reader.GetString()!),
                            PropertyType.SystemOfUnits => reader.TokenType == JsonTokenType.Number
                                ? (UnitSystem)reader.GetInt32()
                                : (UnitSystem)Enum.Parse(typeof(UnitSystem), reader.GetString()!),
                            PropertyType.LinearUnits => reader.TokenType == JsonTokenType.Number
                                ? (LinearType)reader.GetInt32()
                                : (LinearType)Enum.Parse(typeof(LinearType), reader.GetString()!),
                            PropertyType.AngularUnits => reader.TokenType == JsonTokenType.Number
                                ? (AngularType)reader.GetInt32()
                                : (AngularType)Enum.Parse(typeof(AngularType), reader.GetString()!),
                            PropertyType.Point => JsonSerializer.Deserialize<Point3D>(ref reader, options),
                            PropertyType.Vector => JsonSerializer.Deserialize<Vector3D>(ref reader, options),
                            // Add more cases as needed for other types
                            _ => JsonSerializer.Deserialize<object>(ref reader, options)
                        };
                        break;
                    case nameof(Property.CompilerName):
                        compilerName = reader.GetString();
                        break;
                }
            }

            if (type == null || name == null || compilerName == null)
                throw new JsonException("Missing required property fields.");

            return new Property(type.Value, name, value!, compilerName);
        }

        public override void Write(Utf8JsonWriter writer, Property value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(Property.Type), value.Type.ToString());
            writer.WriteString(nameof(Property.Name), value.Name);
            writer.WriteString(nameof(Property.CompilerName), value.CompilerName);

            writer.WritePropertyName(nameof(Property.Value));
            switch (value.Type)
            {
                case PropertyType.ID:
                    writer.WriteStringValue((Guid)value.Value);
                    break;
                case PropertyType.Boolean:
                    writer.WriteBooleanValue((bool)value.Value);
                    break;
                case PropertyType.Integer:
                    writer.WriteNumberValue((int)value.Value);
                    break;
                case PropertyType.Double:
                    writer.WriteNumberValue((double)value.Value);
                    break;
                case PropertyType.String:
                    writer.WriteStringValue((string)value.Value);
                    break;
                case PropertyType.Color:
                    writer.WriteNumberValue(((Color)value.Value).ToArgb());
                    break;
                case PropertyType.LineType:
                    writer.WriteNumberValue((int)(LineType)value.Value);
                    break;
                case PropertyType.LineWeight:
                    writer.WriteNumberValue((int)(LineWeight)value.Value);
                    break;
                case PropertyType.SystemOfUnits:
                    writer.WriteNumberValue((int)(UnitSystem)value.Value);
                    break;
                case PropertyType.LinearUnits:
                    writer.WriteNumberValue((int)(LinearType)value.Value);
                    break;
                case PropertyType.AngularUnits:
                    writer.WriteNumberValue((int)(AngularType)value.Value);
                    break;
                case PropertyType.Point:
                    JsonSerializer.Serialize(writer, (Point3D)value.Value, options);
                    break;
                case PropertyType.Vector:
                    JsonSerializer.Serialize(writer, (Vector3D)value.Value, options);
                    break;
                // Add more cases as needed for other types
                default:
                    JsonSerializer.Serialize(writer, value.Value, options);
                    break;
            }
            writer.WriteEndObject();
        }
    }
}