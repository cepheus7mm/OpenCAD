using OpenCAD;
using OpenCAD.Geometry;
using System;
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
            _command = command;
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

        public async Task<InputResult> GetDistance(
            string prompt,
            double? defaultValue = null,
            bool allowLastPoint = false,
            Point3D? basePoint = null,
            string[]? keyWords = null,
            CancellationToken cancellationToken = default)
        {
            AllowArbitraryInput = true;
            DefaultValue = defaultValue;
            
            // Append formatted default to prompt if provided
            if (defaultValue.HasValue)
            {
                prompt = $"{prompt} <{FormatDefaultForPrompt(OpenCADDocument.UnitFormatType.Linear)}>";
            }
            
            _pointInputHelper = new GetPointInput(_context, _viewModel) { AllowArbitraryInput = true };
            
            // Pass the default value to the nested helper so it knows a default exists
            _pointInputHelper.DefaultValue = defaultValue;

            try
            {
                InputResult? first = null;
                var secondDefault = defaultValue;
                // If basePoint is provided, skip to getting the second point
                if (basePoint == null)
                {
                    first = await _pointInputHelper.GetPointOrKeywordAsync(
                                        prompt,
                                        allowLastPoint: allowLastPoint,
                                        basePoint: null,
                                        keywords: keyWords,
                                        cancellationToken: cancellationToken);
                    prompt = OpenCADStrings.SecondPointPrompt ?? "Specify second point:";
                    secondDefault = null;
                }
                else
                {
                    first = new InputResult 
                    { 
                        ResultType = InputResult.InputResultType.Point, 
                        Point = basePoint 
                    };
                }

                if (first == null || first.ResultType == InputResult.InputResultType.Cancel)
                {
                    return CreateCancelResult();
                }

                // Check for empty keyword (signal from default acceptance)
                if (first.ResultType == InputResult.InputResultType.Keyword 
                    && string.IsNullOrWhiteSpace(first.Keyword) 
                    && defaultValue.HasValue)
                {
                    return CreateDoubleResult(defaultValue.Value);
                }

                // Check for keyword or numeric input
                if (first.ResultType == InputResult.InputResultType.Keyword && first.Keyword != null)
                {
                    return ProcessKeywordInput(first.Keyword);
                }

                // User picked a point - prompt for second point to calculate distance
                if (first.ResultType == InputResult.InputResultType.Point && first.Point != null)
                {
                    BasePoint = first.Point;
                    _command.StartPreview();

                    try
                    {
                        var second = await _pointInputHelper!.GetPointOrKeywordAsync(
                            prompt,
                            defaultValue: secondDefault,
                            allowLastPoint: allowLastPoint,
                            basePoint: BasePoint,
                            keywords: keyWords,
                            cancellationToken: cancellationToken);

                        if (second?.ResultType == InputResult.InputResultType.Point && second.Point != null)
                        {
                            return CreateDoubleResult(BasePoint.DistanceTo(second.Point));
                        }
                        if (second?.ResultType == InputResult.InputResultType.Keyword && second.Keyword != null && second.Keyword.Length == 0)
                        {
                            return CreateDoubleResult((double)DefaultValue);
                        }
                        if (second?.ResultType == InputResult.InputResultType.Arbitrary && second.Keyword != null)
                        {
                            if (double.TryParse(second.Keyword, out double distanceFromInput))
                            {
                                return CreateDoubleResult(distanceFromInput);
                            }
                            if (Document != null)
                            {
                                try
                                {
                                    var val = Document.StringToValue(second.Keyword, OpenCADDocument.UnitFormatType.Linear);
                                    return new InputResult { ResultType = InputResult.InputResultType.Double, DoubleValue = val };
                                }
                                catch
                                {
                                    return CreateCancelResult();
                                }
                            }
                        }

                        return CreateCancelResult();
                    }
                    finally
                    {
                        _command.StopPreview(false);
                        BasePoint = null;
                    }
                }

                // Arbitrary input fallback
                if (first.ResultType == InputResult.InputResultType.Arbitrary && double.TryParse(first.Keyword, out double distance))
                {
                    return CreateDoubleResult(distance);
                }
            }
            catch (OperationCanceledException)
            {
                return CreateCancelResult();
            }
            
            return CreateCancelResult();
        }

        /// <summary>
        /// Get distance by prompting for a second point and calculating distance from basePoint.
        /// </summary>
        private async Task<InputResult> GetDistanceFromTwoPoints(
            Point3D basePoint,
            bool allowLastPoint,
            string[]? keyWords,
            CancellationToken cancellationToken)
        {
            BasePoint = basePoint;
            _command.StartPreview();
            
            try
            {
                var secondPrompt = OpenCADStrings.SecondPointPrompt ?? "Specify second point:";

                var second = await _pointInputHelper!.GetPointOrKeywordAsync(
                    secondPrompt,
                    allowLastPoint: allowLastPoint,
                    basePoint: BasePoint,
                    keywords: keyWords,
                    cancellationToken: cancellationToken);

                if (second?.ResultType == InputResult.InputResultType.Point && second.Point != null)
                {
                    return CreateDoubleResult(BasePoint.DistanceTo(second.Point));
                }

                return CreateCancelResult();
            }
            finally
            {
                _command.StopPreview(false);
                BasePoint = null;
            }
        }

        /// <summary>
        /// Process keyword input - try to parse as numeric or unit-aware distance.
        /// </summary>
        private InputResult ProcessKeywordInput(string keyword)
        {
            // Check for keyword handler first
            if (KeyWordHandler != null)
            {
                try { KeyWordHandler(keyword); }
                catch { }
                return CreateCancelResult();
            }

            // Try simple numeric parse
            if (double.TryParse(keyword, out var parsed))
            {
                return CreateDoubleResult(parsed);
            }

            // Try unit-aware parsing
            if (Document != null)
            {
                try
                {
                    return CreateDoubleResult(Document.StringToValue(keyword, OpenCADDocument.UnitFormatType.Linear));
                }
                catch
                {
                    return new InputResult 
                    { 
                        ResultType = InputResult.InputResultType.None, 
                        DoubleValue = double.NaN 
                    };
                }
            }

            return CreateCancelResult();
        }

        private static InputResult CreateDoubleResult(double value)
        {
            return new InputResult 
            { 
                ResultType = InputResult.InputResultType.Double, 
                DoubleValue = value 
            };
        }

        private static InputResult CreateCancelResult()
        {
            return new InputResult 
            { 
                ResultType = InputResult.InputResultType.Cancel, 
                DoubleValue = double.NaN 
            };
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
    }
}
