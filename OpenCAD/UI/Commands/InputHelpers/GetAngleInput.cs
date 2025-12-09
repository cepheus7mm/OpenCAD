using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using OpenCAD;
using OpenCAD.Geometry;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    public class GetAngleInput : InputHelperBase
    {
        private GetPointInput? _pointInputHelper;

        public GetAngleInput(ICommandContext context, ViewportViewModel? viewModel)
            : base(context, viewModel)
        {
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
        public async Task<InputResult> GetAngle(
            string prompt,
            double? defaultValue = null,  // NEW parameter (angle in radians)
            bool allowLastPoint = false,
            Point3D? basePoint = null,
            string[]? keyWords = null,
            CancellationToken cancellationToken = default)
        {
            DefaultValue = defaultValue;  // NEW: Store default
            
            // NEW: Append formatted default to prompt if provided
            if (defaultValue.HasValue)
            {
                prompt = $"{prompt} <{FormatDefaultForPrompt(OpenCADDocument.UnitFormatType.Angular)}>";
            }

            // Use GetPointInput to allow picking a point or entering a keyword/text
            _pointInputHelper = new GetPointInput(_context, _viewModel) { AllowArbitraryInput = true };

            var result = await _pointInputHelper.GetPointOrKeywordAsync(
                prompt,
                allowLastPoint: allowLastPoint,
                basePoint: basePoint,
                keywords: keyWords,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var bad = new InputResult { ResultType = InputResult.InputResultType.None, DoubleValue = double.NaN };
            if (result == null) return bad;

            if (result.ResultType == InputResult.InputResultType.Cancel)
                return new InputResult { ResultType = InputResult.InputResultType.Cancel, DoubleValue = double.NaN };

            // NEW: Check for empty input with default value
            if (result.ResultType == InputResult.InputResultType.Keyword 
                && string.IsNullOrWhiteSpace(result.Keyword) 
                && defaultValue.HasValue)
            {
                return new InputResult 
                { 
                    ResultType = InputResult.InputResultType.Double, 
                    DoubleValue = defaultValue.Value  // Already in radians
                };
            }

            if (result.ResultType == InputResult.InputResultType.Arbitrary && result.Keyword != null)
            {
                // Try interpret keyword as numeric angle first
                if (double.TryParse(result.Keyword, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return new InputResult { ResultType = InputResult.InputResultType.Double, DoubleValue = parsed };
                }

                // Existing unit-aware parsing (already correct)
                if (Document != null)
                {
                    try
                    {
                        var val = Document.StringToValue(result.Keyword, OpenCADDocument.UnitFormatType.Angular);
                        return new InputResult { ResultType = InputResult.InputResultType.Double, DoubleValue = val };
                    }
                    catch
                    {
                        return bad;
                    }
                }

                return bad;
            }

            if (result.ResultType == InputResult.InputResultType.Point && result.Point != null)
            {
                if (basePoint == null)
                    return bad;

                // Angle from basePoint to picked point
                double dx = result.Point.X - basePoint.X;
                double dy = result.Point.Y - basePoint.Y;
                double angle = Math.Atan2(dy, dx);
                return new InputResult { ResultType = InputResult.InputResultType.Double, DoubleValue = angle };
            }

            return bad;
        }
    }
}