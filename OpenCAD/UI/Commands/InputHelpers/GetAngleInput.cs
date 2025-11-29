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
        public GetAngleInput(ICommandContext context, ViewportViewModel? viewModel)
            : base(context, viewModel)
        {
        }

        /// <summary>
        /// Prompt user for an angle. Accepts numeric/keyword input (unit-aware) or a point pick (angle from basePoint to picked point).
        /// Returns InputResult.Double on success with angle in radians, or Cancel/None as appropriate.
        /// </summary>
        public async Task<InputResult> GetAngle(
            string prompt,
            bool allowLastPoint = false,
            Point3D? basePoint = null,
            string[]? keyWords = null,
            CancellationToken cancellationToken = default)
        {
            // Use GetPointInput to allow picking a point or entering a keyword/text
            var helper = new GetPointInput(_context, _viewModel);

            // If caller provided keywords, pass them through
            var result = await helper.GetPointOrKeywordAsync(
                prompt,
                allowLastPoint: allowLastPoint,
                basePoint: basePoint,
                keywords: keyWords,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var bad = new InputResult { ResultType = InputResult.InputResultType.None, DoubleValue = double.NaN };
            if (result == null) return bad;

            if (result.ResultType == InputResult.InputResultType.Cancel)
                return new InputResult { ResultType = InputResult.InputResultType.Cancel, DoubleValue = double.NaN };

            if (result.ResultType == InputResult.InputResultType.Keyword && result.Keyword != null)
            {
                // Try interpret keyword as numeric angle first
                if (double.TryParse(result.Keyword, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return new InputResult { ResultType = InputResult.InputResultType.Double, DoubleValue = parsed };
                }

                // Try unit-aware parsing via document
                var doc = _context.GetDocument();
                if (doc != null)
                {
                    try
                    {
                        var val = doc.StringToValue(result.Keyword, OpenCADDocument.UnitFormatType.Angular);
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