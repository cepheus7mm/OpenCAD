using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;
using static UI.Commands.InputHelpers.InputResult;

namespace UI.Commands.InputHelpers
{
    public class GetDistanceInput : InputHelperBase
    {
        private GetPointInput _pointInputHelper;

        public GetDistanceInput(
            ICommandContext context,
            ViewportViewModel? viewModel)
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

        public async Task<InputResult> GetDistance(InputParams parameters)
        {
            _inputParams = parameters;

            //
            // CASE A — basePoint is provided
            //
            if (_inputParams.BasePoint.HasValue && _inputParams.BasePoint.Value.IsValid)
                return await GetDistanceFromBasePoint(_inputParams);

            //
            // CASE B — no basePoint: first input determines the flow
            //
            var first = await _pointInputHelper.GetPointOrKeywordAsync(_inputParams);

            // Try to parse directly (numeric, default, unit-aware)
            var parsed = ParseDistanceInput(first, null, parameters.DefaultValue as double?);

            if (parsed.ResultType == InputResultType.Double)
                return parsed;

            // Otherwise first was a point → ask for second
            if (first.ResultType == InputResultType.Point && first.Point.HasValue)
            {
                _inputParams.BasePoint = first.Point.Value;
                _inputParams.Prompt = "Specify second point";
                return await GetDistanceFromBasePoint(_inputParams);
            }

            return InputResult.BadDouble;
        }

        private async Task<InputResult> GetDistanceFromBasePoint(InputParams inputParams)
        {
            var second = await _pointInputHelper!.GetPointOrKeywordAsync(inputParams);

            return ParseDistanceInput(second, inputParams.BasePoint, null);
        }

        protected override void HandleMatchedKeyword(string? keyword)
        {
            // Route to nested helper's keyword handler
            if (_pointInputHelper != null)
            {
                _pointInputHelper.KeyWordHandler?.Invoke(keyword);
            }
            else
            {
                base.HandleMatchedKeyword(keyword);
            }
        }

        private InputResult ParseDistanceInput(
            InputResult input,
            Point3D? basePoint,
            double? defaultValue)
        {
            // Cancel
            if (input.IsCancel)
                return input;

            // Empty keyword → default
            if (input.ResultType == InputResult.InputResultType.Keyword &&
                string.IsNullOrWhiteSpace(input.Keyword) &&
                defaultValue.HasValue)
            {
                return InputResult.FromDouble(defaultValue.Value);
            }

            // Arbitrary numeric
            if (input.ResultType == InputResult.InputResultType.Arbitrary &&
                double.TryParse(input.Keyword, out double numeric))
            {
                return InputResult.FromDouble(numeric);
            }

            // Arbitrary unit-aware
            if (input.IsArbitrary)
            {
                try
                {
                    double val = Document.StringToValue(
                        input.Arbitrary,
                        OpenCADDocument.UnitFormatType.Linear);

                    return InputResult.FromDouble(val);
                }
                catch
                {
                    return InputResult.BadDouble;
                }
            }

            // Point input → compute distance
            if (input.ResultType == InputResult.InputResultType.Point &&
                input.Point.HasValue &&
                basePoint.HasValue)
            {
                double d = basePoint.Value.DistanceTo(input.Point.Value);
                return InputResult.FromDouble(d);
            }

            return InputResult.BadDouble;
        }
    }
}
