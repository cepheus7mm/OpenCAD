using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Editing.Fillet
{
    public sealed class FilletState
    {
        // --- SETTINGS ---------------------------------------------------------

        /// <summary>
        /// The fillet radius. Persistent across command invocations.
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// Whether to trim the original curves.
        /// </summary>
        public bool Trim { get; set; } = true;


        // --- USER SELECTION ---------------------------------------------------

        /// <summary>
        /// The first selected object (line, arc, circle, polyline segment).
        /// </summary>
        public OpenCADObject? FirstObject { get; set; }

        /// <summary>
        /// The second selected object.
        /// </summary>
        public OpenCADObject? SecondObject { get; set; }

        public Point3D? FirstPickPoint { get; set; }
        public Point3D? SecondPickPoint { get; set; }


        // --- INTERNAL STATE ---------------------------------------------------

        /// <summary>
        /// Tracks which step the user is currently in.
        /// </summary>
        public FilletStep Step { get; set; } = FilletStep.PromptRadius;

        /// <summary>
        /// Stores the last hover target for dynamic preview.
        /// </summary>
        public OpenCADObject? HoverObject { get; set; }

        /// <summary>
        /// True if the solver produced a valid preview solution.
        /// </summary>
        public bool HasPreview { get; set; }
    }
}
