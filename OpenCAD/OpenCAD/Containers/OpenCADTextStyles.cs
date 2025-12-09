using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAD.Containers
{
    /// <summary>
    /// Specialized container for managing text styles in an OpenCAD document.
    /// Provides text style-specific operations like add, remove, and lookup by name.
    /// </summary>
    public class OpenCADTextStyles : OpenCADObject
    {
        // Cache for quick text style lookup by name
        private readonly ConcurrentDictionary<string, Guid> _textStyleNameToId = new();

        public OpenCADTextStyles(OpenCADDocument? document = null) : base(document)
        {
            Name = OpenCADStrings.TextStylesContainer;
        }

        /// <summary>
        /// Adds a new text style to the container.
        /// </summary>
        /// <param name="textStyle">The text style to add.</param>
        /// <returns>True if the text style was added successfully, false if a text style with the same name already exists.</returns>
        public bool AddTextStyle(OpenCADTextStyle textStyle)
        {
            if (textStyle == null)
                throw new ArgumentNullException(nameof(textStyle));

            // Check if text style name already exists
            if (_textStyleNameToId.ContainsKey(textStyle.Name))
                return false;

            if (Add(textStyle))
            {
                _textStyleNameToId.TryAdd(textStyle.Name, textStyle.ID);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates and adds a new text style to the container.
        /// </summary>
        /// <param name="name">The name of the new text style.</param>
        /// <param name="fontFamily">The font family for the text style.</param>
        /// <param name="fontSize">The font size for the text style.</param>
        /// <returns>The newly created text style, or null if a text style with the same name already exists.</returns>
        public OpenCADTextStyle? CreateTextStyle(string name, string fontFamily, double fontSize)
        {
            var textStyle = new OpenCADTextStyle(
                name,
                fontFamily,
                fontSize,
                _document as OpenCADDocument
            );

            if (AddTextStyle(textStyle))
                return textStyle;

            return null;
        }

        /// <summary>
        /// Gets a text style by name.
        /// </summary>
        /// <param name="name">The name of the text style to retrieve.</param>
        /// <returns>The text style with the specified name, or null if not found.</returns>
        public OpenCADTextStyle? GetTextStyle(string name)
        {
            if (_textStyleNameToId.TryGetValue(name, out var textStyleId))
            {
                var textStyle = GetChild(textStyleId);
                return textStyle as OpenCADTextStyle;
            }
            return null;
        }

        /// <summary>
        /// Gets a text style by ID.
        /// </summary>
        /// <param name="textStyleId">The ID of the text style to retrieve.</param>
        /// <returns>The text style with the specified ID, or null if not found.</returns>
        public OpenCADTextStyle? GetTextStyle(Guid textStyleId)
        {
            var textStyle = GetChild(textStyleId);
            return textStyle as OpenCADTextStyle;
        }

        /// <summary>
        /// Removes a text style from the container.
        /// The default text style cannot be removed.
        /// </summary>
        /// <param name="name">The name of the text style to remove.</param>
        /// <returns>True if the text style was removed successfully, false otherwise.</returns>
        public bool RemoveTextStyle(string name)
        {
            if (name == OpenCADStrings.DefaultTextStyleName)
                return false; // Cannot remove default text style

            if (_textStyleNameToId.TryGetValue(name, out var textStyleId))
            {
                if (Remove(textStyleId))
                {
                    _textStyleNameToId.TryRemove(name, out _);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all text styles in the container.
        /// </summary>
        public IEnumerable<OpenCADTextStyle> GetTextStyles()
        {
            return GetChildren().OfType<OpenCADTextStyle>();
        }

        /// <summary>
        /// Rebuilds the text style name-to-ID cache.
        /// Call this after deserialization or when text style names may have changed.
        /// </summary>
        public void RebuildCache()
        {
            _textStyleNameToId.Clear();
            foreach (var textStyle in GetTextStyles())
            {
                if (!string.IsNullOrEmpty(textStyle.Name))
                    _textStyleNameToId.TryAdd(textStyle.Name, textStyle.ID);
            }
        }
    }
}