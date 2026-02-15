using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips
{
    public enum GripEditMode
    {
        Move = 0,          // Default: drag the grip directly
        Stretch = 1,       // Modify geometry by moving the grip point
        Rotate = 2,        // Rotate entity around active grip
        Scale = 3,         // Scale entity relative to active grip
        Mirror = 4,        // Mirror entity across a line defined by grip + mouse
        Lengthen = 5,      // Extend/shorten lines, arcs, polylines
        Copy = 6,          // Create copies during grip edit
        None = 99          // No active mode (idle)
    }
}
