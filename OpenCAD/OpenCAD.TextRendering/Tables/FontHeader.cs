namespace OpenCAD.TextRendering.Tables
{
    /// <summary>
    /// Font header information from 'head' table
    /// </summary>
    public class FontHeader
    {
        public ushort UnitsPerEm { get; set; }
        public short XMin { get; set; }
        public short YMin { get; set; }
        public short XMax { get; set; }
        public short YMax { get; set; }
        public short IndexToLocFormat { get; set; } // 0 for short offsets, 1 for long
    }
}