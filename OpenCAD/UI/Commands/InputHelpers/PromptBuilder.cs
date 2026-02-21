using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.InputHelpers
{
    public static class PromptBuilder
    {
        public static string Build(InputParams inputParams, Point3D? lastPoint)
            
        {
            string display = inputParams.Prompt;
            var keywords = inputParams.Keywords ?? Array.Empty<string>();

            if (keywords != null && keywords.Length > 0)
                display += $" [{string.Join("/", keywords)}]";

            if (inputParams.AllowLastPoint && lastPoint != null)
            {
                return string.Format(
                    OpenCADStrings.PromptWithLastPointFormat,
                    display,
                    lastPoint.Value.X,
                    lastPoint.Value.Y,
                    lastPoint.Value.Z);
            }

            return string.Format(OpenCADStrings.PromptWithViewportFormat, display);
        }
    }
}
