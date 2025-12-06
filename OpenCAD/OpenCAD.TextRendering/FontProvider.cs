using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAD.TextRendering
{
    /// <summary>
    /// Thread-safe provider that wraps an internal FontRegistry and exposes
    /// a small API surface for loading, preloading and clearing the font cache.
    /// The provider keeps track of custom search paths so ClearCache recreates
    /// registry state with the same search paths.
    /// </summary>
    public class FontProvider : IFontProvider
    {
        private FontRegistry _registry;
        private readonly HashSet<string> _loadedKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _customFontPaths = new();
        private readonly object _sync = new();

        /// <summary>
        /// Create a new provider with a fresh internal FontRegistry.
        /// </summary>
        public FontProvider()
        {
            _registry = new FontRegistry();
        }

        /// <summary>
        /// Create a provider with an injected registry (useful for tests).
        /// </summary>
        public FontProvider(FontRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <inheritdoc />
        public FontParser GetFont(string fontFamily, bool bold = false, bool italic = false)
        {
            if (string.IsNullOrWhiteSpace(fontFamily))
                throw new ArgumentException("Font family must be provided.", nameof(fontFamily));

            string key = MakeKey(fontFamily, bold, italic);

            lock (_sync)
            {
                // Delegate to registry which performs caching internally
                var parser = _registry.GetFont(fontFamily, bold, italic);

                // Track loaded keys locally so we can report and clear them
                _loadedKeys.Add(key);

                return parser;
            }
        }

        /// <inheritdoc />
        public bool TryGetFont(string fontFamily, out FontParser? parser, bool bold = false, bool italic = false)
        {
            try
            {
                parser = GetFont(fontFamily, bold, italic);
                return true;
            }
            catch
            {
                parser = null;
                return false;
            }
        }

        /// <inheritdoc />
        public void AddFontPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must be provided.", nameof(path));

            lock (_sync)
            {
                if (!_customFontPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    _customFontPaths.Add(path);
                    _registry.AddFontPath(path);
                }
            }
        }

        /// <inheritdoc />
        public void PreloadFont(string fontFamily, bool bold = false, bool italic = false)
        {
            // Simply call GetFont to force load
            GetFont(fontFamily, bold, italic);
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            lock (_sync)
            {
                // Replace registry with a fresh instance to drop internal caches
                _registry = new FontRegistry();

                // Re-apply custom search paths so behaviour is preserved
                foreach (var p in _customFontPaths)
                {
                    _registry.AddFontPath(p);
                }

                _loadedKeys.Clear();
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetLoadedFonts()
        {
            lock (_sync)
            {
                // Return a snapshot of loaded keys
                return _loadedKeys.ToList().AsReadOnly();
            }
        }

        private static string MakeKey(string family, bool bold, bool italic)
        {
            return $"{family}_{bold}_{italic}";
        }
    }
}