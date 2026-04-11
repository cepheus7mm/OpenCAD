using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    /// <summary>
    /// Implemented by entities (including composites like dimensions) that
    /// define their own hit-testing logic.
    /// </summary>
    public interface IHitTestable
    {
        HitResult HitTest(HitTestContext ctx);
    }
}