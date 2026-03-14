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
    public class UndoMode : PolylineCreationModeBase
    {
        private readonly Point3D _lastPoint;
        private readonly Point3D _firstPoint;

        public UndoMode()
        {
        }

        public override string Prompt => string.Empty;

        public override string[] Keywords { get; }
            = Array.Empty<string>();

        // No point input needed
        public override bool IsComplete => false;

        public override IPolylineCreationMode Apply(PolylineCommand command)
        {
            command.RemoveLastVertex();

            return new NextPointMode();
        }

        // No preview — the command will finish immediately
        public override IEnumerable<IDrawable> GetPreview(PolylineCommand command)
            => Enumerable.Empty<IDrawable>();
    }
}
