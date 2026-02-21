using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Drawing.ArcCommand.ArcModes
{
    public class StartEndRadiusMode : BaseArcMode
    {
        public override string Prompt =>
            Start.IsNotAPoint ? "Specify start point" :
            Second.IsNotAPoint ? "Specify end point" :
                                 "Specify radius";

        public override Point3D GetBasePoint()
        {
            if (Second.IsNotAPoint)
                return Start;
            else
                return Second;
        }

        public override UserInputType UserInputType => Start.IsNotAPoint || Second.IsNotAPoint ? UserInputType.Point : UserInputType.Distance;

        public override bool IsComplete => !Start.IsNotAPoint && !Second.IsNotAPoint && !double.IsNaN(Number);

        public override Arc GetPreview(Point3D cursor)
        {
            if (Start.IsNotAPoint || Second.IsNotAPoint)
                return Arc.Empty;

            double r = Start.DistanceTo(cursor);
            return FromStartEndRadius(cursor);
        }

        public override Arc CreateArc()
        {
            double r = Start.DistanceTo(Third);
            return FromStartEndRadius(null);
        }

        public StartEndRadiusMode(OpenCADDocument doc) : base(doc) { }
    }
}
