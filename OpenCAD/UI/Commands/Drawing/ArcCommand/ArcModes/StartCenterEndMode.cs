using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UI.Commands.Drawing.ArcCommand.ArcModes
{
    public class StartCenterEndMode : BaseArcMode
    {
        public override string Prompt =>
            Start.IsNotAPoint ? "Specify start point" :
            Second.IsNotAPoint ? "Specify center point" :
                                 "Specify end point";

        public override string[] Keywords => Start.IsNotAPoint ? ["ANG", "DIR", "RAD", "LEN", "3PT", "CONT"] : Array.Empty<string>();
                                                 
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
            return FromThreePoints(cursor);
        }

        public override Arc CreateArc()
        {
            return FromThreePoints(Third);
        }

        public StartCenterEndMode(OpenCADDocument doc) : base(doc) { }
    }
}
