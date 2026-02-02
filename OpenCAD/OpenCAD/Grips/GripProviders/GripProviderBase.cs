using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips.GripProviders
{
    public class GripProviderBase
    {
        public Vector2 Point3DToVector2(Point3D pt) 
        {
            return new Vector2((float)pt.X, (float)pt.Y);
        }

        public Point3D Vector2ToPoint3D(Vector2 v, double z) 
        {
            return new Point3D(v.X, v.Y, z);
        }
    }
}
