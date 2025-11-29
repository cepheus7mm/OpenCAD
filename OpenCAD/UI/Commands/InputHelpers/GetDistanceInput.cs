using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    public class GetDistanceInput : InputHelperBase
    {
        private CommandBase _command;
        private GetPointInput? _pointInputHelper;

        public GetDistanceInput(
            ICommandContext context,
            ViewportViewModel? viewModel,
            CommandBase command)
            : base(context, viewModel)
        {
            this._command = command;
        }

        public async Task<InputResult> GetDistance(
            string prompt,
            bool allowLastPoint = false,
            Point3D? basePoint = null,
            string[]? keyWords = null,
            CancellationToken cancellationToken = default)
        {
            AllowArbitraryInput = true;
            _pointInputHelper = new GetPointInput(_context, _viewModel) { AllowArbitraryInput = true };

            var result = new InputResult() { ResultType = InputResult.InputResultType.Cancel, DoubleValue = double.NaN };

            try
            {
                var first = await _pointInputHelper.GetPointOrKeywordAsync(
                    prompt,
                    allowLastPoint: allowLastPoint,
                    basePoint: BasePoint,
                    keywords: keyWords,
                    cancellationToken: cancellationToken);

                if (first == null || first.ResultType == InputResult.InputResultType.Cancel)
                    return result;

                if (first.ResultType == InputResult.InputResultType.Keyword && first.Keyword != null)
                {
                    if (KeyWordHandler != null)
                    {
                        try { KeyWordHandler(first.Keyword); }
                        catch { }
                        return result;
                    }

                    if (double.TryParse(first.Keyword, out var parsed))
                    {
                        result.DoubleValue = parsed;
                        result.ResultType = InputResult.InputResultType.Double;
                        return result;
                    }

                    var doc = _context.GetDocument();
                    if (doc != null)
                    {
                        try
                        {
                            result.DoubleValue = doc.StringToValue(first.Keyword, OpenCADDocument.UnitFormatType.Linear);
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

                if (first.ResultType == InputResult.InputResultType.Point && first.Point != null)
                {
                    // two-point distance: set base, prompt for second pick
                    BasePoint = first.Point;
                    _command.StartPreview();
                    try
                    {
                        var secondPrompt = OpenCADStrings.SecondPointPrompt ?? "Specify second point:";

                        var second = await _pointInputHelper.GetPointOrKeywordAsync(
                            secondPrompt,
                            allowLastPoint: allowLastPoint,
                            basePoint: BasePoint,
                            keywords: keyWords,
                            cancellationToken: cancellationToken);

                        if (second == null || second.ResultType == InputResult.InputResultType.Cancel)
                            return new InputResult() { ResultType = InputResult.InputResultType.Cancel, DoubleValue = double.NaN };

                        if (second.ResultType == InputResult.InputResultType.Point && second.Point != null)
                        {
                            result.ResultType = InputResult.InputResultType.Double;
                            result.DoubleValue = BasePoint.DistanceTo(second.Point);
                            return second;
                        }

                        return new InputResult() { ResultType = InputResult.InputResultType.Cancel, DoubleValue = double.NaN };
                    }
                    finally
                    {
                        _command.StopPreview(false);
                        BasePoint = null;
                    }
                }
                if (first.ResultType == InputResult.InputResultType.Arbitrary)
                {
                    if(double.TryParse(first.Keyword, out double distance))
                    {
                        return new InputResult() { ResultType = InputResult.InputResultType.Double, DoubleValue = distance };
                    }
                }
            }
            finally
            {
                result = new InputResult() { ResultType = InputResult.InputResultType.Cancel, DoubleValue = double.NaN };
            }
            return result;
        }

        protected override void HandleMatchedKeyword(string? keyword)
        {
            if (_pointInputHelper == null)
            {
                base.HandleMatchedKeyword(keyword);
            }
            _pointInputHelper?.KeyWordHandler?.Invoke(keyword);
        }
    }
}
