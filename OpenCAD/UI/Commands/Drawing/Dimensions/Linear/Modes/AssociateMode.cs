using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using UI.Commands.Interfaces;

namespace UI.Commands.Drawing.Dimensions.Linear.Modes
{
    /// <summary>
    /// Initial mode for LinearDimensionCommand. Prompts the user to select a line
    /// for associative dimensioning, or switch to manual point entry via the "Points" keyword.
    /// </summary>
    public class AssociateMode : LinearDimensionModeBase
    {
        public override string Prompt => "Select line";

        public override string[] Keywords { get; }
            = new[] { "POINTS" };

        public override bool AllowLastPoint => false;

        public override UserInputType UserInputType => UserInputType.Entity;

        public override bool IsComplete => SelectedEntity != null;

        /// <summary>
        /// The entity selected by the user (set by the command's entity input handler).
        /// </summary>
        internal OpenCADObject? SelectedEntity { get; private set; }

        /// <summary>
        /// Called by the command when the user selects an entity.
        /// </summary>
        public void SetEntity(OpenCADObject entity)
        {
            SelectedEntity = entity;
        }

        public override ILinearDimensionMode Apply(LinearDimensionCommand command)
        {
            if (SelectedEntity is not ICurve curve)
                return this;

            command.SetAssociatedEntity(curve);

            return new TypeMode(curve.StartPoint, curve.EndPoint);
        }
    }
}