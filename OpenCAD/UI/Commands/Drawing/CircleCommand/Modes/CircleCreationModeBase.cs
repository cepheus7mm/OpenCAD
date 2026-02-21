using OpenCAD.Geometry;
using OpenCAD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.Drawing.CircleCommand.Modes
{
    public abstract class CircleCreationModeBase
    {
        protected readonly OpenCADDocument Document;

        protected CircleCreationModeBase(OpenCADDocument context)
        {
            Document = context;
        }

        // --- PROMPTS & KEYWORDS ----------------------------------------------

        /// <summary>
        /// The prompt shown to the user for the next input.
        /// </summary>
        public abstract string Prompt { get; }

        /// <summary>
        /// Optional keywords available at this step.
        /// </summary>
        public virtual string[] Keywords => Array.Empty<string>();


        // --- INPUT HANDLING ---------------------------------------------------

        public abstract UserInputType UserInputType { get; }


        /// <summary>
        /// Called when the user provides a point.
        /// </summary>
        public abstract void SetPoint(Point3D point);

        /// <summary>
        /// Called when the user enters a keyword.
        /// </summary>
        public virtual void SetKeyword(string keyword)
        {
            // Default: do nothing. Subclasses override if needed.
        }

        public abstract void SetDistance(double distance);


        // --- STATE -------------------------------------------------------------

        /// <summary>
        /// True when this mode has collected all required inputs.
        /// </summary>
        public abstract bool IsComplete { get; }

        /// <summary>
        /// Called by the command when the user moves the mouse.
        /// Subclasses may override to update preview geometry.
        /// </summary>
        public virtual void UpdateDynamicInput(Point3D cursor)
        {
            // Default: no-op
        }


        // --- PREVIEW -----------------------------------------------------------

        /// <summary>
        /// Returns a preview circle if enough information is available.
        /// </summary>
        public abstract Circle? GetPreview(Point3D dragPoint);


        // --- FINAL RESULT ------------------------------------------------------

        /// <summary>
        /// Creates the final circle entity.
        /// </summary>
        public abstract Circle CreateCircle();
    }
}
