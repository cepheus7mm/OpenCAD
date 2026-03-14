using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Drawing;
using UI.Commands.Drawing.PolylineCreation;

namespace UI.Commands.Interfaces
{
    public interface IPolylineCreationMode
    {
        // Text shown to the user
        string Prompt { get; }

        // Keywords available in this mode (A, C, U, Width, etc.)
        string[] Keywords { get; }

        // Whether the user can press Enter to reuse the last point
        bool AllowLastPoint { get; }

        // The base point used for dynamic preview and relative input
        Point3D? GetBasePoint();

        // Called when the user provides a point
        void SetPoint(Point3D point);

        // Whether this mode has enough information to transition
        bool IsComplete { get; }

        // Apply the mode’s result to the command and return the next mode
        IPolylineCreationMode Apply(PolylineCommand command);

        // Optional: dynamic preview
        IEnumerable<IDrawable> GetPreview(PolylineCommand command);

        UserInputType UserInputType { get; }

        void SetDouble(double value);

        object? GetDefaultValue();

        bool AllowArbitraryInput { get; }
    }
}
