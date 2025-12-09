using OpenCAD;
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
        
        // Cache document reference
        protected OpenCADDocument? Document { get; private set; }
        
        // Make public so composed helpers can set it
        public object? DefaultValue { get; set; }
        protected bool HasDefault => DefaultValue != null;

        public bool AllowArbitraryInput { get; set; } = false;
        public Point3D? BasePoint { get; set; }
        public Action<string?>? KeyWordHandler { get; set; }
        protected string[]? Keywords { get; set; }
        protected bool LastInputHandled { get; private set; }

        public InputHelperBase(ICommandContext context, ViewportViewModel? viewModel)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _viewModel = viewModel;
            
            // Cache document reference once
            Document = _context.GetDocument();
        }

        /// <summary>
        /// Format the default value for display in prompt using document's unit settings.
        /// Override in derived classes for type-specific formatting.
        /// </summary>
        /// <param name="formatType">The type of formatting to apply (Linear, Angular, etc.)</param>
        protected virtual string FormatDefaultForPrompt(OpenCADDocument.UnitFormatType formatType)
        {
            if (DefaultValue == null || Document == null)
                return DefaultValue?.ToString() ?? string.Empty;
            
            // Base implementation for double values
            if (DefaultValue is double value)
            {
                return Document.ValueToString(value, formatType);
            }
            
            return DefaultValue.ToString() ?? string.Empty;
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

            // NEW: Check for empty input with default value FIRST (before whitespace check)
            if (string.IsNullOrWhiteSpace(input) && HasDefault)
            {
                // Signal that empty input with default was handled
                HandleMatchedKeyword(string.Empty);
                LastInputHandled = true;
                return false;
            }

            // Now do the original whitespace check for non-default cases
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
            // Don't log empty keywords (used for default value acceptance)
            if (!string.IsNullOrEmpty(keyword))
            {
                _context.OutputMessage($"Keyword: {keyword}");
            }
        }
    }
}