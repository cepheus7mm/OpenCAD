using System.Collections.Generic;

namespace OpenCAD.TextRendering.Tables
{
    /// <summary>
    /// Character to glyph index mapping from 'cmap' table
    /// </summary>
    public class CharacterMap
    {
        private readonly Dictionary<char, int> _charToGlyphIndex = new();

        public void AddMapping(char character, int glyphIndex)
        {
            _charToGlyphIndex[character] = glyphIndex;
        }

        public int GetGlyphIndex(char character)
        {
            return _charToGlyphIndex.TryGetValue(character, out var index) ? index : 0; // 0 = missing glyph
        }
    }
}