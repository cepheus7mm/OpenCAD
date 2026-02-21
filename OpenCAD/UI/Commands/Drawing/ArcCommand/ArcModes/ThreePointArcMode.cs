using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Drawing.ArcCommand.ArcModes
{
    public class ThreePointArcMode : BaseArcMode
    {
        public override string Prompt =>
            Start.IsNotAPoint ? "Specify first point" :
            Second.IsNotAPoint ? "Specify second point" :
                                 "Specify third point";

        public override Point3D GetBasePoint()
        {
            if (Second.IsNotAPoint)
                return Start;
            else
                return Second;
        }

        public override UserInputType UserInputType => UserInputType.Point;

        public override bool IsComplete => !Start.IsNotAPoint && !Second.IsNotAPoint && !Third.IsNotAPoint;

        public override Arc GetPreview(Point3D cursor)
        {
            return FromThreePoints(Start, Second, cursor);
        }

        public override Arc CreateArc()
        {
            return FromThreePoints(Start, Second, Third);
        }

        public ThreePointArcMode(OpenCADDocument doc) : base(doc) { }
    }
}
