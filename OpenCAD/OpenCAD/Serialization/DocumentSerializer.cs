using OpenCAD.Settings;
using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OpenCAD.Serialization
{
    public static class DocumentSerializer
    {
        private static readonly object _polyLock = new();
        private static readonly List<(Type Type, string Discriminator)> _externalDerived = new();
        private static readonly HashSet<Type> _externalTypes = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            // Removed ReferenceHandler.Preserve for clean JSON without $id/$ref
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new ColorJsonConverter(),
                new ConcurrentDictionaryConverter<int, Property>(),
                new ConcurrentDictionaryConverter<Guid, OpenCADObject>()
            },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { ConfigurePolymorphism }
            }
        };

        public static void RegisterPolymorphicDerivedType<TDerived>(string discriminator)
            where TDerived : OpenCADObject =>
            RegisterPolymorphicDerivedType(typeof(TDerived), discriminator);

        public static void RegisterPolymorphicDerivedType(Type derivedType, string discriminator)
        {
            if (derivedType is null) throw new ArgumentNullException(nameof(derivedType));
            if (!typeof(OpenCADObject).IsAssignableFrom(derivedType))
                throw new ArgumentException("Derived type must inherit OpenCADObject.", nameof(derivedType));
            if (string.IsNullOrWhiteSpace(discriminator))
                throw new ArgumentException("Discriminator cannot be null or whitespace.", nameof(discriminator));

            lock (_polyLock)
            {
                if (_externalTypes.Add(derivedType))
                    _externalDerived.Add((derivedType, discriminator));
            }
        }

        private static void ConfigurePolymorphism(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Type == typeof(OpenCADObject))
            {
                typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$type",
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType
                };

                typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(OpenCADDocument), "document"));
                typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(OpenCADLayer), "layer"));
                typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(OpenCAD.Geometry.Line), "line"));
                typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(ViewportSettings), "viewportSettings"));
                typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(GridSettings), "gridSettings"));
                typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(CrosshairSettings), "crosshairSettings"));
                typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(SnapSettings), "snapSettings"));

                lock (_polyLock)
                {
                    foreach (var (t, disc) in _externalDerived)
                        typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(t, disc));
                }
            }
        }

        public static void SaveToJson(OpenCADDocument document, string filePath)
        {
            try
            {
                string json = JsonSerializer.Serialize(document, JsonOptions);
                File.WriteAllText(filePath, json);
                System.Diagnostics.Debug.WriteLine($"Document saved successfully to: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public static OpenCADDocument? LoadFromJson(string filePath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Loading document from: {filePath}");

                string json = File.ReadAllText(filePath);
                System.Diagnostics.Debug.WriteLine($"JSON file read, length: {json.Length} characters");

                var document = JsonSerializer.Deserialize<OpenCADDocument>(json, JsonOptions);
                System.Diagnostics.Debug.WriteLine($"Deserialization complete, document: {(document != null ? "valid" : "NULL")}");

                if (document != null)
                {
                    System.Diagnostics.Debug.WriteLine("Calling InitializeAfterDeserialization...");
                    document.InitializeAfterDeserialization();

                    System.Diagnostics.Debug.WriteLine("Verifying initialization...");
                    if (!document.EnsureInitialized())
                        throw new InvalidOperationException("Document failed to initialize after deserialization");

                    System.Diagnostics.Debug.WriteLine($"Document fully initialized and verified!");
                }

                System.Diagnostics.Debug.WriteLine($"Document loaded successfully from: {filePath}");
                return document;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }

    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                int argb = reader.GetInt32();
                return Color.FromArgb(argb);
            }
            return Color.White;
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value.ToArgb());
    }

    public class ConcurrentDictionaryConverter<TKey, TValue> : JsonConverter<System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>>
        where TKey : notnull
    {
        public override System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var dictionary = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(ref reader, options);
            return dictionary != null
                ? new System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>(dictionary)
                : new System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue>();
        }

        public override void Write(
            Utf8JsonWriter writer,
            System.Collections.Concurrent.ConcurrentDictionary<TKey, TValue> value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), options);
        }
    }
}