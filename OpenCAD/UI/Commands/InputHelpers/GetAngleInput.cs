using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using OpenCAD;
using OpenCAD.Geometry;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    public class GetAngleInput : InputHelperBase
    {
        private GetPointInput? _pointInputHelper;

        public GetAngleInput(ICommandContext context, ViewportViewModel? viewModel)
            : base(context, viewModel)
        {
            _pointInputHelper = new GetPointInput(_context, _viewModel);
        }

        /// <summary>
        /// Override to route all keyboard input to the nested GetPointInput helper.
        /// </summary>
        public override bool ProcessKeyboardInput(string input)
        {
            // Always route to the nested helper if it exists
            if (_pointInputHelper != null)
            {
                return _pointInputHelper.ProcessKeyboardInput(input);
            }

            // Fallback to base if no nested helper (shouldn't happen)
            return base.ProcessKeyboardInput(input);
        }

        /// <summary>
        /// Prompt user for an angle. Accepts numeric/keyword input (unit-aware) or a point pick.
        /// Returns InputResult.Double on success with angle in radians.
        /// </summary>
        public async Task<InputResult> GetAngle(InputParams inputParams)
        {
            _inputParams = inputParams;
            // Use GetPointInput to allow picking a point or entering a keyword/text

            // Reuse the unified point/keyword input system
            var result = await _pointInputHelper.GetPointOrKeywordAsync(_inputParams);

            // Cancel
            if (result.IsCancel)
                return InputResult.Cancel;

            // Empty input → use default
            if (result.IsDefault && _inputParams.DefaultValue is double defaulDouble)
            {
                return InputResult.FromDouble(defaulDouble);
            }

            // Arbitrary text → parse as angle
            if (result.IsArbitrary)
            {
                string text = result.Keyword;

                // Try numeric first
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
                    return InputResult.FromDouble(numeric);

                // Try unit-aware parsing
                try
                {
                    double val = Document.StringToValue(text, OpenCADDocument.UnitFormatType.Angular);
                    return InputResult.FromDouble(val);
                }
                catch
                {
                    return InputResult.BadDouble;
                }
            }

            // Point picked → compute angle from basePoint
            if (result.IsPoint &&
                result.Point.HasValue &&
                inputParams.BasePoint.HasValue)
            {
                var p = result.Point.Value;
                var b = inputParams.BasePoint.Value;

                double angle = Math.Atan2(p.Y - b.Y, p.X - b.X);
                return InputResult.FromDouble(angle);
            }

            return InputResult.BadDouble;
        }
    }
}