using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Interfaces
{
    public interface IArcCreationMode
    {
        string Prompt { get; }
        UserInputType UserInputType { get; }
        string[] Keywords { get; }
        bool IsComplete { get; }

        Point3D GetBasePoint();
        void SetPoint(Point3D point);
        void SetDouble(double value);
        Arc GetPreview(Point3D cursor);
        Arc CreateArc();
    }
}
