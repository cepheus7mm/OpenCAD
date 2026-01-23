using OpenCAD.SegmentSource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface ISegmentSource
    {
        IEnumerable<Segment> GetSegments(float maxSagitta = 0);

        LinetypeGpuData GetLinetypeGpuData();
    }


}
