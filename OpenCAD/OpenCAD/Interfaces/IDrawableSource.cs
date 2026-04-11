using OpenCAD.Dimensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface IDrawableSource
    {
        public IEnumerable<IDrawable> GenerateGeometry();

    }
}
