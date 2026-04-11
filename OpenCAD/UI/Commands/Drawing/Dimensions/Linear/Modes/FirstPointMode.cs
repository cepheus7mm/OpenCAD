using OpenCAD.Geometry;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.Dimensions.Linear.Modes
{
    public class FirstPointMode : LinearDimensionModeBase
    {
        public override string Prompt => "Specify first extension line origin";

        public override string[] Keywords { get; }
            = Array.Empty<string>();

        public override bool AllowLastPoint => false;

        public override bool IsComplete => InputPoint.HasValue;

        public override ILinearDimensionMode Apply(LinearDimensionCommand command)
        {
            command.SetFirstPoint(InputPoint!.Value);

            return new SecondPointMode(InputPoint!.Value);
        }
    }
}