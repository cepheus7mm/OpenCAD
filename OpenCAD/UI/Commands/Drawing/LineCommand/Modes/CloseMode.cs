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
    public class CloseMode : ILineCreationMode
    {
        private readonly Point3D _current;
        private readonly Point3D _first;
        private readonly OpenCADDocument _document;

        public CloseMode(Point3D current, Point3D first, OpenCADDocument document)
        {
            _current = current;
            _first = first;
            _document = document;
        }

        public string Prompt => "";
        public string[] Keywords => Array.Empty<string>();
        public bool IsComplete => true;

        public void SetPoint(Point3D p) { }
        public Point3D GetBasePoint() => _current;

        public IEnumerable<OpenCADObject> GetPreview(Point3D cursor)
        {
            yield return new Line(_current, _first, _document);
        }

        public ILineCreationMode Apply(LineCommand command)
        {
            command.CreateLine(_current, _first);
            //command.Context?.OutputMessage("Figure closed.");
            return new FinishedMode();
        }
    }
}
