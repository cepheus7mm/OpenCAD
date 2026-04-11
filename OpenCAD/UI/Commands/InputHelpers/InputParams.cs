using OpenCAD.Geometry;
using UI.Commands.Interfaces;
using static OpenCAD.OpenCADDocument;

namespace UI.Commands.InputHelpers
{
    public class InputParams
    {
        public string Prompt { get; set; } = string.Empty;
        public object? DefaultValue { get; set; }
        public bool AllowLastPoint { get; set; }
        public bool AllowArbitraryInput { get; set; }
        public Point3D? BasePoint { get; set; }
        public string[]? Keywords { get; set; }
        public CancellationToken CancellationToken { get; set; } = default;
        public ICommandContext? Context { get; set; } = null;
        public UnitFormatType? UnitFormatType { get; set; }

        /// <summary>
        /// Optional custom projection for converting a picked point into a distance.
        /// When set, <see cref="GetDistanceInput"/> calls this instead of
        /// <c>basePoint.DistanceTo(pickedPoint)</c>, allowing callers to project
        /// onto a specific axis or normal.
        /// </summary>
        public Func<Point3D, Point3D, double>? DistanceProjection { get; set; }
    }
}