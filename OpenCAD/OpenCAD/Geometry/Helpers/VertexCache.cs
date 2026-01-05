using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers
{
    public sealed class VertexCache : Cache<Guid>
    {
        private readonly Polyline _polyline;

        public VertexCache(Polyline polyline)
        {
            _polyline = polyline;
        }

        protected override List<Guid> Rebuild()
        {
            return _polyline.BuildOrderedVertices();
        }
    }
}
