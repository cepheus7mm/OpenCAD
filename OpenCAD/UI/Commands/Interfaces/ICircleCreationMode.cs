using OpenCAD;
using OpenCAD.Geometry;

namespace UI.Commands.Interfaces
{
    public interface ICircleCreationMode
    {
        string Prompt { get; }
        string[] Keywords { get; }

        // Called when user provides a point
        void SetPoint(Point3D p);

        // Called when user provides a keyword
        void SetKeyword(string keyword);

        // Whether the mode is finished
        bool IsComplete { get; }

        // Preview geometry
        Circle? GetPreview(Point3D dragPoint);

        // Final geometry
        Circle CreateCircle();

        Point3D GetBasePoint();
    }
}
