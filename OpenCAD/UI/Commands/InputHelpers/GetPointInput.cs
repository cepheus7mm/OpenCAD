using Newtonsoft.Json.Linq;
using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;
using UI.Helpers;

namespace UI.Commands.InputHelpers
{
    /// <summary>
    /// Helper class to get point input from user via keyboard coordinates or mouse click
    /// Uses ViewportViewModel for separation of concerns and better testability
    /// </summary>
    public class GetPointInput : InputHelperBase
    {
        private EventHandler<PointPickedEventArgs>? _pointPickedHandler;
        private EventHandler? _pointPickingCancelledHandler;
        private Point3D? _basePoint; // For preview line from base point
        private bool _allowLastPoint; // Store for keyboard input handling
        private TaskCompletionSource<InputResult>? _pointOrKeywordTaskSource;

        public GetPointInput(ICommandContext context, ViewportViewModel? viewModel) 
            : base(context, viewModel)
        {
        }

        /// <summary>
        /// Get a point from the user via keyboard input or mouse click
        /// </summary>
        public async Task<Point3D?> GetPointAsync(
            string prompt, 
            bool allowLastPoint = false, 
            Point3D? basePoint = null,
            CancellationToken cancellationToken = default)
        {
            var result = await GetPointOrKeywordAsync(prompt, null, allowLastPoint, basePoint, null, cancellationToken);
            return result.Point;
        }

        /// <summary>
        /// Get a point or keyword from the user via keyboard input or mouse click
        /// </summary>
        /// <param name="defaultValue">Optional default value (for use by composed helpers like GetDistanceInput)</param>
        public async Task<InputResult> GetPointOrKeywordAsync(
            string prompt,
            object? defaultValue = null, // NEW: Accept default value (could be Point3D, double, etc.)
            bool allowLastPoint = false,
            Point3D? basePoint = null,
            string[]? keywords = null,
            CancellationToken cancellationToken = default)
        {
            _basePoint = basePoint;
            _allowLastPoint = allowLastPoint;
            
            // NEW: Store default value in base class
            DefaultValue = defaultValue;

            // Use base.Keywords so base class can handle keyword matching
            this.Keywords = keywords;

            // Build display prompt including keywords if provided
            string displayPrompt = prompt ?? string.Empty;
            if (keywords != null && keywords.Length > 0)
            {
                var kw = string.Join("/", keywords);
                displayPrompt = $"{displayPrompt} [{kw}]";
            }

            // Build the actual prompt string shown on command pane
            string formattedPrompt;
            if (allowLastPoint)
            {
                var lastPoint = _context.GetLastPoint();
                if (lastPoint != null)
                {
                    formattedPrompt = string.Format(
                            OpenCADStrings.PromptWithLastPointFormat,
                            displayPrompt,
                            lastPoint.Value.X,
                            lastPoint.Value.Y,
                            lastPoint.Value.Z);
                }
                else
                {
                    formattedPrompt = string.Format(
                            OpenCADStrings.PromptWithViewportFormat,
                            displayPrompt);
                }
            }
            else
            {
                formattedPrompt = string.Format(
                        OpenCADStrings.PromptWithViewportFormat,
                        displayPrompt);
            }

            // Prepare TCS (run continuations asynchronously to avoid reentrancy on UI thread)
            _pointOrKeywordTaskSource = new TaskCompletionSource<InputResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            // If viewport not available, return Cancel quickly (but still set prompt on UI)
            if (_viewModel == null)
            {
                // show prompt for consistency
                _context.PostToUI(() => _context.SetCommandPrompt(formattedPrompt));
                return new InputResult { ResultType = InputResult.InputResultType.Cancel };
            }

            // Show prompt and subscribe / enable picking on UI thread
            _context.PostToUI(() =>
            {
                try
                {
                    _context.SetCommandPrompt(formattedPrompt);

                    // Point picked handler
                    _pointPickedHandler = (sender, e) => OnPointPicked(e.Point);
                    _viewModel.PointPicked += _pointPickedHandler;

                    // Cancelled handler
                    _pointPickingCancelledHandler = (s, e) => _pointOrKeywordTaskSource?.TrySetResult(new InputResult { ResultType = InputResult.InputResultType.Cancel });
                    _viewModel.PointPickingCancelled += _pointPickingCancelledHandler;

                    // Enable picking mode
                    _viewModel.EnablePointPickingMode();

                    // Setup preview mode if a base point was provided
                    if (_basePoint != null)
                    {
                        _viewModel.ClearTempPoints();
                        _viewModel.AddTempPoint(_basePoint.Value);
                        _viewModel.EnablePreviewMode(OnPreviewPointChanged);
                    }
                }
                catch
                {
                    // swallow UI setup exceptions; TCS still usable
                }
            });

            // Build a KeyWordHandler that updates the visible prompt then completes the task.
            KeyWordHandler = (kw) =>
            {
                try
                {
                    // Update UI prompt with chosen keyword (marshal to UI)
                    var promptWithKeyword = $"{formattedPrompt} {kw}";
                    _context.PostToUI(() =>
                    {
                        try { _context.SetCommandPrompt(promptWithKeyword); } catch { }
                    });

                    // Complete TCS with keyword result (TrySetResult to avoid races)
                    _pointOrKeywordTaskSource?.TrySetResult(new InputResult
                    {
                        ResultType = InputResult.InputResultType.Keyword,
                        Keyword = kw
                    });
                }
                catch
                {
                    // ignore
                }
            };

            try
            {
                // Wait for either mouse click, keyboard input, or cancellation
                using (cancellationToken.Register(() => _pointOrKeywordTaskSource?.TrySetResult(new InputResult { ResultType = InputResult.InputResultType.Cancel })))
                {
                    return await _pointOrKeywordTaskSource.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                // Cleanup must happen on UI thread to safely unsubscribe and disable modes
                _context.PostToUI(() =>
                {
                    try
                    {
                        // Disable preview mode and clear temp points
                        if (_basePoint != null)
                        {
                            try { _viewModel.DisablePreviewMode(); } catch { }
                            try { _viewModel.ClearTempPoints(); } catch { }
                        }

                        // Disable picking mode
                        try { _viewModel.DisablePointPickingMode(); } catch { }

                        // Unsubscribe handlers
                        if (_pointPickedHandler != null)
                        {
                            try { _viewModel.PointPicked -= _pointPickedHandler; } catch { }
                            _pointPickedHandler = null;
                        }

                        if (_pointPickingCancelledHandler != null)
                        {
                            try { _viewModel.PointPickingCancelled -= _pointPickingCancelledHandler; } catch { }
                            _pointPickingCancelledHandler = null;
                        }

                        // Clear prompt
                        try { _context.SetCommandPrompt(string.Empty); } catch { }
                    }
                    catch
                    {
                        // swallow cleanup exceptions
                    }
                    finally
                    {
                        _pointOrKeywordTaskSource = null;
                        _basePoint = null;
                        _allowLastPoint = false;
                        DefaultValue = null; // NEW: Clear default value
                        this.Keywords = null;
                        KeyWordHandler = null;
                    }
                });
            }
        }

        /// <summary>
        /// Process keyboard input and complete the async task if one is pending
        /// </summary>
        public override bool ProcessKeyboardInput(string input)
        {
            // If there's no pending task, return false
            if (_pointOrKeywordTaskSource == null)
                return false;

            // NEW: Check for empty input with default value (for composed helpers like GetDistanceInput)
            if (string.IsNullOrWhiteSpace(input) && HasDefault)
            {
                // Signal default acceptance with empty keyword
                // The composed helper (GetDistanceInput) will recognize this and return the default
                _pointOrKeywordTaskSource.TrySetResult(new InputResult
                {
                    ResultType = InputResult.InputResultType.Keyword,
                    Keyword = string.Empty
                });
                return false;
            }

            if (AllowArbitraryInput && !string.IsNullOrWhiteSpace(input))
            {
                // Accept any arbitrary input as a keyword
                _pointOrKeywordTaskSource.TrySetResult(new InputResult
                {
                    ResultType = InputResult.InputResultType.Arbitrary,
                    Keyword = input
                });
                return false;
            }

            // Let base class handle keywords first
            base.ProcessKeyboardInput(input);
            if (LastInputHandled)
            {
                // base already invoked KeyWordHandler which updated prompt and completed the TCS.
                return true;
            }

            // Parse the input as a point
            var point = ParsePointInput(input, _allowLastPoint);
            
            // If we got a valid point and there's a base point (rubberband mode),
            // set the preview point (must be on UI thread)
            if (point != null && _basePoint != null && point != Point3D.NotAPoint)
            {
                _context.PostToUI(() =>
                {
                    try { _viewModel.SetPreviewPoint(point); } catch { }
                });
            }
            if (point.HasValue && AllowArbitraryInput)
            {
                _pointOrKeywordTaskSource.TrySetResult(new InputResult { ResultType = InputResult.InputResultType.Arbitrary, Keyword = input });
            }

            // Complete the task with the result
            if (point.HasValue)
            {
                _pointOrKeywordTaskSource.TrySetResult(new InputResult { Point = point, ResultType = InputResult.InputResultType.Point });
            }
            else
            {
                _pointOrKeywordTaskSource.TrySetResult(new InputResult { ResultType = InputResult.InputResultType.Cancel });
            }
            
            return true;
        }

        /// <summary>
        /// Parse point from string input (synchronous, for keyboard entry)
        /// </summary>
        private Point3D? ParsePointInput(string input, bool allowLastPoint = false)
        {
            // Handle empty input - use last point if allowed
            if (string.IsNullOrWhiteSpace(input) && allowLastPoint)
            {
                var lastPoint = _context.GetLastPoint();
                
                // Output the last point being used
                if (lastPoint != null)
                {
                    _context.OutputMessage(
                        string.Format(
                            OpenCADStrings.PointSelectedFormat,
                            lastPoint.Value.X,
                            lastPoint.Value.Y,
                            lastPoint.Value.Z));
                }
                
                return lastPoint;
            }

            // Parse coordinates
            var parsedPoint = ParsePoint(input);

            if (parsedPoint == null) 
            {
                var polarInputHelper = new PolarInputHelper(input);
                if(polarInputHelper.IsValid)
                {
                    Point3D basePoint = _basePoint ?? _context.GetLastPoint() ?? new Point3D(0, 0, 0);
                    parsedPoint = basePoint + polarInputHelper.Vector;
                }
            }

            // If we successfully parsed a point, set it as the last point
            if (parsedPoint.HasValue)
            {
                _context.SetLastPoint(parsedPoint.Value);
                
                // Output the point
                _context.OutputMessage(
                    string.Format(
                        OpenCADStrings.PointSelectedFormat,
                        parsedPoint.Value.X,
                        parsedPoint.Value.Y,
                        parsedPoint.Value.Z));
            }
            else
            {
                parsedPoint = Point3D.NotAPoint;
            }

            return parsedPoint;
        }

        /// <summary>
        /// Cancel any pending point input
        /// </summary>
        public void Cancel()
        {
            System.Diagnostics.Debug.WriteLine(OpenCADStrings.PointInputHelperCancelCalled);
            
            if (_pointOrKeywordTaskSource != null)
            {
                _pointOrKeywordTaskSource.TrySetResult(new InputResult { ResultType = InputResult.InputResultType.Cancel });
                
                // Run UI cleanup via PostToUI to avoid cross-thread access
                _context.PostToUI(() =>
                {
                    if (_viewModel != null && _basePoint != null)
                    {
                        try { _viewModel.DisablePreviewMode(); } catch { }
                        try { _viewModel.ClearTempPoints(); } catch { }
                    }

                    try { _viewModel?.DisablePointPickingMode(); } catch { }

                    if (_viewModel != null && _pointPickedHandler != null)
                    {
                        try { _viewModel.PointPicked -= _pointPickedHandler; } catch { }
                        _pointPickedHandler = null;
                    }

                    if (_viewModel != null && _pointPickingCancelledHandler != null)
                    {
                        try { _viewModel.PointPickingCancelled -= _pointPickingCancelledHandler; } catch { }
                        _pointPickingCancelledHandler = null;
                    }

                    try { _context.SetCommandPrompt(string.Empty); } catch { }
                });

                _pointOrKeywordTaskSource = null;
                _basePoint = null;
                _allowLastPoint = false;
                DefaultValue = null; // NEW: Clear default value
                this.Keywords = null;
                KeyWordHandler = null;
            }
        }

        private void OnPointPicked(Point3D point)
        {
            System.Diagnostics.Debug.WriteLine(
                string.Format(
                    OpenCADStrings.PointInputHelperPointPickedFormat,
                    point.X,
                    point.Y,
                    point.Z));
            
            // Set the last point
            _context.SetLastPoint(point);
            
            // Output the point
            _context.OutputMessage(
                string.Format(
                    OpenCADStrings.PointSelectedFormat,
                    point.X,
                    point.Y,
                    point.Z));
            
            // Complete the task with the point
            _pointOrKeywordTaskSource?.TrySetResult(new InputResult { Point = point, ResultType = InputResult.InputResultType.Point });
        }

        private void OnPreviewPointChanged(Point3D previewPoint)
        {
            // Update the viewport's preview point for rubberband line rendering
            // marshal to UI to be safe
            _context.PostToUI(() =>
            {
                try { _viewModel?.SetPreviewPoint(previewPoint); } catch { }
            });
            
            //System.Diagnostics.Debug.WriteLine(
            //    string.Format(
            //        OpenCADStrings.LineCommandPreviewPointUpdated,
            //        previewPoint.X,
            //        previewPoint.Y,
            //        previewPoint.Z));
        }

        /// <summary>
        /// Helper method to parse a point from input string
        /// Format: "x y z" or "x,y,z"
        /// </summary>
        private Point3D? ParsePoint(string input)
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