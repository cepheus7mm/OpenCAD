using Newtonsoft.Json.Linq;
using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UI.Commands.Interfaces;
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
        private TaskCompletionSource<InputResult>? _pointOrKeywordTaskSource;

        public GetPointInput(ICommandContext context, ViewportViewModel? viewModel) 
            : base(context, viewModel)
        {
        }

        /// <summary>
        /// Get a point or keyword from the user via keyboard input or mouse click
        /// </summary>
        /// <param name="defaultValue">Optional default value (for use by composed helpers like GetDistanceInput)</param>
        public async Task<InputResult> GetPointOrKeywordAsync(InputParams inputParams)
        {
            _inputParams = inputParams;
            string formattedPrompt = PromptBuilder.Build(inputParams, _context.GetLastPoint());

            _context.PostToUI(() => _context.SetCommandPrompt(formattedPrompt));

            if (_viewModel == null)
                return InputResult.Cancel;

            var controller = new InputTaskController();
            controller.AttachCancellation(_inputParams.CancellationToken);

            using var vp = new ViewportInteraction(
                _viewModel,
                _inputParams.BasePoint,
                p => controller.CompleteWithPoint(p),
                () => controller.CompleteWithCancel(),
                OnPreviewPointChanged);

            KeyWordHandler = kw => controller.CompleteWithKeyword(kw);

            _pointOrKeywordTaskSource = controller.TaskCompletionSource;

            var result = await controller.Task.ConfigureAwait(false);

            //CleanupAfterInput();

            return result;
        }

        /// <summary>
        /// Process keyboard input and complete the async task if one is pending
        /// </summary>
        public override bool ProcessKeyboardInput(string input)
        {
            if (_pointOrKeywordTaskSource == null)
                return false;

            // Interpret the input using the unified helper
            var result = InterpretKeyboardInput(input);

            // Update preview if needed
            UpdatePreviewIfNeeded(result);

            // Complete the task
            _pointOrKeywordTaskSource.TrySetResult(result);

            return true;

        }

        private InputResult InterpretKeyboardInput(string input)
        {
            // 1. Default acceptance
            if (string.IsNullOrWhiteSpace(input) && _inputParams.DefaultValue != null)
                return InputResult.Default;

            // 2. Arbitrary input
            if (_inputParams.AllowArbitraryInput && !string.IsNullOrWhiteSpace(input))
                return InputResult.FromArbitrary(input);

            // 3. Keyword input (handled by base)
            base.ProcessKeyboardInput(input);
            if (LastInputHandled)
                return InputResult.FromKeyword(input);

            // 4. Point input
            var point = ParsePointInput(input, _allowLastPoint);
            if (point.HasValue)
                return InputResult.FromPoint(point.Value);

            // 5. Invalid → cancel
            return InputResult.Cancel;
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
        }

    }
}