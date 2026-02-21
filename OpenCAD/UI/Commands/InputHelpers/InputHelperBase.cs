using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
using UI.Controls.Viewport;
using UI.Helpers;

namespace UI.Commands.InputHelpers
{
    public class InputHelperBase : IInputHelper
    {
        protected readonly ICommandContext _context;
        protected readonly ViewportViewModel? _viewModel;
        protected bool _allowLastPoint; // Store for keyboard input handling
        protected InputParams _inputParams;

        // Cache document reference
        protected OpenCADDocument? Document { get; private set; }
        
        // Make public so composed helpers can set it
        public Action<string?>? KeyWordHandler { get; set; }
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
            if (_inputParams.DefaultValue == null || Document == null)
                return _inputParams.DefaultValue?.ToString() ?? string.Empty;
            
            // Base implementation for double values
            if (_inputParams.DefaultValue is double value)
            {
                return Document.ValueToString(value, formatType);
            }
            
            return _inputParams.DefaultValue.ToString() ?? string.Empty;
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
            if (string.IsNullOrWhiteSpace(input) && _inputParams.DefaultValue != null)
            {
                // Signal that empty input with default was handled
                HandleMatchedKeyword(string.Empty);
                LastInputHandled = true;
                return false;
            }

            // Now do the original whitespace check for non-default cases
            if ((_inputParams.Keywords == null && !_inputParams.AllowArbitraryInput) || string.IsNullOrWhiteSpace(input))
                return false;

            var inputUpper = input.Trim().ToUpperInvariant();
            var matchedKeyword = _inputParams.Keywords?.FirstOrDefault(k =>
                k.ToUpperInvariant() == inputUpper ||
                k.ToUpperInvariant().StartsWith(inputUpper));

            if (matchedKeyword != null || _inputParams.AllowArbitraryInput)
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

        protected void UpdatePreviewIfNeeded(InputResult result)
        {
            if (result.ResultType == InputResult.InputResultType.Point &&
                result.Point.HasValue &&
                _inputParams.BasePoint.HasValue)
            {
                var p = result.Point.Value;
                _context.PostToUI(() =>
                {
                    try { _viewModel.SetPreviewPoint(p); } catch { }
                });
            }
        }

        private Point3D? TryParsePoint(
            string input,
            Point3D? basePoint,
            Point3D? lastPoint)
        {
            // 1. Empty input → last point
            if (string.IsNullOrWhiteSpace(input))
                return lastPoint;

            // 2. Cartesian
            var cart = ParsePoint(input);
            if (cart.HasValue)
                return cart;

            // 3. Polar
            var polar = new PolarInputHelper(input);
            if (polar.IsValid)
            {
                var origin = basePoint ?? lastPoint ?? Point3D.Origin;
                return origin + polar.Vector;
            }

            return null;
        }

        private Point3D? ParsePointInput(string input, bool allowLastPoint)
        {
            var last = allowLastPoint ? _context.GetLastPoint() : null;

            var parsed = TryParsePoint(input, _inputParams.BasePoint, last);

            if (parsed.HasValue)
            {
                _context.SetLastPoint(parsed.Value);
                _context.OutputMessage(
                    string.Format(OpenCADStrings.PointSelectedFormat,
                        parsed.Value.X, parsed.Value.Y, parsed.Value.Z));
                return parsed;
            }

            return Point3D.NotAPoint;
        }

        /// <summary>
        /// Helper method to parse a point from input string
        /// Format: "x y z" or "x,y,z"
        /// </summary>
        protected Point3D? ParsePoint(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            // Try space-separated format
            string[] parts = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 3)
                return null;

            if (double.TryParse(parts[0], out double x) &&
                double.TryParse(parts[1], out double y) &&
                double.TryParse(parts[2], out double z))
            {
                return new Point3D(x, y, z);
            }

            return null;
        }
    }
}