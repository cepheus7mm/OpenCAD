using OpenCAD.TextRendering.Tables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenCAD.TextRendering
{
    /// <summary>
    /// Parses TrueType and OpenType font files
    /// </summary>
    public class FontParser
    {
        private readonly string _fontPath;
        private readonly Dictionary<int, VectorizedGlyph> _glyphCache = new();

        // Font header tables (loaded upfront)
        private FontHeader? _header;
        private CharacterMap? _cmap;
        private GlyphLocations? _loca;
        private HorizontalMetrics? _hmtx;

        // Table directory - maps table tags to their locations in the file
        private Dictionary<string, TableDirectoryEntry> _tableDirectory = new();

        // Store number of glyphs for hmtx parsing
        private ushort _numGlyphs;
        private ushort _numberOfHMetrics;

        // Ascender and descender for typographic scaling
        private short _ascender;
        private short _descender;
        private ushort _xHeight;
        private ushort _capHeight = 0;

        public FontParser(string fontPath)
        {
            if (!File.Exists(fontPath))
                throw new FileNotFoundException($"Font file not found: {fontPath}");

            _fontPath = fontPath;
        }

        /// <summary>
        /// Initialize the parser by reading font headers
        /// </summary>
        public void Initialize()
        {
            using var stream = File.OpenRead(_fontPath);
            using var reader = new BigEndianBinaryReader(stream);

            // Parse font table directory
            ParseTableDirectory(reader);

            // Parse required tables (order matters!)
            ParseHeadTable(reader); // Must be first - contains unitsPerEm and indexToLocFormat
            ParseMaxpTable(reader); // Must be second - contains numGlyphs
            ParseHheaTable(reader); // Must be third - contains numberOfHMetrics
            ParseCmapTable(reader); // Character to glyph mapping
            ParseLocaTable(reader); // Glyph locations (needs head.indexToLocFormat)
            ParseHmtxTable(reader); // Horizontal metrics (needs hhea.numberOfHMetrics)
            ParseOS2Table(reader);  // Cap height
        }

        /// <summary>
        /// Parse the font table directory which tells us where each table is located
        /// </summary>
        private void ParseTableDirectory(BigEndianBinaryReader reader)
        {
            // Read the offset table (header)
            uint sfntVersion = reader.ReadUInt32();

            // Check if this is a valid TrueType/OpenType font
            // Valid values: 0x00010000 (TrueType), 0x4F54544F ('OTTO' for CFF/OpenType)
            if (sfntVersion != 0x00010000 && sfntVersion != 0x4F54544F)
            {
                throw new InvalidDataException($"Invalid font file format. SFNT version: 0x{sfntVersion:X8}");
            }

            ushort numTables = reader.ReadUInt16();
            ushort searchRange = reader.ReadUInt16();   // (maximum power of 2 <= numTables) * 16
            ushort entrySelector = reader.ReadUInt16(); // log2(maximum power of 2 <= numTables)
            ushort rangeShift = reader.ReadUInt16();    // numTables * 16 - searchRange

            // Read table directory entries
            for (int i = 0; i < numTables; i++)
            {
                // Table tag (4-byte identifier like 'head', 'glyf', etc.)
                byte[] tagBytes = reader.ReadBytes(4);
                string tag = Encoding.ASCII.GetString(tagBytes);

                // Checksum for this table
                uint checksum = reader.ReadUInt32();

                // Offset from beginning of file
                uint offset = reader.ReadUInt32();

                // Length of this table
                uint length = reader.ReadUInt32();

                _tableDirectory[tag] = new TableDirectoryEntry
                {
                    Tag = tag,
                    Checksum = checksum,
                    Offset = offset,
                    Length = length
                };
            }

            // Verify we have the minimum required tables
            string[] requiredTables = { "head", "hhea", "maxp", "cmap", "hmtx", "loca", "glyf" };
            foreach (var requiredTable in requiredTables)
            {
                if (!_tableDirectory.ContainsKey(requiredTable))
                {
                    throw new InvalidDataException($"Required table '{requiredTable}' not found in font file.");
                }
            }
        }

        /// <summary>
        /// Parse the 'head' table - font header
        /// </summary>
        private void ParseHeadTable(BigEndianBinaryReader reader)
        {
            var entry = _tableDirectory["head"];
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

            _header = new FontHeader();

            // Version
            ushort majorVersion = reader.ReadUInt16();
            ushort minorVersion = reader.ReadUInt16();

            // Font revision
            uint fontRevision = reader.ReadUInt32();

            // Checksum adjustment
            uint checksumAdjustment = reader.ReadUInt32();

            // Magic number (should be 0x5F0F3CF5)
            uint magicNumber = reader.ReadUInt32();
            if (magicNumber != 0x5F0F3CF5)
            {
                throw new InvalidDataException($"Invalid magic number in head table: 0x{magicNumber:X8}");
            }

            // Flags
            ushort flags = reader.ReadUInt16();

            // Units per EM (typically 1024, 2048, or 4096)
            _header.UnitsPerEm = reader.ReadUInt16();

            // Created and modified timestamps (skip)
            reader.ReadInt64(); // created
            reader.ReadInt64(); // modified

            // Bounding box
            _header.XMin = reader.ReadInt16();
            _header.YMin = reader.ReadInt16();
            _header.XMax = reader.ReadInt16();
            _header.YMax = reader.ReadInt16();

            // Mac style (skip)
            ushort macStyle = reader.ReadUInt16();

            // Lowest rec PPEM (skip)
            ushort lowestRecPPEM = reader.ReadUInt16();

            // Font direction hint (skip)
            short fontDirectionHint = reader.ReadInt16();

            // Index to loc format (0 = short offsets, 1 = long offsets)
            _header.IndexToLocFormat = reader.ReadInt16();

            // Glyph data format (skip)
            short glyphDataFormat = reader.ReadInt16();
        }

        /// <summary>
        /// Parse the 'maxp' table - maximum profile
        /// </summary>
        private void ParseMaxpTable(BigEndianBinaryReader reader)
        {
            var entry = _tableDirectory["maxp"];
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

            // Version
            uint version = reader.ReadUInt32();

            // Number of glyphs
            _numGlyphs = reader.ReadUInt16();

            // We only need numGlyphs for now, skip the rest
        }

        /// <summary>
        /// Parse the 'hhea' table - horizontal header
        /// </summary>
        private void ParseHheaTable(BigEndianBinaryReader reader)
        {
            var entry = _tableDirectory["hhea"];
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

            // Version
            uint version = reader.ReadUInt32();

            // Ascender, Descender, LineGap (store ascender/descender now)
            short ascender = reader.ReadInt16();
            short descender = reader.ReadInt16();
            short lineGap = reader.ReadInt16();

            // Save ascender/descender for typographic scaling
            _ascender = ascender;
            _descender = descender;

            // advanceWidthMax (skip)
            ushort advanceWidthMax = reader.ReadUInt16();

            // Side bearings and extents (skip)
            reader.ReadInt16(); // minLeftSideBearing
            reader.ReadInt16(); // minRightSideBearing
            reader.ReadInt16(); // xMaxExtent

            // Caret slope (skip)
            reader.ReadInt16(); // caretSlopeRise
            reader.ReadInt16(); // caretSlopeRun
            reader.ReadInt16(); // caretOffset

            // Reserved fields (skip)
            reader.ReadInt16();
            reader.ReadInt16();
            reader.ReadInt16();
            reader.ReadInt16();

            // Metric data format (skip)
            reader.ReadInt16();

            // Number of horizontal metrics
            _numberOfHMetrics = reader.ReadUInt16();
        }

        /// <summary>
        /// Parse the 'cmap' table - character to glyph mapping
        /// </summary>
        private void ParseCmapTable(BigEndianBinaryReader reader)
        {
            var entry = _tableDirectory["cmap"];
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

            long tableStart = reader.BaseStream.Position;

            _cmap = new CharacterMap();

            // Version
            ushort version = reader.ReadUInt16();

            // Number of encoding tables
            ushort numTables = reader.ReadUInt16();

            // Find the best encoding table (prefer Unicode BMP - platform 3, encoding 1)
            uint bestOffset = 0;
            for (int i = 0; i < numTables; i++)
            {
                ushort platformID = reader.ReadUInt16();
                ushort encodingID = reader.ReadUInt16();
                uint offset = reader.ReadUInt32();

                // Platform 3 (Windows), Encoding 1 (Unicode BMP)
                if (platformID == 3 && encodingID == 1)
                {
                    bestOffset = offset;
                    break;
                }
                // Fallback: Platform 0 (Unicode), any encoding
                else if (platformID == 0 && bestOffset == 0)
                {
                    bestOffset = offset;
                }
            }

            if (bestOffset == 0)
            {
                throw new InvalidDataException("No suitable character encoding table found in cmap.");
            }

            // Seek to the selected subtable
            reader.BaseStream.Seek(tableStart + bestOffset, SeekOrigin.Begin);

            // Read subtable format
            ushort format = reader.ReadUInt16();

            // We'll support format 4 (most common for BMP)
            if (format == 4)
            {
                ParseCmapFormat4(reader, _cmap);
            }
            else
            {
                throw new NotSupportedException($"cmap format {format} is not yet supported.");
            }
        }

        /// <summary>
        /// Parse cmap format 4 subtable
        /// </summary>
        private void ParseCmapFormat4(BigEndianBinaryReader reader, CharacterMap cmap)
        {
            // Length and language
            ushort length = reader.ReadUInt16();
            ushort language = reader.ReadUInt16();

            // Segment count
            ushort segCountX2 = reader.ReadUInt16();
            ushort segCount = (ushort)(segCountX2 / 2);

            // Search parameters (skip)
            ushort searchRange = reader.ReadUInt16();
            ushort entrySelector = reader.ReadUInt16();
            ushort rangeShift = reader.ReadUInt16();

            // Read segment arrays
            ushort[] endCode = new ushort[segCount];
            for (int i = 0; i < segCount; i++)
                endCode[i] = reader.ReadUInt16();

            ushort reservedPad = reader.ReadUInt16(); // Must be 0

            ushort[] startCode = new ushort[segCount];
            for (int i = 0; i < segCount; i++)
                startCode[i] = reader.ReadUInt16();

            short[] idDelta = new short[segCount];
            for (int i = 0; i < segCount; i++)
                idDelta[i] = reader.ReadInt16();

            ushort[] idRangeOffset = new ushort[segCount];
            long idRangeOffsetPosition = reader.BaseStream.Position;
            for (int i = 0; i < segCount; i++)
                idRangeOffset[i] = reader.ReadUInt16();

            // Build character to glyph index mapping
            for (int i = 0; i < segCount; i++)
            {
                for (int c = startCode[i]; c <= endCode[i]; c++)
                {
                    int glyphIndex;

                    if (idRangeOffset[i] == 0)
                    {
                        // Simple case: glyph index = character code + idDelta
                        glyphIndex = (c + idDelta[i]) & 0xFFFF;
                    }
                    else
                    {
                        // Complex case: indirect lookup
                        long offset = idRangeOffsetPosition + i * 2 + idRangeOffset[i] + (c - startCode[i]) * 2;
                        long currentPos = reader.BaseStream.Position;
                        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                        ushort glyphIndexOffset = reader.ReadUInt16();
                        reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);

                        if (glyphIndexOffset != 0)
                            glyphIndex = (glyphIndexOffset + idDelta[i]) & 0xFFFF;
                        else
                            glyphIndex = 0;
                    }

                    if (glyphIndex != 0 && c <= char.MaxValue)
                    {
                        cmap.AddMapping((char)c, glyphIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Parse the 'loca' table - glyph locations
        /// </summary>
        private void ParseLocaTable(BigEndianBinaryReader reader)
        {
            if (_header == null)
                throw new InvalidOperationException("head table must be parsed before loca table.");

            var entry = _tableDirectory["loca"];
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

            _loca = new GlyphLocations();

            // Format depends on indexToLocFormat from head table
            if (_header.IndexToLocFormat == 0)
            {
                // Short format: offsets are stored as uint16 / 2
                for (int i = 0; i <= _numGlyphs; i++)
                {
                    ushort offset = reader.ReadUInt16();
                    _loca.AddOffset(offset * 2L); // Multiply by 2 to get actual offset
                }
            }
            else
            {
                // Long format: offsets are stored as uint32
                for (int i = 0; i <= _numGlyphs; i++)
                {
                    uint offset = reader.ReadUInt32();
                    _loca.AddOffset(offset);
                }
            }
        }

        /// <summary>
        /// Parse the 'hmtx' table - horizontal metrics
        /// </summary>
        private void ParseHmtxTable(BigEndianBinaryReader reader)
        {
            var entry = _tableDirectory["hmtx"];
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

            _hmtx = new HorizontalMetrics();

            // Read long horizontal metrics (advance width + LSB)
            for (int i = 0; i < _numberOfHMetrics; i++)
            {
                ushort advanceWidth = reader.ReadUInt16();
                short leftSideBearing = reader.ReadInt16();
                _hmtx.AddMetric(advanceWidth, leftSideBearing);
            }

            // Read remaining left side bearings (if any)
            // These glyphs reuse the last advance width
            ushort lastAdvanceWidth = 0;
            if (_numberOfHMetrics > 0)
            {
                var lastMetric = _hmtx.GetMetric(_numberOfHMetrics - 1);
                lastAdvanceWidth = lastMetric.advanceWidth;
            }

            for (int i = _numberOfHMetrics; i < _numGlyphs; i++)
            {
                short leftSideBearing = reader.ReadInt16();
                _hmtx.AddMetric(lastAdvanceWidth, leftSideBearing);
            }
        }

        private void ParseOS2Table(BigEndianBinaryReader reader)
        {
            var entry = _tableDirectory["OS/2"];
            reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

            ushort version = reader.ReadUInt16();
            short xAvgCharWidth = reader.ReadInt16();
            ushort usWeightClass = reader.ReadUInt16();
            ushort usWidthClass = reader.ReadUInt16();
            ushort fsType = reader.ReadUInt16();

            // Skip subscript/superscript metrics (10–29)
            reader.BaseStream.Seek(entry.Offset + 68, SeekOrigin.Begin);

            ushort usWinAscent = reader.ReadUInt16();
            ushort usWinDescent = reader.ReadUInt16();
            short sTypoAscender = reader.ReadInt16();
            short sTypoDescender = reader.ReadInt16();
            short sTypoLineGap = reader.ReadInt16();

            ushort xHeight = 0;
            ushort capHeight = 0;
            if (version >= 2)
            {
                // Jump to xHeight and capHeight (v2+)
                reader.BaseStream.Seek(entry.Offset + 86, SeekOrigin.Begin);
                xHeight = reader.ReadUInt16();
                capHeight = reader.ReadUInt16();
            }

            // Save values
            //_typoAscender = sTypoAscender;
            //_typoDescender = sTypoDescender;
            //_typoLineGap = sTypoLineGap;
            //_winAscent = usWinAscent;
            //_winDescent = usWinDescent;
            _xHeight = xHeight;
            _capHeight = capHeight;


            _xHeight = xHeight;
            _capHeight = capHeight;
        }


        /// <summary>
        /// Get a vectorized glyph for a character (lazy loaded and cached)
        /// </summary>
        public VectorizedGlyph GetGlyph(char character)
        {
            if (_cmap == null)
                throw new InvalidOperationException("Font not initialized. Call Initialize() first.");

            // Get glyph index from character map
            int glyphIndex = _cmap.GetGlyphIndex(character);

            // Check cache
            if (_glyphCache.TryGetValue(glyphIndex, out var cached))
                return cached;

            // Parse and vectorize the glyph
            var glyph = ParseAndVectorizeGlyph(glyphIndex, character);
            _glyphCache[glyphIndex] = glyph;

            return glyph;
        }

        private VectorizedGlyph ParseAndVectorizeGlyph(int glyphIndex, char character)
        {
            if (_loca == null || _hmtx == null || _header == null)
                throw new InvalidOperationException("Font tables not fully initialized.");

            // Get glyph location from loca table
            long offset = _loca.GetOffset(glyphIndex);
            long length = _loca.GetLength(glyphIndex);

            // Get horizontal metrics
            var metrics = _hmtx.GetMetric(glyphIndex);

            // Create the glyph object
            var glyph = new VectorizedGlyph
            {
                Character = character,
                AdvanceWidth = metrics.advanceWidth,
                LeftSideBearing = metrics.leftSideBearing
            };

            // If length is 0, this is a blank glyph (like space)
            if (length == 0)
            {
                return glyph;
            }

            // Open the font file and seek to glyph data
            using var stream = File.OpenRead(_fontPath);
            using var reader = new BigEndianBinaryReader(stream);

            var glyfEntry = _tableDirectory["glyf"];
            reader.BaseStream.Seek(glyfEntry.Offset + offset, SeekOrigin.Begin);

            // Parse glyph header
            short numberOfContours = reader.ReadInt16();
            short xMin = reader.ReadInt16();
            short yMin = reader.ReadInt16();
            short xMax = reader.ReadInt16();
            short yMax = reader.ReadInt16();

            glyph.Bounds = new GlyphBounds
            {
                XMin = xMin,
                YMin = yMin,
                XMax = xMax,
                YMax = yMax
            };

            // Negative numberOfContours means composite glyph
            if (numberOfContours < 0)
            {
                ParseCompositeGlyph(reader, glyph);
            }
            // Simple glyph - parse contours
            else if (numberOfContours > 0)
            {
                ParseSimpleGlyph(reader, numberOfContours, glyph);
            }

            return glyph;
        }

        /// <summary>
        /// Parse a simple (non-composite) glyph
        /// </summary>
        private void ParseSimpleGlyph(BigEndianBinaryReader reader, short numberOfContours, VectorizedGlyph glyph)
        {
            // Read end points of contours
            ushort[] endPtsOfContours = new ushort[numberOfContours];
            for (int i = 0; i < numberOfContours; i++)
            {
                endPtsOfContours[i] = reader.ReadUInt16();
            }

            // Total number of points
            int numberOfPoints = endPtsOfContours[numberOfContours - 1] + 1;

            // Instruction length (skip instructions)
            ushort instructionLength = reader.ReadUInt16();
            reader.ReadBytes(instructionLength); // Skip instructions

            // Read flags
            byte[] flags = new byte[numberOfPoints];
            for (int i = 0; i < numberOfPoints; i++)
            {
                flags[i] = reader.ReadByte();

                // Flag bit 3: Repeat flag
                if ((flags[i] & 0x08) != 0)
                {
                    byte repeatCount = reader.ReadByte();
                    for (int j = 0; j < repeatCount; j++)
                    {
                        i++;
                        flags[i] = flags[i - 1];
                    }
                }
            }

            // Read X coordinates
            short[] xCoordinates = new short[numberOfPoints];
            short xValue = 0;
            for (int i = 0; i < numberOfPoints; i++)
            {
                byte flag = flags[i];

                if ((flag & 0x02) != 0) // X-Short Vector
                {
                    byte value = reader.ReadByte();
                    if ((flag & 0x10) != 0) // Positive
                        xValue += value;
                    else
                        xValue -= value;
                }
                else if ((flag & 0x10) == 0) // Same X or explicit delta
                {
                    xValue += reader.ReadInt16();
                }
                // else: same as previous (flag & 0x10) != 0 and (flag & 0x02) == 0

                xCoordinates[i] = xValue;
            }

            // Read Y coordinates
            short[] yCoordinates = new short[numberOfPoints];
            short yValue = 0;
            for (int i = 0; i < numberOfPoints; i++)
            {
                byte flag = flags[i];

                if ((flag & 0x04) != 0) // Y-Short Vector
                {
                    byte value = reader.ReadByte();
                    if ((flag & 0x20) != 0) // Positive
                        yValue += value;
                    else
                        yValue -= value;
                }
                else if ((flag & 0x20) == 0) // Same Y or explicit delta
                {
                    yValue += reader.ReadInt16();
                }
                // else: same as previous (flag & 0x20) != 0 and (flag & 0x04) == 0

                yCoordinates[i] = yValue;
            }

            // Build contours
            int startPoint = 0;
            for (int i = 0; i < numberOfContours; i++)
            {
                int endPoint = endPtsOfContours[i];
                var contour = new GlyphContour();

                for (int j = startPoint; j <= endPoint; j++)
                {
                    bool onCurve = (flags[j] & 0x01) != 0;
                    contour.Points.Add(new GlyphPoint(xCoordinates[j], yCoordinates[j], onCurve));
                }

                glyph.Contours.Add(contour);
                startPoint = endPoint + 1;
            }
        }

        /// <summary>
        /// Parse a composite glyph (made up of component glyphs with transformations)
        /// </summary>
        private void ParseCompositeGlyph(BigEndianBinaryReader reader, VectorizedGlyph glyph)
        {
            const ushort ARG_1_AND_2_ARE_WORDS = 0x0001;
            const ushort ARGS_ARE_XY_VALUES = 0x0002;
            const ushort ROUND_XY_TO_GRID = 0x0004;
            const ushort WE_HAVE_A_SCALE = 0x0008;
            const ushort MORE_COMPONENTS = 0x0020;
            const ushort WE_HAVE_AN_X_AND_Y_SCALE = 0x0040;
            const ushort WE_HAVE_A_TWO_BY_TWO = 0x0080;
            const ushort WE_HAVE_INSTRUCTIONS = 0x0100;
            const ushort USE_MY_METRICS = 0x0200;
            const ushort OVERLAP_COMPOUND = 0x0400;

            bool hasMoreComponents = true;

            while (hasMoreComponents)
            {
                ushort flags = reader.ReadUInt16();
                ushort glyphIndexComponent = reader.ReadUInt16();

                // Read transformation parameters
                short arg1, arg2;
                if ((flags & ARG_1_AND_2_ARE_WORDS) != 0)
                {
                    arg1 = reader.ReadInt16();
                    arg2 = reader.ReadInt16();
                }
                else
                {
                    arg1 = reader.ReadSByte();
                    arg2 = reader.ReadSByte();
                }

                // Transformation matrix (default is identity)
                double a = 1.0, b = 0.0, c = 0.0, d = 1.0;
                double m = 0.0, n = 0.0;

                if ((flags & WE_HAVE_A_SCALE) != 0)
                {
                    // Uniform scale
                    a = d = ReadF2Dot14(reader);
                }
                else if ((flags & WE_HAVE_AN_X_AND_Y_SCALE) != 0)
                {
                    // Separate X and Y scales
                    a = ReadF2Dot14(reader);
                    d = ReadF2Dot14(reader);
                }
                else if ((flags & WE_HAVE_A_TWO_BY_TWO) != 0)
                {
                    // Full 2x2 transformation matrix
                    a = ReadF2Dot14(reader);
                    b = ReadF2Dot14(reader);
                    c = ReadF2Dot14(reader);
                    d = ReadF2Dot14(reader);
                }

                // Calculate translation offsets
                if ((flags & ARGS_ARE_XY_VALUES) != 0)
                {
                    // Arguments are X and Y offsets
                    m = arg1;
                    n = arg2;
                }
                else
                {
                    // Arguments are point numbers (not commonly used, skip for now)
                    // Would need to match points from component to parent
                    m = 0;
                    n = 0;
                }

                // Recursively load the component glyph
                var componentGlyph = GetGlyphByIndex(glyphIndexComponent);

                // Transform and add component contours to this glyph
                if (componentGlyph != null)
                {
                    foreach (var componentContour in componentGlyph.Contours)
                    {
                        var transformedContour = new GlyphContour();

                        foreach (var point in componentContour.Points)
                        {
                            // Apply 2x2 transformation matrix + translation
                            double x = a * point.X + c * point.Y + m;
                            double y = b * point.X + d * point.Y + n;

                            transformedContour.Points.Add(new GlyphPoint(x, y, point.OnCurve));
                        }

                        glyph.Contours.Add(transformedContour);
                    }
                }

                // Check if there are more components
                hasMoreComponents = (flags & MORE_COMPONENTS) != 0;
            }
        }

        /// <summary>
        /// Get a glyph by its index (used internally for composite glyphs)
        /// </summary>
        private VectorizedGlyph? GetGlyphByIndex(int glyphIndex)
        {
            // Check cache first
            if (_glyphCache.TryGetValue(glyphIndex, out var cached))
                return cached;

            // Parse the component glyph (use null character since we don't know the char)
            try
            {
                var glyph = ParseAndVectorizeGlyph(glyphIndex, '\0');
                _glyphCache[glyphIndex] = glyph;
                return glyph;
            }
            catch
            {
                // If component glyph fails to parse, return null
                return null;
            }
        }

        /// <summary>
        /// Read a fixed-point 2.14 format number (used in transformation matrices)
        /// </summary>
        private double ReadF2Dot14(BigEndianBinaryReader reader)
        {
            short value = reader.ReadInt16();
            return value / 16384.0; // 2^14 = 16384
        }

        /// <summary>
        /// Represents an entry in the font table directory
        /// </summary>
        private class TableDirectoryEntry
        {
            public string Tag { get; set; } = string.Empty;
            public uint Checksum { get; set; }
            public uint Offset { get; set; }
            public uint Length { get; set; }
        }

        /// <summary>
        /// Gets the font's units per EM value (needed for scaling)
        /// </summary>
        public ushort UnitsPerEm
        {
            get
            {
                if (_header == null)
                    throw new InvalidOperationException("Font not initialized. Call Initialize() first.");
                return _header.UnitsPerEm;
            }
        }

        public ushort XHeight
        {
            get
            {
                return _xHeight;
            }
        }
        
        public ushort CapHeight
        {
            get
            {
                if (_capHeight != 0)
                    return _capHeight;
                // Fallback: approximate cap height as 70% of ascender
                return (ushort)(_ascender * 0.7);
            }
        }

        /// <summary>
        /// Gets the font's ascender (font units)
        /// </summary>
        public short Ascender
        {
            get
            {
                return _ascender;
            }
        }

        /// <summary>
        /// Gets the font's descender (font units)
        /// </summary>
        public short Descender
        {
            get
            {
                return _descender;
            }
        }
    }

    /// <summary>
    /// BinaryReader that reads multi-byte values in big-endian format (as used in TrueType/OpenType fonts)
    /// </summary>
    internal class BigEndianBinaryReader : BinaryReader
    {
        public BigEndianBinaryReader(Stream stream) : base(stream)
        {
        }

        public override short ReadInt16()
        {
            byte[] bytes = ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        public override ushort ReadUInt16()
        {
            byte[] bytes = ReadBytes(2);
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        public override int ReadInt32()
        {
            byte[] bytes = ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public override uint ReadUInt32()
        {
            byte[] bytes = ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        public override long ReadInt64()
        {
            byte[] bytes = ReadBytes(8);
            Array.Reverse(bytes);
            return BitConverter.ToInt64(bytes, 0);
        }

        public override ulong ReadUInt64()
        {
            byte[] bytes = ReadBytes(8);
            Array.Reverse(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }

        /// <summary>
        /// Read a signed byte
        /// </summary>
        public override sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }
    }
}