using OpenCAD.Settings;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        // Property indices for multiple String properties
        private const int FILENAME_INDEX = 0;
        private const int DESCRIPTION_INDEX = 1;
        
        // Property indices for current drawing properties
        private const int CURRENT_LAYER_ID_INDEX = 0;
        private const int CURRENT_COLOR_INDEX = 0;
        private const int CURRENT_LINETYPE_INDEX = 0;
        private const int CURRENT_LINEWEIGHT_INDEX = 0;
        
        // Property index for layers container ID
        private const int LAYERS_CONTAINER_ID_INDEX = 0;

        // Property index for viewport settings container ID
        private const int VIEWPORT_SETTINGS_CONTAINER_ID_INDEX = 1;

        // Cache for quick layer lookup by name
        [JsonIgnore, XmlIgnore]
        private ConcurrentDictionary<string, Guid> _layerNameToId = new();

        public OpenCADDocument()
        {
            // Initialize string properties with names (filename and description)
            properties.TryAdd((int)PropertyType.String, new Property(PropertyType.String, 
                (OpenCADStrings.Filename, string.Empty),
                (OpenCADStrings.Description, string.Empty)
            ));
            
            // Create a container object to hold all layers
            var layersContainer = new OpenCADObject(this) { Name = OpenCADStrings.LayersContainer };
            Add(layersContainer);
            
            // Create default "0" layer (standard in CAD systems)
            var defaultLayer = new OpenCADLayer(OpenCADStrings.DefaultLayerName, Color.White, LineType.Continuous, LineWeight.Default, this);
            layersContainer.Add(defaultLayer);
            _layerNameToId.TryAdd(defaultLayer.Name, defaultLayer.ID);
            
            // Store current layer ID in properties with name
            properties.TryAdd((int)PropertyType.Layer, new Property(PropertyType.Layer, OpenCADStrings.CurrentLayerID, defaultLayer.ID));

            // Current drawing properties start as null (ByLayer) - don't add to properties until set

            // Add other default settings objects as children if needed
            var viewportSettings = new ViewportSettings(this);
            Add(viewportSettings);

            // Store the layers container ID in properties for quick lookup
            // Store the viewport settings container ID in properties for quick lookup
            properties.TryAdd((int)PropertyType.Integer, new Property(PropertyType.Integer,
                (OpenCADStrings.LayersContainerID, layersContainer.ID),
                (OpenCADStrings.ViewportSettingsContainerID, viewportSettings.ID)
            ));
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
                System.Diagnostics.Debug.WriteLine($"Document initialized: {test4.Count} layers, current: {test3?.Name ?? "null"}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Document NOT initialized: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Called after deserialization to rebuild caches and restore references.
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            System.Diagnostics.Debug.WriteLine("=== OpenCADDocument.OnDeserialized START ===");
            
            try
            {
                // Rebuild the layer name-to-ID cache
                _layerNameToId = new ConcurrentDictionary<string, Guid>();
                
                System.Diagnostics.Debug.WriteLine($"Rebuilding layer cache...");
                
                var layers = GetLayers().ToList();
                System.Diagnostics.Debug.WriteLine($"Found {layers.Count} layers");
                
                foreach (var layer in layers)
                {
                    System.Diagnostics.Debug.WriteLine($"  Layer: {layer.Name} (ID: {layer.ID})");
                    _layerNameToId.TryAdd(layer.Name, layer.ID);
                    
                    // Restore document reference
                    layer.Document = this;
                }
                
                // Recursively restore document and parent references for all children
                System.Diagnostics.Debug.WriteLine("Restoring references...");
                RestoreReferences(this, this);
                
                System.Diagnostics.Debug.WriteLine("=== OpenCADDocument.OnDeserialized COMPLETE ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== OpenCADDocument.OnDeserialized FAILED: {ex.Message} ===");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
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
        /// Gets the layers container object.
        /// </summary>
        private OpenCADObject? GetLayersContainer()
        {
            // First try to get from properties (fast path for normal operation)
            if (properties.TryGetValue((int)PropertyType.Integer, out var prop))
            {
                try
                {
                    var containerId = (Guid)prop.GetValue(LAYERS_CONTAINER_ID_INDEX);
                    var container = GetChild(containerId);
                    if (container != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"GetLayersContainer: Found by ID: {containerId}");
                        return container;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetLayersContainer: Failed to get by ID: {ex.Message}");
                }
            }
            
            // Fallback: Search children by name (important for deserialization!)
            System.Diagnostics.Debug.WriteLine("GetLayersContainer: Searching children by name...");
            var containerByName = GetChildren()
                .FirstOrDefault(c => c.Name == OpenCADStrings.LayersContainer);
            
            if (containerByName != null)
            {
                System.Diagnostics.Debug.WriteLine($"GetLayersContainer: Found by name: {containerByName.Name} (ID: {containerByName.ID})");
                
                // Update the properties cache with the found ID for future lookups
                try
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Integer,
                        new Property(PropertyType.Integer, OpenCADStrings.LayersContainerID, containerByName.ID),
                        (key, oldValue) => new Property(PropertyType.Integer, OpenCADStrings.LayersContainerID, containerByName.ID)
                    );
                    System.Diagnostics.Debug.WriteLine("GetLayersContainer: Updated properties cache with container ID");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetLayersContainer: Failed to update cache: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("GetLayersContainer: NOT FOUND!");
                System.Diagnostics.Debug.WriteLine($"  Total children: {GetChildren().Count()}");
                foreach (var child in GetChildren())
                {
                    System.Diagnostics.Debug.WriteLine($"    Child: Name='{child.Name ?? "null"}', Type={child.GetType().Name}, ID={child.ID}");
                }
            }
            
            return containerByName;
        }

        /// <summary>
        /// Gets the viewport settings container object.
        /// </summary>
        private ViewportSettings? GetViewportSettingsContainer()
        {
            // First try to get from properties (fast path for normal operation)
            if (properties.TryGetValue((int)PropertyType.Integer, out var prop))
            {
                try
                {
                    var containerId = (Guid)prop.GetValue(VIEWPORT_SETTINGS_CONTAINER_ID_INDEX);
                    var container = GetChild(containerId) as ViewportSettings;
                    if (container != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"GetViewportSettingsContainer: Found by ID: {containerId}");
                        return container;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetViewportSettingsContainer: Failed to get by ID: {ex.Message}");
                }
            }

            return null;
        }


        /// <summary>
        /// Gets or sets the filename of the CAD document.
        /// </summary>
        [JsonIgnore] // or [XmlIgnore]
        public string Filename
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.String, out var prop))
                    return (string)prop.GetValue(FILENAME_INDEX);
                return string.Empty;
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.String, out var prop))
                {
                    prop.SetValue(FILENAME_INDEX, value ?? string.Empty);
                }
                else
                {
                    properties.TryAdd((int)PropertyType.String, new Property(PropertyType.String,
                        (OpenCADStrings.Filename, value ?? string.Empty),
                        (OpenCADStrings.Description, string.Empty)
                    ));
                }
            }
        }

        /// <summary>
        /// Gets or sets the description of the CAD document.
        /// </summary>
        [JsonIgnore]
        public string Description
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.String, out var prop))
                    return (string)prop.GetValue(DESCRIPTION_INDEX);
                return string.Empty;
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.String, out var prop))
                {
                    prop.SetValue(DESCRIPTION_INDEX, value ?? string.Empty);
                }
                else
                {
                    properties.TryAdd((int)PropertyType.String, new Property(PropertyType.String,
                        (OpenCADStrings.Filename, string.Empty),
                        (OpenCADStrings.Description, value ?? string.Empty)
                    ));
                }
            }
        }

        /// <summary>
        /// Gets or sets the current active layer for new objects.
        /// </summary>
        [JsonIgnore]
        public OpenCADLayer? CurrentLayer
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Layer, out var prop))
                {
                    var layerId = (Guid)prop.GetValue(CURRENT_LAYER_ID_INDEX);
                    var layersContainer = GetLayersContainer();
                    if (layersContainer != null)
                    {
                        var layer = layersContainer.GetChild(layerId);
                        return layer as OpenCADLayer;
                    }
                }
                return null;
            }
            set
            {
                if (value != null)
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Layer,
                        new Property(PropertyType.Layer, OpenCADStrings.CurrentLayerID, value.ID),
                        (key, oldValue) => new Property(PropertyType.Layer, OpenCADStrings.CurrentLayerID, value.ID)
                    );
                }
            }
        }

        [JsonIgnore]
        public ViewportSettings? CurrentViewportSettings => GetViewportSettingsContainer();

        /// <summary>
        /// Gets or sets the current color for new objects.
        /// If null, new objects will use ByLayer color.
        /// </summary>
        [JsonIgnore]
        public Color? CurrentColor
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Color, out var prop))
                    return (Color)prop.GetValue(CURRENT_COLOR_INDEX);
                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.Color,
                        new Property(PropertyType.Color, OpenCADStrings.CurrentColor, value.Value),
                        (key, oldValue) => new Property(PropertyType.Color, OpenCADStrings.CurrentColor, value.Value)
                    );
                }
                else
                {
                    properties.TryRemove((int)PropertyType.Color, out _);
                }
            }
        }

        /// <summary>
        /// Gets or sets the current line type for new objects.
        /// If null, new objects will use ByLayer line type.
        /// </summary>
        [JsonIgnore]
        public LineType? CurrentLineType
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.LineType, out var prop))
                    return (LineType)prop.GetValue(CURRENT_LINETYPE_INDEX);
                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.LineType,
                        new Property(PropertyType.LineType, OpenCADStrings.CurrentLineType, value.Value),
                        (key, oldValue) => new Property(PropertyType.LineType, OpenCADStrings.CurrentLineType, value.Value)
                    );
                }
                else
                {
                    properties.TryRemove((int)PropertyType.LineType, out _);
                }
            }
        }

        /// <summary>
        /// Gets or sets the current line weight for new objects.
        /// If null, new objects will use ByLayer line weight.
        /// </summary>
        [JsonIgnore]
        public LineWeight? CurrentLineWeight
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.LineWeight, out var prop))
                    return (LineWeight)prop.GetValue(CURRENT_LINEWEIGHT_INDEX);
                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    properties.AddOrUpdate(
                        (int)PropertyType.LineWeight,
                        new Property(PropertyType.LineWeight, OpenCADStrings.CurrentLineWeight, value.Value),
                        (key, oldValue) => new Property(PropertyType.LineWeight, OpenCADStrings.CurrentLineWeight, value.Value)
                    );
                }
                else
                {
                    properties.TryRemove((int)PropertyType.LineWeight, out _);
                }
            }
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
        /// <param name="name">The name of the layer to retrieve.</param>
        /// <returns>The layer with the specified name, or null if not found.</returns>
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
        public void ApplyCurrentProperties(OpenCADObject obj)
        {
            if (obj == null)
                return;

            obj.Layer = CurrentLayer;
            obj.SetColor(CurrentColor);
            obj.SetLineType(CurrentLineType);
            obj.SetLineWeight(CurrentLineWeight);
        }

        [JsonIgnore, XmlIgnore]
        public bool HasUnsavedChanges { get; set; }
        
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
        private void EnsureChildrenDeserialized(OpenCADObject parent)
        {
            foreach (var child in parent.GetChildren())
            {
                System.Diagnostics.Debug.WriteLine($"  Deserializing child: Type={child.GetType().Name}, ID={child.ID}");
        
                // Force the child to complete its deserialization
                // by manually calling the conversion that OnDeserialized should do
                child.CompleteDeserialization();
        
                // Recursively process grandchildren
                EnsureChildrenDeserialized(child);
            }
        }
    }
}