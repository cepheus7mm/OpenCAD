using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Drawing.PolylineCreation.Modes;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.LineCommand.Modes
{
    public class FirstPointMode : ILineCreationMode
    {
        private Point3D? _start;

        public string Prompt => "Specify start point";
        public string[] Keywords => Array.Empty<string>();
        public bool IsComplete => _start.HasValue;

        public void SetPoint(Point3D p) => _start = p;
        public Point3D GetBasePoint() => Point3D.NotAPoint;

        public IEnumerable<OpenCADObject> GetPreview(Point3D cursor)
        {
            yield break; // no preview for first point
        }

        public ILineCreationMode Apply(LineCommand command)
        {
            command.FirstStartPoint = _start.Value;
            return new NextPointMode(_start.Value, command.FirstStartPoint.Value, command.GetDocument());
        }
    }


}
