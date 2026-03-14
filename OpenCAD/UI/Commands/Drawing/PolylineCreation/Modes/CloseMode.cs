using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.PolylineCreation.Modes
{
    public class CloseMode : PolylineCreationModeBase
    {
        private readonly Point3D _lastPoint;
        private readonly Point3D _firstPoint;

        public CloseMode(Point3D lastPoint, Point3D firstPoint)
        {
            _lastPoint = lastPoint;
            _firstPoint = firstPoint;
        }

        public override string Prompt => string.Empty;

        // No keywords — this mode is terminal
        public override string[] Keywords { get; }
            = Array.Empty<string>();

        // No point input needed
        public override bool IsComplete => true;

        public override IPolylineCreationMode Apply(PolylineCommand command)
        {
            // Add the closing segment
            command.AddVertex(_firstPoint);

            // Mark the polyline as closed
            command.SetClosed(true);

            // Transition to FinishedMode
            return new FinishedMode();
        }

        // No preview — the command will finish immediately
        public override IEnumerable<IDrawable> GetPreview(PolylineCommand command)
            => Enumerable.Empty<IDrawable>();
    }
}
