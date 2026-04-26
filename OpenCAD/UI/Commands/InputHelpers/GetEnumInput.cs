using System;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    /// <summary>
    /// Keyboard-only input helper that prompts the user to select an enum value.
    /// Uses enum member names as keywords, leveraging the existing keyword matching
    /// and prompt formatting infrastructure.
    /// </summary>
    public class GetEnumInput<TEnum> : InputHelperBase where TEnum : struct, Enum
    {
        private InputTaskController? _controller;

        public GetEnumInput(ICommandContext context, ViewportViewModel? viewModel)
            : base(context, viewModel)
        {
        }

        /// <summary>
        /// Prompt the user to select an enum value via keyword input.
        /// Enter accepts the default, Escape cancels.
        /// </summary>
        public async Task<InputResult> GetEnumAsync(InputParams inputParams)
        {
            // Inject enum names as keywords if not already set
            if (inputParams.Keywords is null or { Length: 0 })
                inputParams.Keywords = Enum.GetNames<TEnum>();

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

            // Empty input with default → accept default
            if (string.IsNullOrWhiteSpace(input) && _inputParams.DefaultValue != null)
            {
                string defaultName = _inputParams.DefaultValue is TEnum e
                    ? e.ToString()
                    : _inputParams.DefaultValue.ToString() ?? string.Empty;

                _controller.CompleteWithKeyword(defaultName);
                return true;
            }

            // Keyword matching via base
            base.ProcessKeyboardInput(input);
            if (LastInputHandled)
                return true;

            return false;
        }
    }
}