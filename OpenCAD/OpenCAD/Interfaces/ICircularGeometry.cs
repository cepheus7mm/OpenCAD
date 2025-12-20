using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface ICircularGeometry
    {
        public double Radius { get; set; }
        public Point3D Center { get; set; }

        }
}
