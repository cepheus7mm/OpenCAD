using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    /// <summary>
    /// Helper to obtain an angle from the user.
    /// Accepts numeric/keyword angular input or point picks (single pick with BasePoint set,
    /// or two-point entry when BasePoint not supplied).
    /// </summary>
    public class GetAngleInput : InputHelperBase
    {
        private readonly CommandBase _command;

        public GetAngleInput(
            ICommandContext context,
            ViewportViewModel? viewModel,
            CommandBase command)
            : base(context, viewModel)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
        }

        /// <summary>
        /// Prompt for an angle. Returns an InputResult containing either:
        /// - ResultType.Double with DoubleValue = angle (radians) when numeric/keyword or computed from picks
        /// - ResultType.Point when a point was picked (caller may compute angle if desired)
        /// - ResultType.Cancel when cancelled
        /// </summary>
        public async Task<InputResult> GetAngle(
            string prompt,
            bool allowLastPoint = false,
            Point3D? basePoint = null,
            string[]? keyWords = null,
            CancellationToken cancellationToken = default)
        {
            // Ensure BasePoint property reflects requested basePoint (used by GetPointInput)
            this.BasePoint = basePoint;

            var helper = new GetPointInput(_context, _viewModel);

            var result = new InputResult()
            {
                ResultType = InputResult.InputResultType.Cancel,
                DoubleValue = double.NaN
            };

            try
            {
                // First input: could be an angle keyword/number, or a point
                var first = await helper.GetPointOrKeywordAsync(
                    prompt,
                    allowLastPoint: allowLastPoint,
                    basePoint: this.BasePoint,
                    keywords: keyWords,
                    cancellationToken: cancellationToken);

                if (first == null || first.ResultType == InputResult.InputResultType.Cancel)
                    return result;

                // Handle keyword/numeric input
                if (first.ResultType == InputResult.InputResultType.Keyword && first.Keyword != null)
                {
                    // Allow external keyword handler to intercept (e.g., command-specific)
                    if (KeyWordHandler != null)
                    {
                        try { KeyWordHandler(first.Keyword); }
                        catch { }
                        return result;
                    }

                    // Try parse plain numeric
                    if (double.TryParse(first.Keyword, out var parsed))
                    {
                        result.ResultType = InputResult.InputResultType.Double;
                        result.DoubleValue = parsed;
                        return result;
                    }

                    // Try document unit parsing (angular)
                    var doc = _context.GetDocument();
                    if (doc != null)
                    {
                        try
                        {
                            result.DoubleValue = doc.StringToValue(first.Keyword, OpenCADDocument.UnitFormatType.Angular);
                            result.ResultType = InputResult.InputResultType.Double;
                            return result;
                        }
                        catch
                        {
                            result.DoubleValue = double.NaN;
                            result.ResultType = InputResult.InputResultType.None;
                            return result;
                        }
                    }

                    return result;
                }

                // Handle point input
                if (first.ResultType == InputResult.InputResultType.Point && first.Point != null)
                {
                    // If we already had a BasePoint, compute angle from BasePoint -> picked point
                    if (this.BasePoint != null)
                    {
                        result.ResultType = InputResult.InputResultType.Double;
                        result.DoubleValue = this.BasePoint.AngleTo(first.Point);
                        return result;
                    }

                    // No base supplied: treat as first point of two-point entry
                    this.BasePoint = first.Point;
                    _command.StartPreview();
                    try
                    {
                        var secondPrompt = OpenCADStrings.SecondPointPrompt ?? "Specify second point:";

                        var second = await helper.GetPointOrKeywordAsync(
                            secondPrompt,
                            allowLastPoint: allowLastPoint,
                            basePoint: this.BasePoint,
                            keywords: keyWords,
                            cancellationToken: cancellationToken);

                        if (second == null || second.ResultType == InputResult.InputResultType.Cancel)
                            return new InputResult() { ResultType = InputResult.InputResultType.Cancel, DoubleValue = double.NaN };

                        if (second.ResultType == InputResult.InputResultType.Point && second.Point != null)
                        {
                            result.ResultType = InputResult.InputResultType.Double;
                            result.DoubleValue = this.BasePoint.AngleTo(second.Point);
                            return result;
                        }

                        // If second was a keyword or numeric, attempt to interpret as angle (unlikely in two-point flow)
                        if (second.ResultType == InputResult.InputResultType.Keyword && second.Keyword != null)
                        {
                            if (double.TryParse(second.Keyword, out var parsed2))
                            {
                                result.ResultType = InputResult.InputResultType.Double;
                                result.DoubleValue = parsed2;
                                return result;
                            }

                            var doc2 = _context.GetDocument();
                            if (doc2 != null)
                            {
                                try
                                {
                                    result.DoubleValue = doc2.StringToValue(second.Keyword, OpenCADDocument.UnitFormatType.Angular);
                                    result.ResultType = InputResult.InputResultType.Double;
                                    return result;
                                }
                                catch { }
                            }
                        }

                        return new InputResult() { ResultType = InputResult.InputResultType.Cancel, DoubleValue = double.NaN };
                    }
                    finally
                    {
                        _command.StopPreview(false);
                        this.BasePoint = null;
                    }
                }

                return result;
            }
            finally
            {
                // default cancel if nothing returned
                if (result.ResultType == InputResult.InputResultType.Cancel)
                {
                    result.DoubleValue = double.NaN;
                }
            }
        }
    }
}