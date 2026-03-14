using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.PolylineCreation.Modes
{
    /// <summary>
    /// Terminal mode for PolylineCommand.
    /// Once the command enters this mode, the Execute loop ends.
    /// </summary>
    public class FinishedMode : PolylineCreationModeBase
    {
        public override string Prompt => string.Empty;

        public override string[] Keywords { get; }
            = Array.Empty<string>();

        // No point input is needed
        public override bool IsComplete => true;

        // No transition — this is the final state
        public override IPolylineCreationMode Apply(PolylineCommand command)
        {
            return this;
        }

        // No preview in a finished state
        public override IEnumerable<IDrawable> GetPreview(PolylineCommand command)
            => Enumerable.Empty<IDrawable>();
    }
}
