using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    /// <summary>
    /// Prompt for arbitrary strings or configured keywords.
    /// Supports live preview callback as text is being typed.
    /// </summary>
    public class GetStringInput : InputHelperBase
    {
        private TaskCompletionSource<InputResult?>? _tcs;
        private Action<string>? _previewCallback;

        public bool AllowArbitraryInput { get; private set; }
        public string[]? Keywords { get; private set; }

        public GetStringInput(ICommandContext context, ViewportViewModel? viewModel)
            : base(context, viewModel)
        {
        }

        /// <summary>
        /// Show the prompt and wait for user keyboard input (keyword or arbitrary text).
        /// Optionally provide a preview callback that's invoked as the user types.
        /// </summary>
        public async Task<InputResult?> GetStringAsync(
            string prompt,
            bool allowArbitrary = true,
            string[]? keywords = null,
            Action<string>? previewCallback = null,
            CancellationToken cancellationToken = default)
        {
            AllowArbitraryInput = allowArbitrary;
            Keywords = keywords;
            _previewCallback = previewCallback;

            _tcs = new TaskCompletionSource<InputResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

            // If caller cancels, complete the TCS with null to indicate cancellation
            using var reg = cancellationToken.Register(() => _tcs?.TrySetResult(null));

            // Echo prompt to the user
            _context.OutputMessage(prompt);

            var result = await _tcs.Task.ConfigureAwait(false);

            // cleanup
            _tcs = null;
            _previewCallback = null;
            AllowArbitraryInput = false;
            Keywords = null;

            return result;
        }

        /// <summary>
        /// Called when user is typing (TextChanged event from CommandInputControl).
        /// Invokes the preview callback if registered. Does NOT complete the async task.
        /// Only processes the preview, not the final submission.
        /// </summary>
        public void OnTextChanged(string currentText)
        {
            // Invoke preview callback without completing the task
            _previewCallback?.Invoke(currentText ?? string.Empty);
        }

        /// <summary>
        /// Called when user presses Enter.
        /// Completes the async task with the final input.
        /// </summary>
        public override bool ProcessKeyboardInput(string input)
        {
            // This should only be called when Enter is pressed
            // The input string should contain the full text that was typed
            
            if (string.IsNullOrWhiteSpace(input))
            {
                // Empty input - check if we should allow it
                if (_tcs != null && AllowArbitraryInput)
                {
                    var res = new InputResult()
                    {
                        ResultType = InputResult.InputResultType.Arbitrary,
                        Keyword = string.Empty
                    };
                    _tcs.TrySetResult(res);
                }
                return false;
            }

            // Let base handle keywords / arbitrary matching
            base.ProcessKeyboardInput(input);

            // If base handled it, completion occurs in HandleMatchedKeyword
            if (LastInputHandled)
                return false;

            // If arbitrary allowed, complete the task
            if (AllowArbitraryInput && _tcs != null)
            {
                var res = new InputResult()
                {
                    ResultType = InputResult.InputResultType.Arbitrary,
                    Keyword = input
                };
                _tcs.TrySetResult(res);
            }

            return false;
        }

        protected override void HandleMatchedKeyword(string? keyword)
        {
            if (_tcs != null)
            {
                var isKeyword = false;
                if (!string.IsNullOrEmpty(keyword) && Keywords != null && Keywords.Length > 0)
                {
                    isKeyword = Keywords.Any(k =>
                        string.Equals(k, keyword, StringComparison.InvariantCultureIgnoreCase) ||
                        k.StartsWith(keyword, StringComparison.InvariantCultureIgnoreCase));
                }

                var result = new InputResult()
                {
                    Keyword = keyword,
                    ResultType = isKeyword ? InputResult.InputResultType.Keyword : InputResult.InputResultType.Arbitrary
                };

                _tcs.TrySetResult(result);
            }

            base.HandleMatchedKeyword(keyword ?? string.Empty);
        }
    }
}