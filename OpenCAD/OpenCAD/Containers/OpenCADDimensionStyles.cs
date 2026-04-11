using OpenCAD.Dimensions;
using OpenCAD.Styles;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAD.Containers
{
    /// <summary>
    /// Specialized container for managing dimension styles in an OpenCAD document.
    /// Provides dimension style-specific operations like add, remove, and lookup by name.
    /// </summary>
    public class OpenCADDimensionStyles : OpenCADObject
    {
        // Cache for quick dimension style lookup by name
        private readonly ConcurrentDictionary<string, Guid> _dimStyleNameToId = new();

        public OpenCADDimensionStyles(OpenCADDocument? document = null) : base(document)
        {
            Name = OpenCADStrings.DimensionStylesContainer;
        }

        /// <summary>
        /// Adds a new dimension style to the container.
        /// </summary>
        /// <param name="dimStyle">The dimension style to add.</param>
        /// <returns>True if the dimension style was added successfully, false if a dimension style with the same name already exists.</returns>
        public bool AddDimensionStyle(OpenCADDimensionStyle dimStyle)
        {
            if (dimStyle == null)
                throw new ArgumentNullException(nameof(dimStyle));

            // Check if dimension style name already exists
            if (_dimStyleNameToId.ContainsKey(dimStyle.Name))
                return false;

            if (Add(dimStyle))
            {
                _dimStyleNameToId.TryAdd(dimStyle.Name, dimStyle.ID);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates and adds a new dimension style to the container.
        /// </summary>
        /// <param name="name">The name of the new dimension style.</param>
        /// <param name="arrow">The arrow type for dimension lines.</param>
        /// <param name="arrowSize">The size of the arrows.</param>
        /// <param name="textStyleId">The ID of the text style for dimension text.</param>
        /// <param name="textHeight">The height of the dimension text.</param>
        /// <param name="offset">The offset distance from the measured points.</param>
        /// <param name="extensionLength">The length of extension lines.</param>
        /// <param name="extensionOffset">The offset of extension lines from the measured points.</param>
        /// <param name="extensionBeyond">The distance extension lines extend beyond the dimension line.</param>
        /// <param name="scale">The scale factor for the dimension style.</param>
        /// <returns>The newly created dimension style, or null if a dimension style with the same name already exists.</returns>
        public OpenCADDimensionStyle? CreateDimensionStyle(
            string name,
            ArrowType arrow = ArrowType.ClosedFilled,
            float arrowSize = 0.1f,
            Guid? textStyleId = null,
            float textHeight = 0.1f,
            float offset = 0.1f,
            float extensionLength = 0.1f,
            float extensionOffset = 0.1f,
            float extensionBeyond = 0.1f,
            float scale = 10.0f)
        {
            var dimStyle = new OpenCADDimensionStyle(
                name,
                arrow,
                arrowSize,
                textStyleId ?? Guid.Empty,
                textHeight,
                offset,
                extensionLength,
                extensionOffset,
                extensionBeyond,
                scale,
                _document!
            );

            if (AddDimensionStyle(dimStyle))
                return dimStyle;

            return null;
        }

        /// <summary>
        /// Gets a dimension style by name.
        /// </summary>
        /// <param name="name">The name of the dimension style to retrieve.</param>
        /// <returns>The dimension style with the specified name, or null if not found.</returns>
        public OpenCADDimensionStyle? GetDimensionStyle(string name)
        {
            if (_dimStyleNameToId.TryGetValue(name, out var dimStyleId))
            {
                var dimStyle = GetChild(dimStyleId);
                return dimStyle as OpenCADDimensionStyle;
            }
            return null;
        }

        /// <summary>
        /// Gets a dimension style by ID.
        /// </summary>
        /// <param name="dimStyleId">The ID of the dimension style to retrieve.</param>
        /// <returns>The dimension style with the specified ID, or null if not found.</returns>
        public OpenCADDimensionStyle? GetDimensionStyle(Guid dimStyleId)
        {
            var dimStyle = GetChild(dimStyleId);
            return dimStyle as OpenCADDimensionStyle;
        }

        /// <summary>
        /// Removes a dimension style from the container.
        /// The default dimension style cannot be removed.
        /// </summary>
        /// <param name="name">The name of the dimension style to remove.</param>
        /// <returns>True if the dimension style was removed successfully, false otherwise.</returns>
        public bool RemoveDimensionStyle(string name)
        {
            if (name == OpenCADStrings.DefaultDimensionStyleName)
                return false; // Cannot remove default dimension style

            if (_dimStyleNameToId.TryGetValue(name, out var dimStyleId))
            {
                if (Remove(dimStyleId))
                {
                    _dimStyleNameToId.TryRemove(name, out _);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all dimension styles in the container.
        /// </summary>
        public IEnumerable<OpenCADDimensionStyle> GetDimensionStyles()
        {
            return GetChildren().OfType<OpenCADDimensionStyle>();
        }

        /// <summary>
        /// Rebuilds the dimension style name-to-ID cache.
        /// Call this after deserialization or when dimension style names may have changed.
        /// </summary>
        public void RebuildCache()
        {
            _dimStyleNameToId.Clear();
            foreach (var dimStyle in GetDimensionStyles())
            {
                if (!string.IsNullOrEmpty(dimStyle.Name))
                    _dimStyleNameToId.TryAdd(dimStyle.Name, dimStyle.ID);
            }
        }

        internal OpenCADDimensionStyle GetDefaultDimStyle()
        {
            var existing = GetDimensionStyle(OpenCADStrings.DefaultDimensionStyleName);
            if (existing != null)
                return existing;

            return CreateDimensionStyle(OpenCADStrings.DefaultDimensionStyleName)!;
        }
    }
}