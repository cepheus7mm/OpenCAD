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
    public class NextPointMode : ILineCreationMode
    {
        private Point3D _start;
        private Point3D? _end;
        private readonly Point3D _firstStart;
        OpenCADDocument _document;

        public NextPointMode(Point3D start, Point3D firstStart, OpenCADDocument document)
        {
            _start = start;
            _firstStart = firstStart;
            _document = document;
        }

        public string Prompt => "Specify next point";
        public string[] Keywords => new[] { "CLOSE", "UNDO" };
        public bool IsComplete => _end.HasValue;

        public void SetPoint(Point3D p) => _end = p;
        public Point3D GetBasePoint() => _start;

        public IEnumerable<OpenCADObject> GetPreview(Point3D cursor)
        {
            yield return new Line(_start, cursor, _document);
        }

        public ILineCreationMode Apply(LineCommand command)
        {
            if (!_end.HasValue)
                return this;

            command.CreateLine(_start, _end.Value);
            return new NextPointMode(_end.Value, _firstStart, command.GetDocument());
        }
    }
}
