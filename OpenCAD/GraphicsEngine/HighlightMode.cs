using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphicsEngine
{
    public enum HighlightMode
    {
        None = 0,

        // Hover-only glow (soft, subtle)
        Hover = 1,

        // Selected objects (stronger glow + core color override)
        Selected = 2,

        // Optional: both hover and selected at once
        HoverSelected = 3
    }
}
