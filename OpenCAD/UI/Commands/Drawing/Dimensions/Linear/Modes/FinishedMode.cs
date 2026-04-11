using OpenCAD.Interfaces;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.Dimensions.Linear.Modes
{
    /// <summary>
    /// Terminal mode for LinearDimensionCommand.
    /// Once the command enters this mode, the Execute loop ends.
    /// </summary>
    public class FinishedMode : LinearDimensionModeBase
    {
        public override string Prompt => string.Empty;

        public override string[] Keywords { get; }
            = Array.Empty<string>();

        public override bool IsComplete => true;

        public override ILinearDimensionMode Apply(LinearDimensionCommand command)
        {
            return this;
        }

        //public override IEnumerable<IDrawable> GetPreview(LinearDimensionCommand command)
        //    => Enumerable.Empty<IDrawable>();
    }
}