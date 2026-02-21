using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Drawing.ArcCommand.ArcModes
{
    public class StartCenterAngleMode : BaseArcMode
    {
        public override string Prompt =>
            Start.IsNotAPoint ? "Specify start point" :
            Second.IsNotAPoint ? "Specify center point" :
                                 "Specify included angle";
        
        public override Point3D GetBasePoint()
        {
            if (Second.IsNotAPoint)
                return Start;
            else
                return Second;
        }

        public override UserInputType UserInputType => Start.IsNotAPoint || Second.IsNotAPoint ? UserInputType.Point : UserInputType.Angle;

        public override bool IsComplete =>
            !Start.IsNotAPoint && !Second.IsNotAPoint && !double.IsNaN(Number);

        public override Arc GetPreview(Point3D cursor)
        {
            return FromStartCenterIncludedAngle(cursor);
        }

        public override Arc CreateArc()
        {
            return FromStartCenterIncludedAngle(null);
        }

        public StartCenterAngleMode(OpenCADDocument doc) : base(doc) { }
    }
}
