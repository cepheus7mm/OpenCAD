using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using OpenCAD.Settings;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD
{
    /// <summary>
    /// Represents a root CAD document/file with associated metadata.
    /// Child objects represent geometry, settings, and other drawing elements.
    /// </summary>
    public class OpenCADDocument : OpenCADObject
    {
        public enum UnitFormatType
        {
            Linear,
            Angular,
            Vector3D,
        }

        // Cache for quick layer lookup by name
        [JsonIgnore, XmlIgnore]
        private ConcurrentDictionary<string, Guid> _layerNameToId = new();

        // Add volatile to ensure visibility across threads
        private volatile IServiceProvider? _serviceProvider;

        public OpenCADDocument()
        {
            // Initialize string properties with names (filename and description)
            Filename = string.Empty;
            Description = string.Empty;

            // Create a container object to hold all layers
            var layersContainer = new OpenCADObject(this) { Name = OpenCADStrings.LayersContainer };
            Add(layersContainer);

            LayersContainerID = layersContainer.ID;

            // Create default "0" layer (standard in CAD systems)
            var defaultLayer = new OpenCADLayer(OpenCADStrings.DefaultLayerName, Color.White, LineType.Continuous, LineWeight.Default, this);
            layersContainer.Add(defaultLayer);
            _layerNameToId.TryAdd(OpenCADStrings.DefaultLayerName, defaultLayer.ID);
            CurrentLayer = defaultLayer;
            CurrentLineType = LineType.ByLayer;
            CurrentLineWeight = LineWeight.ByLayer;
            CurrentColor = Color.FromArgb(0,0,0,0); // ByLayer

            // Create a container object to hold all text styles
            var textStylesContainer = new OpenCADObject(this) { Name = OpenCADStrings.TextStylesContainer };
            Add(textStylesContainer);
            TextStylesContainerID = textStylesContainer.ID;

            var defaultTextStyle = new OpenCADTextStyle(OpenCADStrings.DefaultTextStyleName, "Arial", 4.0, this);
            textStylesContainer.Add(defaultTextStyle);
            CurrentTextStyle = defaultTextStyle;

            // Add other default settings objects as children if needed
            var viewportSettings = new ViewportSettings(this);
            Add(viewportSettings);

            CurrentViewportSettingsID = viewportSettings.ID;
            LastGeometricChild = Guid.Empty;
        }

        public OpenCADDocument(string filename, string description = "") : this()
        {
            Filename = filename;
            Description = description;
            _document = this;
        }

        /// <summary>
        /// Ensures the document is fully initialized after deserialization.
        /// Call this after loading from JSON to verify all properties are accessible.
        /// </summary>
        public bool EnsureInitialized()
        {
            try
            {
                // Force properties to be accessed to trigger any lazy initialization
                var test1 = Filename;
                var test2 = Description;
                var test3 = CurrentLayer;
                var test4 = GetLayers().ToList();
                
                // If we got here without exception, document is initialized
                //System.Diagnostics.Debug.WriteLine($"Document initialized: {test4.Count} layers, current: {test3?.Name ?? "null"}");
                return true;
            }
            catch (Exception ex)
            {
                //System.Diagnostics.Debug.WriteLine($"Document NOT initialized: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Called after deserialization to rebuild caches and restore references.
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            //System.Diagnostics.Debug.WriteLine("=== OpenCADDocument.OnDeserialized START ===");
            
            try
            {
                // Rebuild the layer name-to-ID cache
                _layerNameToId = new ConcurrentDictionary<string, Guid>();
                
                //System.Diagnostics.Debug.WriteLine($"Rebuilding layer cache...");
                
                var layers = GetLayers().ToList();
                //System.Diagnostics.Debug.WriteLine($"Found {layers.Count} layers");
                
                foreach (var layer in layers)
                {
                    //System.Diagnostics.Debug.WriteLine($"  Layer: {layer.Name} (ID: {layer.ID})");
                    _layerNameToId.TryAdd(layer.Name, layer.ID);
                    
                    // Restore document reference
                    layer.Document = this;
                }
                
                // Recursively restore document and parent references for all children
                //System.Diagnostics.Debug.WriteLine("Restoring references...");
                RestoreReferences(this, this);
                
                //System.Diagnostics.Debug.WriteLine("=== OpenCADDocument.OnDeserialized COMPLETE ===");
            }
            catch (Exception ex)
            {
                //System.Diagnostics.Debug.WriteLine($"=== OpenCADDocument.OnDeserialized FAILED: {ex.Message} ===");
                //System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Recursively restores document and parent references after deserialization.
        /// </summary>
        private void RestoreReferences(OpenCADObject parent, OpenCADDocument document)
        {
            foreach (var child in parent.GetChildren())
            {
                child.Document = document;
                child.Parent = parent;
                
                // Recursively process grandchildren
                RestoreReferences(child, document);
            }
        }

        /// <summary>
        /// Gets or sets the filename of the CAD document.
        /// </summary>
        [JsonIgnore] // or [XmlIgnore]
        public string Filename
        {
            get => GetPropertyValue<string>(PropertyType.String, nameof(Filename));
            set => SetPropertyValue(PropertyType.String, nameof(Filename), OpenCADStrings.Filename, value);
        }

        /// <summary>
        /// Gets or sets the description of the CAD document.
        /// </summary>
        [JsonIgnore]
        public string Description
        {
            get => GetPropertyValue<string>(PropertyType.String, nameof(Description)) ?? string.Empty;
            set => SetPropertyValue(PropertyType.String, nameof(Description), OpenCADStrings.Description, value);
        }

        /// <summary>
        /// Gets or sets the current active layer for new objects.
        /// </summary>
        [JsonIgnore]
        public OpenCADLayer CurrentLayer
        {
            get => GetChild(LayersContainerID)?.GetChild(CurrentLayerID) as OpenCADLayer ?? new OpenCADLayer();
            set => SetPropertyValue(PropertyType.ID, nameof(CurrentLayerID), OpenCADStrings.CurrentLayerID, value.ID);
        }

        /// <summary>
        /// Gets or sets the current active layer's Id for new objects.
        /// </summary>
        [JsonIgnore]
        public Guid CurrentLayerID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(CurrentLayerID));
            set => SetPropertyValue(PropertyType.ID, nameof(CurrentLayerID), OpenCADStrings.CurrentLayerID, value);
        }


        /// <summary>
        /// Gets or sets the current active layer for new objects.
        /// </summary>
        [JsonIgnore]
        public OpenCADTextStyle CurrentTextStyle
        {
            get => GetChild(TextStylesContainerID)?.GetChild(CurrentTextStyleID) as OpenCADTextStyle ?? new OpenCADTextStyle();
            set => SetPropertyValue(PropertyType.ID, nameof(CurrentTextStyleID), OpenCADStrings.CurrentTextStyleID, value.ID);
        }

        /// <summary>
        /// Gets or sets the current active layer's Id for new objects.
        /// </summary>
        [JsonIgnore]
        public Guid CurrentTextStyleID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(CurrentTextStyleID));
            set => SetPropertyValue(PropertyType.ID, nameof(CurrentTextStyleID), OpenCADStrings.CurrentTextStyleID, value);
        }

        [JsonIgnore]
        public Guid? CurrentViewportSettingsID
        {
            get => GetPropertyValue<Guid?>(PropertyType.ID, nameof(CurrentViewportSettingsID));
            private set => SetPropertyValue(PropertyType.ID, nameof(CurrentViewportSettingsID), OpenCADStrings.CurrentViewportSettingsID, value);
        }

        /// <summary>
        /// Gets or sets the current color for new objects.
        /// If null, new objects will use ByLayer color.
        /// </summary>
        [JsonIgnore]
        public Color CurrentColor
        {
            get => GetPropertyValue<Color>(PropertyType.Color, nameof(CurrentColor));
            set => SetPropertyValue(PropertyType.Color, nameof(CurrentColor), OpenCADStrings.CurrentColor, value);
        }

        /// <summary>
        /// Gets or sets the current line type for new objects.
        /// If null, new objects will use ByLayer line type.
        /// </summary>
        [JsonIgnore]
        public LineType? CurrentLineType
        {
            get => GetPropertyValue<LineType>(PropertyType.LineType, nameof(CurrentLineType));
            set => SetPropertyValue(PropertyType.LineType, nameof(CurrentLineType), OpenCADStrings.CurrentLineType, value);
        }

        /// <summary>
        /// Gets or sets the current line weight for new objects.
        /// If null, new objects will use ByLayer line weight.
        /// </summary>
        [JsonIgnore]
        public LineWeight? CurrentLineWeight
        {
            get => GetPropertyValue<LineWeight>(PropertyType.LineWeight, nameof(CurrentLineWeight));
            set => SetPropertyValue(PropertyType.LineWeight, nameof(CurrentLineWeight), OpenCADStrings.CurrentLineWeight, value);
        }

        [JsonIgnore, XmlIgnore]
        public Guid? LastGeometricChild
        {
            get => GetPropertyValue<Guid?>(PropertyType.ID, nameof(LastGeometricChild));
            private set => SetPropertyValue(PropertyType.ID, nameof(LastGeometricChild), OpenCADStrings.LastGeometricChild, value);
        }

        public override bool Add(OpenCADObject obj)
        {            
            var added = base.Add(obj);
            if (added && obj is IDrawable)
            {
                LastGeometricChild = obj.ID;
            }
            return added;
        }

        public IDrawable? GetLastGeometricChild()
        {
            if (LastGeometricChild.HasValue && LastGeometricChild != Guid.Empty)
            {
                var obj = GetChild(LastGeometricChild.Value);
                return obj as IDrawable;
            }
            return null;
        }

        /// <summary>
        /// Adds a new layer to the document.
        /// </summary>
        /// <param name="layer">The layer to add.</param>
        /// <returns>True if the layer was added successfully, false if a layer with the same name already exists.</returns>
        public bool AddLayer(OpenCADLayer layer)
        {
            if (layer == null)
                throw new ArgumentNullException(nameof(layer));

            // Check if layer name already exists
            if (_layerNameToId.ContainsKey(layer.Name))
                return false;

            var layersContainer = GetLayersContainer();
            if (layersContainer != null)
            {
                layersContainer.Add(layer);
                _layerNameToId.TryAdd(layer.Name, layer.ID);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates and adds a new layer to the document.
        /// </summary>
        /// <param name="name">The name of the new layer.</param>
        /// <param name="color">The default color for the layer.</param>
        /// <param name="lineType">The default line type for the layer.</param>
        /// <param name="lineWeight">The default line weight for the layer.</param>
        /// <returns>The newly created layer, or null if a layer with the same name already exists.</returns>
        public OpenCADLayer? CreateLayer(string name, Color? color = null, LineType? lineType = null, LineWeight? lineWeight = null)
        {
            var layer = new OpenCADLayer(
                name,
                color ?? Color.White,
                lineType ?? LineType.Continuous,
                lineWeight ?? LineWeight.Default,
                this
            );

            if (AddLayer(layer))
                return layer;

            return null;
        }

        /// <summary>
        /// Gets a layer by name.
        /// </summary>
        /// <param name="name">The name of the layer to retrieve.</param>
        /// <returns>The layer with the specified name, or null if not found.</returns>
        public OpenCADLayer? GetLayer(string name)
        {
            if (_layerNameToId.TryGetValue(name, out var layerId))
            {
                var layersContainer = GetLayersContainer();
                if (layersContainer != null)
                {
                    var layer = layersContainer.GetChild(layerId);
                    return layer as OpenCADLayer;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a layer by name.
        /// </summary>
        /// <param name="layerId">The ID of the layer to retrieve.</param>
        /// <returns>The layer with the specified ID, or null if not found.</returns>
        public OpenCADLayer? GetLayer(Guid layerId)
        {
            var layersContainer = GetLayersContainer();
            if (layersContainer != null)
            {
                var layer = layersContainer.GetChild(layerId);
                return layer as OpenCADLayer;
            }
            return null;
        }

        /// <summary>
        /// Removes a layer from the document.
        /// Layer "0" cannot be removed.
        /// </summary>
        /// <param name="name">The name of the layer to remove.</param>
        /// <returns>True if the layer was removed successfully, false otherwise.</returns>
        public bool RemoveLayer(string name)
        {
            if (name == OpenCADStrings.DefaultLayerName)
                return false; // Cannot remove default layer

            if (_layerNameToId.TryGetValue(name, out var layerId))
            {
                var layersContainer = GetLayersContainer();
                if (layersContainer != null)
                {
                    if (layersContainer.Remove(layerId))
                    {
                        _layerNameToId.TryRemove(name, out _);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all layers in the document.
        /// </summary>
        public IEnumerable<OpenCADLayer> GetLayers()
        {
            var layersContainer = GetLayersContainer();
            if (layersContainer != null)
            {
                return layersContainer.GetChildren().OfType<OpenCADLayer>();
            }
            return Enumerable.Empty<OpenCADLayer>();
        }

        /// <summary>
        /// Sets the current layer by name.
        /// </summary>
        /// <param name="name">The name of the layer to set as current.</param>
        /// <returns>True if the layer was found and set as current, false otherwise.</returns>
        public bool SetCurrentLayer(string name)
        {
            var layer = GetLayer(name);
            if (layer != null)
            {
                CurrentLayer = layer;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Applies the document's current properties to a new object.
        /// </summary>
        /// <param name="obj">The object to apply properties to.</param>
        public void ApplyCurrentProperties(IDrawable obj)
        {
            if (obj == null)
                return;

            obj.Layer = CurrentLayer;
            obj.Color = CurrentColor;
            obj.LineType = CurrentLineType ?? LineType.ByLayer;
            obj.LineWeight = CurrentLineWeight ?? LineWeight.ByLayer;
        }

        [JsonIgnore, XmlIgnore]
        public bool HasUnsavedChanges { get; set; }

        [JsonIgnore, XmlIgnore]
        public Guid LayersContainerID 
        { 
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(LayersContainerID));
            private set => SetPropertyValue(PropertyType.ID, nameof(LayersContainerID), OpenCADStrings.LayersContainerID, value);
        }

        [JsonIgnore, XmlIgnore]
        public Guid TextStylesContainerID
        {
            get => GetPropertyValue<Guid>(PropertyType.ID, nameof(TextStylesContainerID));
            private set => SetPropertyValue(PropertyType.ID, nameof(TextStylesContainerID), OpenCADStrings.TextStylesContainerID, value);
        }

        [JsonIgnore, XmlIgnore]
        public IServiceProvider? ServiceProvider
        {
            get => _serviceProvider;
            set => _serviceProvider = value;
        }

        /// <summary>
        /// Thread-safe service resolution.
        /// </summary>
        public T? GetService<T>() where T : class
        {
            var provider = _serviceProvider; // Read once
            if (provider == null)
                return null;

            try
            {
                return provider.GetService(typeof(T)) as T;
            }
            catch (ObjectDisposedException)
            {
                // Service provider might be disposed during shutdown
                return null;
            }
        }

        // Call this whenever the document is modified
        public void MarkAsModified()
        {
            HasUnsavedChanges = true;
        }
        
        // Call this after saving
        public void MarkAsSaved()
        {
            HasUnsavedChanges = false;
        }

        /// <summary>
        /// Initializes the document after deserialization.
        /// MUST be called after loading from JSON!
        /// </summary>
        public void InitializeAfterDeserialization()
        {
            _document = this;

            // Restore parent/document recursively
            RestoreReferences(this, this);

            // Rebuild layer cache
            _layerNameToId = new ConcurrentDictionary<string, Guid>();
            foreach (var layer in GetLayers())
            {
                layer.Document = this;
                if (!string.IsNullOrEmpty(layer.Name))
                    _layerNameToId.TryAdd(layer.Name, layer.ID);
            }
        }

        /// <summary>
        /// Recursively ensures all children have been deserialized properly.
        /// This manually triggers the conversion of SerializedProperties to properties.
        /// </summary>
        //private void EnsureChildrenDeserialized(OpenCADObject parent)
        //{
        //    foreach (var child in parent.GetChildren())
        //    {
        //        //System.Diagnostics.Debug.WriteLine($"  Deserializing child: Type={child.GetType().Name}, ID={child.ID}");
        
        //        // Force the child to complete its deserialization
        //        // by manually calling the conversion that OnDeserialized should do
        //        child.CompleteDeserialization();
        
        //        // Recursively process grandchildren
        //        EnsureChildrenDeserialized(child);
        //    }
        //}

        private OpenCADObject? GetLayersContainer()
        {
            return GetChild(LayersContainerID);
        }

        public ViewportSettings? GetViewportSettings()
        {
            if (CurrentViewportSettingsID.HasValue)
            {
                return GetChild(CurrentViewportSettingsID.Value) as ViewportSettings;
            }
            return null;
        }

        public string ValueToString(double value, UnitFormatType type)
        {
            var unitSettings = GetViewportSettings()?.Unit;
            if (unitSettings != null)
            {
                return type switch
                {
                    UnitFormatType.Linear => unitSettings.LengthToString(value),
                    UnitFormatType.Angular => unitSettings.AngleToString(value),
                    _ => value.ToString()
                };
            }
            return value.ToString();
        }

        public double StringToValue(string str, UnitFormatType type)
        {
            var unitSettings = GetViewportSettings()?.Unit;
            if (unitSettings != null)
            {
                return type switch
                {
                    UnitFormatType.Linear => unitSettings.StringToLength(str),
                    UnitFormatType.Angular => unitSettings.StringToAngle(str),
                    _ => double.Parse(str)
                };
            }
            return double.Parse(str);
        }

        public string VectorToString(Vector3D vector)
        {
            if (vector is null)
            {
                return OpenCADStrings.NullValue;
            }

            var unitSettings = GetViewportSettings()?.Unit;
            if (unitSettings != null)
            {
                var xStr = unitSettings.LengthToString(vector.X);
                var yStr = unitSettings.LengthToString(vector.Y);
                var zStr = unitSettings.LengthToString(vector.Z);
                return $"X:{xStr}, Y:{yStr}, Z:{zStr}";
            }

            return vector.ToString();
        }

        public Vector3D StringToVector(string str)
        {
            return Vector3D.ParseFromPropertyString(str);
        }

        public string PointToString(Point3D point)
        {
            if (point is null)
            {
                return OpenCADStrings.NullValue;
            }

            var unitSettings = GetViewportSettings()?.Unit;
            if (unitSettings != null)
            {
                return VectorToString(point.AsVector3D());
            }

            return point.ToString();
        }

        public Point3D StringToPoint(string str)
        {
            return Point3D.ParseFromPropertyString(str);
        }

        public string ColorToString(Color value)
        {
            return $"A{value.A} R{value.R} G{value.G} B{value.B}";
        }

        public Color StringToColor(string strValue)
        {
            if (string.IsNullOrWhiteSpace(strValue))
                throw new ArgumentNullException(nameof(strValue));

            if (strValue.Equals(OpenCADStrings.ByLayer, StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(0, 0, 0, 0); // ByLayer

            var components = strValue.Split(' ');
            if (components.Length != 4)
                throw new FormatException("Invalid color format. Expected format: \"A{alpha} R{red} G{green} B{blue}\"");

            try
            {
                byte a = byte.Parse(components[0].Substring(1));
                byte r = byte.Parse(components[1].Substring(1));
                byte g = byte.Parse(components[2].Substring(1));
                byte b = byte.Parse(components[3].Substring(1));
                return Color.FromArgb(a, r, g, b);
            }
            catch (Exception ex)
            {
                throw new FormatException("Invalid color format.", ex);
            }
        }
    }
}