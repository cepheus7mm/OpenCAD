using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;
using UI.Helpers;

namespace UI.Commands
{
    /// <summary>
    /// Result of a point or keyword input request
    /// </summary>
    public class PointOrKeywordResult
    {
        public Point3D? Point { get; set; }
        public string? Keyword { get; set; }
        public bool IsCancelled { get; set; }
        
        public bool IsKeyword => !string.IsNullOrEmpty(Keyword);
        public bool IsPoint => Point != null;
    }

    /// <summary>
    /// Helper class to get point input from user via keyboard coordinates or mouse click
    /// Uses ViewportViewModel for separation of concerns and better testability
    /// </summary>
    public class PointInputHelper
    {
        private readonly ICommandContext _context;
        private readonly ViewportViewModel? _viewModel;
        private TaskCompletionSource<PointOrKeywordResult>? _pointOrKeywordTaskSource;
        private EventHandler<PointPickedEventArgs>? _pointPickedHandler;
        private EventHandler? _pointPickingCancelledHandler; // <-- ADDED
        private Point3D? _basePoint; // For preview line from base point
        private bool _allowLastPoint; // Store for keyboard input handling
        private string[]? _keywords; // Valid keywords for this input

        public PointInputHelper(ICommandContext context, ViewportViewModel? viewModel)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _viewModel = viewModel;
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
            var result = await GetPointOrKeywordAsync(prompt, allowLastPoint, basePoint, null, cancellationToken);
            return result.Point;
        }

        /// <summary>
        /// Get a point or keyword from the user via keyboard input or mouse click
        /// </summary>
        /// <param name="prompt">Prompt message to display</param>
        /// <param name="allowLastPoint">Whether to allow using the last point with empty input</param>
        /// <param name="basePoint">Base point for preview/rubberband line (optional)</param>
        /// <param name="keywords">Valid keywords that can be entered (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result containing either a point, keyword, or cancellation status</returns>
        public async Task<PointOrKeywordResult> GetPointOrKeywordAsync(
            string prompt,
            bool allowLastPoint = false,
            Point3D? basePoint = null,
            string[]? keywords = null,
            CancellationToken cancellationToken = default)
        {
            _basePoint = basePoint;
            _allowLastPoint = allowLastPoint;
            _keywords = keywords;

            // Display prompt
            if (allowLastPoint)
            {
                var lastPoint = _context.GetLastPoint();
                if (lastPoint != null)
                {
                    _context.OutputMessage(
                        string.Format(
                            OpenCADStrings.PromptWithLastPointFormat,
                            prompt,
                            lastPoint.X,
                            lastPoint.Y,
                            lastPoint.Z));
                }
                else
                {
                    _context.OutputMessage(
                        string.Format(
                            OpenCADStrings.PromptWithViewportFormat,
                            prompt));
                }
            }
            else
            {
                _context.OutputMessage(
                    string.Format(
                        OpenCADStrings.PromptWithViewportFormat,
                        prompt));
            }

            // If viewport is available, enable mouse picking
            if (_viewModel != null)
            {
                System.Diagnostics.Debug.WriteLine(OpenCADStrings.PointInputHelperEnablingPickingMode);
                _pointOrKeywordTaskSource = new TaskCompletionSource<PointOrKeywordResult>();

                // Set up point picked handler
                _pointPickedHandler = (sender, e) => OnPointPicked(e.Point);
                _viewModel.PointPicked += _pointPickedHandler;
                
                // Set up cancellation via viewport point-picking cancelled (ESC/right-click)
                _pointPickingCancelledHandler = (s, e) => _pointOrKeywordTaskSource?.TrySetResult(new PointOrKeywordResult { IsCancelled = true });
                _viewModel.PointPickingCancelled += _pointPickingCancelledHandler;

                // Enable picking mode
                _viewModel.EnablePointPickingMode();

                // Enable preview mode if we have a base point (for rubberband line)
                if (_basePoint != null)
                {
                    System.Diagnostics.Debug.WriteLine(OpenCADStrings.LineCommandEnablingPreviewMode);
                    
                    // Add base point to temp points for preview rendering
                    _viewModel.ClearTempPoints();
                    _viewModel.AddTempPoint(_basePoint);
                    
                    // Enable preview mode with callback
                    _viewModel.EnablePreviewMode(OnPreviewPointChanged);
                }

                try
                {
                    // Wait for either mouse click, keyboard input, or cancellation
                    using (cancellationToken.Register(() => _pointOrKeywordTaskSource?.TrySetResult(new PointOrKeywordResult { IsCancelled = true })))
                    {
                        return await _pointOrKeywordTaskSource.Task;
                    }
                }
                finally
                {
                    System.Diagnostics.Debug.WriteLine(OpenCADStrings.PointInputHelperCleaningUp);
                    
                    // Disable preview mode
                    if (_basePoint != null)
                    {
                        _viewModel.DisablePreviewMode();
                        _viewModel.ClearTempPoints();
                    }
                    
                    // Disable picking mode
                    _viewModel.DisablePointPickingMode();
                    
                    // Clean up event handler
                    if (_pointPickedHandler != null)
                    {
                        _viewModel.PointPicked -= _pointPickedHandler;
                        _pointPickedHandler = null;
                    }

                    // Clean up point-picking cancelled handler
                    if (_pointPickingCancelledHandler != null)
                    {
                        _viewModel.PointPickingCancelled -= _pointPickingCancelledHandler;
                        _pointPickingCancelledHandler = null;
                    }
                    
                    _pointOrKeywordTaskSource = null;
                    _basePoint = null;
                    _allowLastPoint = false;
                    _keywords = null;
                }
            }

            // If no viewport, return cancelled
            return new PointOrKeywordResult { IsCancelled = true };
        }

        /// <summary>
        /// Process keyboard input and complete the async task if one is pending
        /// </summary>
        public bool ProcessKeyboardInput(string input)
        {
            // If there's no pending task, return false
            if (_pointOrKeywordTaskSource == null)
                return false;

            // Check if input matches a keyword
            if (_keywords != null && !string.IsNullOrWhiteSpace(input))
            {
                var inputUpper = input.Trim().ToUpperInvariant();
                var matchedKeyword = _keywords.FirstOrDefault(k => 
                    k.ToUpperInvariant() == inputUpper || 
                    k.ToUpperInvariant().StartsWith(inputUpper));

                if (matchedKeyword != null)
                {
                    _context.OutputMessage($"Keyword: {matchedKeyword}");
                    _pointOrKeywordTaskSource.TrySetResult(new PointOrKeywordResult { Keyword = matchedKeyword });
                    return false;
                }
            }

            // Parse the input as a point
            var point = ParsePointInput(input, _allowLastPoint);
            
            // If we got a valid point and there's a base point (rubberband mode),
            // set the preview point
            if (point != null && _basePoint != null && _viewModel != null)
            {
                _viewModel.SetPreviewPoint(point);
            }
            
            // Complete the task with the result
            if (point != null)
            {
                _pointOrKeywordTaskSource.TrySetResult(new PointOrKeywordResult { Point = point });
            }
            else
            {
                _pointOrKeywordTaskSource.TrySetResult(new PointOrKeywordResult { IsCancelled = true });
            }
            
            return false;
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
                            lastPoint.X,
                            lastPoint.Y,
                            lastPoint.Z));
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
            if (parsedPoint != null)
            {
                _context.SetLastPoint(parsedPoint);
                
                // Output the point
                _context.OutputMessage(
                    string.Format(
                        OpenCADStrings.PointSelectedFormat,
                        parsedPoint.X,
                        parsedPoint.Y,
                        parsedPoint.Z));
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
                _pointOrKeywordTaskSource.TrySetResult(new PointOrKeywordResult { IsCancelled = true });
                
                // Disable preview mode
                if (_viewModel != null && _basePoint != null)
                {
                    _viewModel.DisablePreviewMode();
                    _viewModel.ClearTempPoints();
                }
                
                // Disable picking mode
                _viewModel?.DisablePointPickingMode();
                
                // Clean up event handler
                if (_viewModel != null && _pointPickedHandler != null)
                {
                    _viewModel.PointPicked -= _pointPickedHandler;
                    _pointPickedHandler = null;
                }

                // Clean up point-picking cancelled handler
                if (_viewModel != null && _pointPickingCancelledHandler != null)
                {
                    _viewModel.PointPickingCancelled -= _pointPickingCancelledHandler;
                    _pointPickingCancelledHandler = null;
                }
                
                _pointOrKeywordTaskSource = null;
                _basePoint = null;
                _allowLastPoint = false;
                _keywords = null;
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
            _pointOrKeywordTaskSource?.TrySetResult(new PointOrKeywordResult { Point = point });
        }

        private void OnPreviewPointChanged(Point3D previewPoint)
        {
            // Update the viewport's preview point for rubberband line rendering
            _viewModel?.SetPreviewPoint(previewPoint);
            
            System.Diagnostics.Debug.WriteLine(
                string.Format(
                    OpenCADStrings.LineCommandPreviewPointUpdated,
                    previewPoint.X,
                    previewPoint.Y,
                    previewPoint.Z));
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