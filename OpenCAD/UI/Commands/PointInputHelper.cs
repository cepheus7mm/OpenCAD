using OpenCAD.Geometry;
using System;
using System.Threading;
using System.Threading.Tasks;
using UI.Controls.Viewport;

namespace UI.Commands
{
    /// <summary>
    /// Helper class to get point input from user via keyboard coordinates or mouse click
    /// </summary>
    public class PointInputHelper
    {
        private readonly ICommandContext _context;
        private readonly ViewportControl? _viewport;
        private TaskCompletionSource<Point3D?>? _pointTaskSource;
        private EventHandler<PointPickedEventArgs>? _pointPickedHandler;

        public PointInputHelper(ICommandContext context, ViewportControl? viewport)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _viewport = viewport;
        }

        /// <summary>
        /// Get a point from the user via keyboard input or mouse click
        /// </summary>
        /// <param name="prompt">Prompt message to display</param>
        /// <param name="allowLastPoint">Whether to allow using the last point with empty input</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The point selected by the user, or null if cancelled</returns>
        public async Task<Point3D?> GetPointAsync(string prompt, bool allowLastPoint = false, CancellationToken cancellationToken = default)
        {
            // Display prompt
            if (allowLastPoint)
            {
                var lastPoint = _context.GetLastPoint();
                if (lastPoint != null)
                {
                    _context.OutputMessage($"{prompt} (or press Enter for last point {lastPoint.X:F3}, {lastPoint.Y:F3}, {lastPoint.Z:F3}):");
                }
                else
                {
                    _context.OutputMessage($"{prompt} (or click in viewport):");
                }
            }
            else
            {
                _context.OutputMessage($"{prompt} (or click in viewport):");
            }

            // If viewport is available, enable mouse picking
            if (_viewport != null)
            {
                System.Diagnostics.Debug.WriteLine("PointInputHelper: Enabling point picking mode");
                _pointTaskSource = new TaskCompletionSource<Point3D?>();

                // Set up point picked handler
                _pointPickedHandler = (sender, e) => OnPointPicked(e.Point);
                _viewport.PointPicked += _pointPickedHandler;
                
                // Enable picking mode
                _viewport.EnablePointPickingMode();

                try
                {
                    // Wait for either mouse click or cancellation
                    using (cancellationToken.Register(() => _pointTaskSource?.TrySetCanceled()))
                    {
                        return await _pointTaskSource.Task;
                    }
                }
                finally
                {
                    System.Diagnostics.Debug.WriteLine("PointInputHelper: Cleaning up point picking");
                    
                    // Disable picking mode
                    _viewport.DisablePointPickingMode();
                    
                    // Clean up event handler
                    if (_pointPickedHandler != null)
                    {
                        _viewport.PointPicked -= _pointPickedHandler;
                        _pointPickedHandler = null;
                    }
                    _pointTaskSource = null;
                }
            }

            // If no viewport, return null (will rely on keyboard input)
            return null;
        }

        /// <summary>
        /// Parse point from string input (synchronous, for keyboard entry)
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="allowLastPoint">Whether to allow using the last point with empty input</param>
        /// <returns>Parsed point, or null if invalid</returns>
        public Point3D? ParsePointInput(string input, bool allowLastPoint = false)
        {
            // Handle empty input - use last point if allowed
            if (string.IsNullOrWhiteSpace(input) && allowLastPoint)
            {
                return _context.GetLastPoint();
            }

            // Parse coordinates
            return ParsePoint(input);
        }

        /// <summary>
        /// Cancel any pending point input
        /// </summary>
        public void Cancel()
        {
            System.Diagnostics.Debug.WriteLine("PointInputHelper: Cancel called");
            
            if (_pointTaskSource != null)
            {
                _pointTaskSource.TrySetResult(null);
                
                // Disable picking mode
                _viewport?.DisablePointPickingMode();
                
                // Clean up event handler
                if (_viewport != null && _pointPickedHandler != null)
                {
                    _viewport.PointPicked -= _pointPickedHandler;
                    _pointPickedHandler = null;
                }
                
                _pointTaskSource = null;
            }
        }

        private void OnPointPicked(Point3D point)
        {
            System.Diagnostics.Debug.WriteLine($"PointInputHelper: Point picked ({point.X:F3}, {point.Y:F3}, {point.Z:F3})");
            
            // Set the last point
            _context.SetLastPoint(point);
            
            // Output the point
            _context.OutputMessage($"Point selected: ({point.X:F3}, {point.Y:F3}, {point.Z:F3})");
            
            // Complete the task
            _pointTaskSource?.TrySetResult(point);
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