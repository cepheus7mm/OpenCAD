using OpenCAD.Geometry;
using System.Drawing;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    /// <summary>
    /// Input helper that prompts for a second corner point while rendering a filled
    /// rectangle preview between the base point and the current mouse position.
    /// The drag line produced by GetPointInput is suppressed; the rectangle is rendered
    /// instead via the ViewportViewModel rectangle preview properties.
    /// </summary>
    public class GetRectangleInput : InputHelperBase
    {
        private readonly Color _edgeColor;
        private readonly Color _fillColor;
        private readonly GetPointInput _pointInputHelper;

        public GetRectangleInput(
            ICommandContext context,
            ViewportViewModel? viewModel,
            Color edgeColor,
            Color fillColor)
            : base(context, viewModel)
        {
            _edgeColor = edgeColor;
            _fillColor = fillColor;
            _pointInputHelper = new GetPointInput(context, viewModel);
        }

        /// <inheritdoc/>
        public override bool ProcessKeyboardInput(string input)
            => _pointInputHelper.ProcessKeyboardInput(input);

        /// <summary>
        /// Prompts the user for the opposite corner while displaying a live rectangle preview.
        /// <paramref name="inputParams"/> must have <see cref="InputParams.BasePoint"/> set to
        /// the first corner.
        /// </summary>
        public async Task<InputResult> GetRectangleAsync(InputParams inputParams)
        {
            _inputParams = inputParams;

            if (_viewModel == null || !inputParams.BasePoint.HasValue)
                return InputResult.Cancel;

            // Start the rectangle preview on the ViewModel.
            _viewModel.BeginRectanglePreview(inputParams.BasePoint.Value, _edgeColor, _fillColor);

            // Enable preview mode so HandlePointPickingMove fires the callback on every
            // mouse-move, which in turn updates the live rectangle.
            _viewModel.EnablePreviewMode(p => _viewModel.UpdateRectanglePreview(p));

            try
            {
                // Pass BasePoint = null so ViewportInteraction does NOT add a temp point,
                // which is what would otherwise produce the unwanted drag line.
                var modifiedParams = new InputParams
                {
                    Prompt              = inputParams.Prompt,
                    DefaultValue        = inputParams.DefaultValue,
                    AllowLastPoint      = inputParams.AllowLastPoint,
                    AllowArbitraryInput = inputParams.AllowArbitraryInput,
                    BasePoint           = null,
                    Keywords            = inputParams.Keywords,
                    CancellationToken   = inputParams.CancellationToken,
                    Context             = inputParams.Context ?? _context
                };

                return await _pointInputHelper.GetPointOrKeywordAsync(modifiedParams);
            }
            finally
            {
                _viewModel.DisablePreviewMode();
                _viewModel.EndRectanglePreview();
            }
        }
    }
}
