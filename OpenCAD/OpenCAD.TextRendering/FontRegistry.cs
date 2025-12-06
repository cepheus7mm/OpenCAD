using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAD.TextRendering
{
    /// <summary>
    /// Manages loaded fonts and provides access to font parsers
    /// </summary>
    public class FontRegistry
    {
        private readonly Dictionary<string, FontParser> _loadedFonts = new();
        private readonly List<string> _fontSearchPaths = new();

        public FontRegistry()
        {
            // Add default system font paths
            AddDefaultFontPaths();
        }

        private void AddDefaultFontPaths()
        {
            // Windows fonts
            if (OperatingSystem.IsWindows())
            {
                _fontSearchPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts)));
                _fontSearchPaths.Add(@"C:\Windows\Fonts");
            }

            // TODO: Add Linux and macOS font paths
        }

        /// <summary>
        /// Add a custom font search path
        /// </summary>
        public void AddFontPath(string path)
        {
            if (Directory.Exists(path) && !_fontSearchPaths.Contains(path))
            {
                _fontSearchPaths.Add(path);
            }
        }

        /// <summary>
        /// Get a font parser for the specified font family and style
        /// </summary>
        public FontParser GetFont(string fontFamily, bool bold = false, bool italic = false)
        {
            string key = $"{fontFamily}_{bold}_{italic}";

            if (_loadedFonts.TryGetValue(key, out var parser))
                return parser;

            // Find the font file
            string? fontPath = FindFontFile(fontFamily, bold, italic);
            if (fontPath == null)
                throw new FileNotFoundException($"Font not found: {fontFamily} (Bold: {bold}, Italic: {italic})");

            // Create and initialize parser
            parser = new FontParser(fontPath);
            parser.Initialize();

            _loadedFonts[key] = parser;
            return parser;
        }

        private string? FindFontFile(string fontFamily, bool bold, bool italic)
        {
            // TODO: Implement font file search logic
            // Look for files matching the font family name with appropriate suffixes
            // e.g., "Arial.ttf", "Arial-Bold.ttf", "Arial-BoldItalic.ttf"
            
            foreach (var searchPath in _fontSearchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                // Simple heuristic - improve this later
                string pattern = $"{fontFamily}*.ttf";
                var files = Directory.GetFiles(searchPath, pattern, SearchOption.TopDirectoryOnly);

                if (files.Length > 0)
                    return files[0]; // For now, just return the first match
            }

            return null;
        }
    }
}