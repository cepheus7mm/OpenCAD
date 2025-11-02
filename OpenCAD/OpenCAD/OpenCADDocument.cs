using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

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
        
        // Cache for quick layer lookup by name
        private ConcurrentDictionary<string, Guid> _layerNameToId = new();

        public OpenCADDocument()
        {
            _isDrawable = false; // Document itself is not drawable
            
            // Initialize string properties with names (filename and description)
            properties.TryAdd((int)PropertyType.String, new Property(PropertyType.String, 
                ("Filename", string.Empty),
                ("Description", string.Empty)
            ));
            
            // Create a container object to hold all layers
            var layersContainer = new OpenCADObject();
            Add(layersContainer);
            
            // Store the layers container ID in properties for quick lookup
            properties.TryAdd((int)PropertyType.Integer, new Property(PropertyType.Integer, "Layers Container ID", layersContainer.ID));
            
            // Create default "0" layer (standard in CAD systems)
            var defaultLayer = new Layer("0", Color.White, LineType.Continuous, LineWeight.Default);
            layersContainer.Add(defaultLayer);
            _layerNameToId.TryAdd(defaultLayer.Name, defaultLayer.ID);
            
            // Store current layer ID in properties with name
            properties.TryAdd((int)PropertyType.Layer, new Property(PropertyType.Layer, "Current Layer ID", defaultLayer.ID));
            
            // Current drawing properties start as null (ByLayer) - don't add to properties until set
        }

        public OpenCADDocument(string filename, string description = "") : this()
        {
            Filename = filename;
            Description = description;
        }

        /// <summary>
        /// Gets the layers container object.
        /// </summary>
        private OpenCADObject? GetLayersContainer()
        {
            if (properties.TryGetValue((int)PropertyType.Integer, out var prop))
            {
                var containerId = (Guid)prop.GetValue(LAYERS_CONTAINER_ID_INDEX);
                return GetChild(containerId);
            }
            return null;
        }

        /// <summary>
        /// Gets or sets the filename of the CAD document.
        /// </summary>
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
                        ("Filename", value ?? string.Empty),
                        ("Description", string.Empty)
                    ));
                }
            }
        }

        /// <summary>
        /// Gets or sets the description of the CAD document.
        /// </summary>
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
                        ("Filename", string.Empty),
                        ("Description", value ?? string.Empty)
                    ));
                }
            }
        }

        /// <summary>
        /// Gets or sets the current active layer for new objects.
        /// </summary>
        public Layer? CurrentLayer
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
                        return layer as Layer;
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
                        new Property(PropertyType.Layer, "Current Layer ID", value.ID),
                        (key, oldValue) => new Property(PropertyType.Layer, "Current Layer ID", value.ID)
                    );
                }
            }
        }

        /// <summary>
        /// Gets or sets the current color for new objects.
        /// If null, new objects will use ByLayer color.
        /// </summary>
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
                        new Property(PropertyType.Color, "Current Color", value.Value),
                        (key, oldValue) => new Property(PropertyType.Color, "Current Color", value.Value)
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
                        new Property(PropertyType.LineType, "Current LineType", value.Value),
                        (key, oldValue) => new Property(PropertyType.LineType, "Current LineType", value.Value)
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
                        new Property(PropertyType.LineWeight, "Current LineWeight", value.Value),
                        (key, oldValue) => new Property(PropertyType.LineWeight, "Current LineWeight", value.Value)
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
        public bool AddLayer(Layer layer)
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
        public Layer? CreateLayer(string name, Color? color = null, LineType? lineType = null, LineWeight? lineWeight = null)
        {
            var layer = new Layer(
                name,
                color ?? Color.White,
                lineType ?? LineType.Continuous,
                lineWeight ?? LineWeight.Default
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
        public Layer? GetLayer(string name)
        {
            if (_layerNameToId.TryGetValue(name, out var layerId))
            {
                var layersContainer = GetLayersContainer();
                if (layersContainer != null)
                {
                    var layer = layersContainer.GetChild(layerId);
                    return layer as Layer;
                }
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
            if (name == "0")
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
        public IEnumerable<Layer> GetLayers()
        {
            var layersContainer = GetLayersContainer();
            if (layersContainer != null)
            {
                return layersContainer.GetChildren().OfType<Layer>();
            }
            return Enumerable.Empty<Layer>();
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
    }
}