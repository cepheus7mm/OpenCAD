using OpenCAD.Geometry;

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
    }
}