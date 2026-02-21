using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Drawing.ArcCommand.ArcModes
{
    public class StartEndDirectionMode : BaseArcMode
    {
        public override string Prompt =>
            Start.IsNotAPoint ? "Specify start point" :
            Second.IsNotAPoint ? "Specify end point" :
                                 "Specify tangent direction";

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

        public override void SetDouble(double value)
        {
            base.SetDouble(value);
            Third = Point3D.Origin; // Just needs to be set to something non-NaN to indicate completion, actual value is computed in CreateArc
        }


        public override Arc GetPreview(Point3D cursor)
        {
            if (Start.IsNotAPoint || Second.IsNotAPoint)
                return Arc.Empty;

            return FromStartEndDirection(cursor);
        }

        public override Arc CreateArc()
        {
            return FromStartEndDirection();
        }

        public StartEndDirectionMode(OpenCADDocument doc) : base(doc) { }
    }
}
