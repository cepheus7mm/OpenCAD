using OpenCAD;
using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using UI.Commands.Drawing.Dimensions.Linear;

namespace UI.Commands.Interfaces
{
    public interface ILinearDimensionMode
    {
        // Text shown to the user
        string Prompt { get; }

        // Keywords available in this mode
        string[] Keywords { get; }

        // Whether the user can press Enter to reuse the last point
        bool AllowLastPoint { get; }

        // The base point used for dynamic preview and relative input
        Point3D? GetBasePoint();

        // Called when the user provides a point
        void SetPoint(Point3D point);

        // Called when the user provides a numeric value
        void SetDouble(double value);

        // Whether this mode has enough information to transition
        bool IsComplete { get; }

        // Apply the mode's result to the command and return the next mode
        ILinearDimensionMode Apply(LinearDimensionCommand command);

        // Optional: dynamic preview
        IEnumerable<OpenCADObject> GetPreview(LinearDimensionCommand command);

        // The type of input expected from the user
        UserInputType UserInputType { get; }

        // Default value shown when the user presses Enter
        object? GetDefaultValue();

        // Whether arbitrary text input is accepted
        bool AllowArbitraryInput { get; }
        bool CanPreview { get; }

        /// <summary>
        /// Optional projection for converting a picked point into a distance.
        /// Used by Distance input to replace the default Euclidean distance calculation.
        /// </summary>
        Func<Point3D, Point3D, double>? DistanceProjection { get; }
    }
}