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
            foreach (var searchPath in _fontSearchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                // Get all TTF files in the directory
                var allFontFiles = Directory.GetFiles(searchPath, "*.ttf", SearchOption.TopDirectoryOnly);

                // Normalize the font family name for comparison (remove spaces, lowercase)
                string normalizedFamily = fontFamily.Replace(" ", "").ToLowerInvariant();

                // Try to find a matching font file
                foreach (var filePath in allFontFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string normalizedFileName = fileName.Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();

                    // Check if this file matches the font family
                    if (!normalizedFileName.Contains(normalizedFamily))
                        continue;

                    // Check for style variants
                    bool fileIsBold = normalizedFileName.Contains("bold") || normalizedFileName.Contains("bd");
                    bool fileIsItalic = normalizedFileName.Contains("italic") || normalizedFileName.Contains("oblique") || normalizedFileName.Contains("it");

                    // For regular fonts, prefer files without Bold/Italic suffixes
                    if (!bold && !italic)
                    {
                        // Skip files with bold or italic in the name
                        if (fileIsBold || fileIsItalic)
                            continue;

                        // Exact match found for regular variant
                        return filePath;
                    }

                    // Match bold and italic requirements
                    if (bold == fileIsBold && italic == fileIsItalic)
                    {
                        return filePath;
                    }
                }

                // Fallback: if no exact match, return any file matching the family name
                foreach (var filePath in allFontFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string normalizedFileName = fileName.Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();

                    if (normalizedFileName.Contains(normalizedFamily))
                    {
                        System.Diagnostics.Debug.WriteLine($"[FontRegistry] Using fallback font file: {filePath} for {fontFamily} (Bold: {bold}, Italic: {italic})");
                        return filePath;
                    }
                }
            }

            return null;
        }
    }
}