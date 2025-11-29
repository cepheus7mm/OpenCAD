using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands.InputHelpers
{
    public class InputHelperBase : IInputHelper
    {
        protected readonly ICommandContext _context;
        protected readonly ViewportViewModel? _viewModel;


        public bool AllowArbitraryInput { get; set; } = false;

        public Point3D? BasePoint { get; set; }

        /// <summary>
        /// Optional consumer that wants to be notified when a keyword is handled.
        /// Derived helpers or commands may set this.
        /// </summary>
        public Action<string?>? KeyWordHandler { get; set; }

        /// <summary>
        /// Keywords this helper should recognize. Set by derived helper when starting input.
        /// </summary>
        protected string[]? Keywords { get; set; }

        /// <summary>
        /// Indicates whether the last ProcessKeyboardInput call was handled by the base (keyword matched).
        /// Derived classes can check this to avoid duplicate processing.
        /// </summary>
        protected bool LastInputHandled { get; private set; }

        public InputHelperBase(ICommandContext context, ViewportViewModel? viewModel)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _viewModel = viewModel;
        }

        /// <summary>
        /// Default keyboard processing: handle keywords if configured.
        /// Derived classes should call base.ProcessKeyboardInput(input) first and, if
        /// LastInputHandled is true, return immediately.
        /// </summary>
        public virtual bool ProcessKeyboardInput(string input)
        {
            // Reset flag each call
            LastInputHandled = false;

            if ((Keywords == null && !AllowArbitraryInput) || string.IsNullOrWhiteSpace(input))
                return false;

            var inputUpper = input.Trim().ToUpperInvariant();
            var matchedKeyword = Keywords?.FirstOrDefault(k =>
                k.ToUpperInvariant() == inputUpper ||
                k.ToUpperInvariant().StartsWith(inputUpper));

            if (matchedKeyword != null || AllowArbitraryInput)
            {
                // Let derived classes handle the matched keyword result (e.g. complete a TCS)
                HandleMatchedKeyword((!string.IsNullOrEmpty(matchedKeyword) ? matchedKeyword : input) ?? string.Empty);

                // Notify optional external handler
                KeyWordHandler?.Invoke(matchedKeyword);

                // Mark handled so derived code won't process the same input again
                LastInputHandled = true;
            }

            // Keep old behavior: return false so CommandBase.ProcessInput remains compatible
            return false;
        }

        /// <summary>
        /// Called when a keyword match is found. Default behavior is to echo the keyword
        /// to history via the command context. Derived helpers should override to also
        /// complete any awaiting task/result, then (optionally) call base.HandleMatchedKeyword.
        /// </summary>
        protected virtual void HandleMatchedKeyword(string keyword)
        {
            // Default: write keyword echo to history so user sees it.
            _context.OutputMessage($"Keyword: {keyword}");
        }
    }
}