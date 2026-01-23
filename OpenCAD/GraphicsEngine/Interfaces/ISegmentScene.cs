using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCAD.Interfaces;

namespace GraphicsEngine.Interfaces
{
    public interface ISegmentScene
    {
        IEnumerable<ISegmentSource> GetEntities();
    }
}
