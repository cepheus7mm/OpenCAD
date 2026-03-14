using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.PolylineCreation.Modes
{
    public class FirstPointMode : PolylineCreationModeBase
    {
        public override string Prompt => "Specify first point";

        // No keywords for the first point
        public override string[] Keywords { get; }
            = Array.Empty<string>();

        // First point never uses last-point logic
        public override bool AllowLastPoint => false;

        public override bool IsComplete => InputPoint.HasValue;

        public override IPolylineCreationMode Apply(PolylineCommand command)
        {
            // Add the first vertex to the polyline
            command.AddVertex(InputPoint!.Value);

            // Transition to the main mode
            return new NextPointMode(InputPoint!.Value);
        }
    }
}