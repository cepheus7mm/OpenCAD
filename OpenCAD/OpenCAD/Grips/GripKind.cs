using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips
{
    public enum GripKind
    {
        Move,       // e.g., endpoints of a line
        Stretch,    // e.g., polyline vertex
        Rotate,
        Scale,
        Custom
    }
}
