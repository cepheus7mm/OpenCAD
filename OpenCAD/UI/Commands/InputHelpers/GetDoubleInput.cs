using System;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    /// <summary>
    /// Keyboard-only input helper that prompts the user to enter a numeric value.
    /// Does not allow point picking — use <see cref="GetDistanceInput"/> when a
    /// two-point pick is also acceptable.
    /// </summary>
    public class GetDoubleInput : InputHelperBase
    {
        private InputTaskController? _controller;

        public GetDoubleInput(ICommandContext context, ViewportViewModel? viewModel)
            : base(context, viewModel)
        {
        }

        public async Task<InputResult> GetDoubleAsync(InputParams inputParams)
        {
            _inputParams = inputParams;

            string formattedPrompt = PromptBuilder.Build(inputParams, _context.GetLastPoint());
            _context.PostToUI(() => _context.SetCommandPrompt(formattedPrompt));

            _controller = new InputTaskController();
            _controller.AttachCancellation(_inputParams.CancellationToken);

            KeyWordHandler = kw => _controller.CompleteWithKeyword(kw ?? string.Empty);

            try
            {
                return await _controller.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return InputResult.Cancel;
            }
        }

        public override bool ProcessKeyboardInput(string input)
        {
            if (_controller == null)
                return false;

            // Empty input → accept default if one was provided
            if (string.IsNullOrWhiteSpace(input))
            {
                if (_inputParams.DefaultValue is double d)
                {
                    _controller.CompleteWithDouble(d);
                    return true;
                }
                return false;
            }

            // Keyword matching (e.g. named options)
            base.ProcessKeyboardInput(input);
            if (LastInputHandled)
                return true;

            // Numeric parse
            if (double.TryParse(input, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double value))
            {
                _controller.CompleteWithDouble(value);
                return true;
            }

            // Unit-aware parse via document
            try
            {
                double val = Document.StringToValue(input, _inputParams.UnitFormatType
                    ?? OpenCAD.OpenCADDocument.UnitFormatType.Linear);
                _controller.CompleteWithDouble(val);
                return true;
            }
            catch { }

            return false;
        }
    }
}
