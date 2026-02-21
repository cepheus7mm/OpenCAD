using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Commands.Drawing.LineCommand;

namespace UI.Commands.Interfaces
{
    public interface ILineCreationMode
    {
        string Prompt { get; }
        string[] Keywords { get; }

        bool IsComplete { get; }

        void SetPoint(Point3D p);
        Point3D GetBasePoint();

        IEnumerable<OpenCADObject> GetPreview(Point3D cursor);

        /// <summary>
        /// Apply the result of this mode to the polyline or document.
        /// Returns the next mode to switch to.
        /// </summary>
        ILineCreationMode Apply(LineCommand command);
    }
}
