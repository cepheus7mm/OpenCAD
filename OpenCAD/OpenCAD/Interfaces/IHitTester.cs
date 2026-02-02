using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface IHitTester
    {
        HitResult HitTest(Point screenPos);

        OpenCADObject? HitTestEntity(Point screenPos, int pickboxSize);

        OpenCADObject? HitTestEntity(Vector2 mouseWorld);

        IEnumerable<OpenCADObject> HitTestEntities(Point screenPos, int pickboxSize);

        HitResult? HitTestGrip(Point screenPos, double gripSizePx);
    }
}
