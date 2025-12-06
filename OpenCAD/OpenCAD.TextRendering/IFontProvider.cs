using System.Collections.Generic;

namespace OpenCAD.TextRendering
{
    /// <summary>
    /// Exposes font lookup and registry cache management operations.
    /// Implementations provide FontParser instances for requested families/styles
    /// and control over registry search paths and cache lifecycle.
    /// </summary>
    public interface IFontProvider
    {
        /// <summary>
        /// Get (and load if necessary) a font parser for the requested family and style.
        /// Throws FileNotFoundException if no matching font file can be found.
        /// </summary>
        FontParser GetFont(string fontFamily, bool bold = false, bool italic = false);

        /// <summary>
        /// Try to get a font parser. Returns false if the font cannot be found or fails to load.
        /// </summary>
        bool TryGetFont(string fontFamily, out FontParser? parser, bool bold = false, bool italic = false);

        /// <summary>
        /// Add a custom search path to locate font files.
        /// </summary>
        void AddFontPath(string path);

        /// <summary>
        /// Ensure a font is loaded into the registry cache (useful for warming).
        /// </summary>
        void PreloadFont(string fontFamily, bool bold = false, bool italic = false);

        /// <summary>
        /// Clear the provider's registry cache. Subsequent GetFont calls will recreate and reload fonts.
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Returns the list of font keys that this provider has loaded (family + style).
        /// Keys use the same internal key format: "{family}_{bold}_{italic}".
        /// </summary>
        IReadOnlyList<string> GetLoadedFonts();
    }
}