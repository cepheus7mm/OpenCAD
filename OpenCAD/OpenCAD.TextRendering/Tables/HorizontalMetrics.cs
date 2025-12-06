using System.Collections.Generic;

namespace OpenCAD.TextRendering.Tables
{
    /// <summary>
    /// Horizontal metrics from 'hmtx' table
    /// </summary>
    public class HorizontalMetrics
    {
        private readonly List<(ushort advanceWidth, short leftSideBearing)> _metrics = new();

        public void AddMetric(ushort advanceWidth, short leftSideBearing)
        {
            _metrics.Add((advanceWidth, leftSideBearing));
        }

        public (ushort advanceWidth, short leftSideBearing) GetMetric(int glyphIndex)
        {
            return ((ushort advanceWidth, short leftSideBearing))(glyphIndex < _metrics.Count ? _metrics[glyphIndex] : (0, 0));
        }
    }
}