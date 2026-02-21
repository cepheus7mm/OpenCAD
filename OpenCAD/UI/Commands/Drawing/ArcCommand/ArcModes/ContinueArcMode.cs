using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Drawing.ArcCommand.ArcModes
{
    public class ContinueArcMode : BaseArcMode
    {
        private readonly ICurve _previousCurve;

        public ContinueArcMode(OpenCADDocument doc) : base(doc)
        {
            _previousCurve = doc.GetLastGeometricChild()!;
            Start = _previousCurve.EndPoint;
        }

        public override string Prompt =>
            Second.IsNotAPoint ? "Specify second point" :
                                 "Specify end point";

        public override Point3D GetBasePoint()
        {
            if (Second.IsNotAPoint)
                return Start;
            else
                return Second;
        }

        public override UserInputType UserInputType => UserInputType.Point;

        public override bool IsComplete =>
            !Start.IsNotAPoint && !Second.IsNotAPoint && !Third.IsNotAPoint;

        public override Arc GetPreview(Point3D cursor)
        {
            return FromThreePoints(Start, Second, cursor);
        }

        public override Arc CreateArc()
        {
            return FromThreePoints(Start, Second, Third);
        }
    }
}
