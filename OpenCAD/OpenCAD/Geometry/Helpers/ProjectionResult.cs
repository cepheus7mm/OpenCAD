using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers
{
    public readonly struct ProjectionResult
    {
        public readonly double Parameter;     // Unclamped
        public readonly Point3D ClosestPoint; // Point on infinite curve
        public readonly double Distance;      // Optional

        public ProjectionResult(double t, Point3D cp, double d)
        {
            Parameter = t;
            ClosestPoint = cp;
            Distance = d;
        }
    }
}
