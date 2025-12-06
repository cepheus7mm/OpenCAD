using System.Collections.Generic;

namespace OpenCAD.TextRendering.Tables
{
    /// <summary>
    /// Glyph location information from 'loca' table
    /// </summary>
    public class GlyphLocations
    {
        private readonly List<long> _offsets = new();

        public void AddOffset(long offset)
        {
            _offsets.Add(offset);
        }

        public long GetOffset(int glyphIndex)
        {
            return glyphIndex < _offsets.Count ? _offsets[glyphIndex] : 0;
        }

        public long GetLength(int glyphIndex)
        {
            if (glyphIndex >= _offsets.Count - 1)
                return 0;
            return _offsets[glyphIndex + 1] - _offsets[glyphIndex];
        }
    }
}