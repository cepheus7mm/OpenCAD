using OpenCAD.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Dimensions
{
    public sealed class DimensionUnits
    {
        public LinearType Linear { get; }
        public AngularType Angular { get; }
        public int Precision { get; }

        public DimensionUnits(
            LinearType linear,
            AngularType angular,
            int precision)
        {
            Linear = linear;
            Angular = angular;
            Precision = precision;
        }
    }
}
