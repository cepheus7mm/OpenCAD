using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.SegmentSource
{
    public abstract class CachedSegmentSource
    {
        private Segment[]? _cache;

        public IEnumerable<Segment> GetSegments(float maxSagitta = 0)
        {
            if (_cache == null)
                _cache = BuildSegments().ToArray();

            return _cache;
        }

        protected abstract IEnumerable<Segment> BuildSegments();

        public void Invalidate() => _cache = null;
    }

}
