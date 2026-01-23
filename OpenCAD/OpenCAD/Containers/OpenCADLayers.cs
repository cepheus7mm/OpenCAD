using OpenCAD.Styles.LineTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenCAD.Containers
{
    /// <summary>
    /// Specialized container for managing layers in an OpenCAD document.
    /// Provides layer-specific operations like add, remove, and lookup by name.
    /// </summary>
    public class OpenCADLayers : OpenCADObject
    {
        // Cache for quick layer lookup by name
        private readonly ConcurrentDictionary<string, Guid> _layerNameToId = new();

        public OpenCADLayers(OpenCADDocument? document = null) : base(document)
        {
            Name = OpenCADStrings.LayersContainer;
        }

        /// <summary>
        /// Adds a new layer to the container.
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

            if (Add(layer))
            {
                _layerNameToId.TryAdd(layer.Name, layer.ID);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates and adds a new layer to the container.
        /// </summary>
        /// <param name="name">The name of the new layer.</param>
        /// <param name="color">The default color for the layer.</param>
        /// <param name="lineType">The default line type for the layer.</param>
        /// <param name="lineWeight">The default line weight for the layer.</param>
        /// <returns>The newly created layer, or null if a layer with the same name already exists.</returns>
        public OpenCADLayer? CreateLayer(string name, Color? color = null, uint? lineType = null, LineWeight? lineWeight = null)
        {
            var layer = new OpenCADLayer(
                name,
                color ?? Color.White,
                lineType ?? OpenCADDocument.ContinuousLineTypeID,
                lineWeight ?? LineWeight.Default,
                _document!
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
                var layer = GetChild(layerId);
                return layer as OpenCADLayer;
            }
            return null;
        }

        /// <summary>
        /// Gets a layer by ID.
        /// </summary>
        /// <param name="layerId">The ID of the layer to retrieve.</param>
        /// <returns>The layer with the specified ID, or null if not found.</returns>
        public OpenCADLayer? GetLayer(Guid layerId)
        {
            var layer = GetChild(layerId);
            return layer as OpenCADLayer;
        }

        /// <summary>
        /// Removes a layer from the container.
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
                if (Remove(layerId))
                {
                    _layerNameToId.TryRemove(name, out _);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all layers in the container.
        /// </summary>
        public IEnumerable<OpenCADLayer> GetLayers()
        {
            return GetChildren().OfType<OpenCADLayer>();
        }

        /// <summary>
        /// Rebuilds the layer name-to-ID cache.
        /// Call this after deserialization or when layer names may have changed.
        /// </summary>
        public void RebuildCache()
        {
            _layerNameToId.Clear();
            foreach (var layer in GetLayers())
            {
                if (!string.IsNullOrEmpty(layer.Name))
                    _layerNameToId.TryAdd(layer.Name, layer.ID);
            }
        }
    }
}