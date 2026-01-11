using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry
{
    public sealed class Point3DComparer : IEqualityComparer<Point3D>
    {
        public static readonly Point3DComparer Instance = new();

        // Tune this to your engine-wide epsilon policy
        private const double Eps = 1e-9;

        public bool Equals(Point3D a, Point3D b)
        {
            return Math.Abs(a.X - b.X) < Eps
                && Math.Abs(a.Y - b.Y) < Eps
                && Math.Abs(a.Z - b.Z) < Eps;
        }

        public int GetHashCode(Point3D p)
        {
            // Quantize to tolerance grid for stable hashing
            long qx = (long)Math.Round(p.X / Eps);
            long qy = (long)Math.Round(p.Y / Eps);
            long qz = (long)Math.Round(p.Z / Eps);

            return HashCode.Combine(qx, qy, qz);
        }
    }

}
