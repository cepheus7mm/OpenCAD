using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.LineCommand.Modes
{
    public class FinishedMode : ILineCreationMode
    {
        public string Prompt => "";
        public string[] Keywords => Array.Empty<string>();
        public bool IsComplete => true;

        public void SetPoint(Point3D p) { }
        public Point3D GetBasePoint() => Point3D.NotAPoint;
        public IEnumerable<OpenCADObject> GetPreview(Point3D cursor) { yield break; }
        public ILineCreationMode Apply(LineCommand command) => this;
    }
}
