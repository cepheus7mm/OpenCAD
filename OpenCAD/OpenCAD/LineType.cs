using System;

namespace OpenCAD
{
    /// <summary>
    /// Defines the line type (pattern) for drawing lines.
    /// </summary>
    public enum LineType
    {
        /// <summary>
        /// Solid continuous line (default).
        /// </summary>
        Continuous,

        /// <summary>
        /// Dashed line pattern.
        /// </summary>
        Dashed,

        /// <summary>
        /// Dotted line pattern.
        /// </summary>
        Dotted,

        /// <summary>
        /// Dash-dot line pattern.
        /// </summary>
        DashDot,

        /// <summary>
        /// Dash-dot-dot line pattern.
        /// </summary>
        DashDotDot,

        /// <summary>
        /// Center line pattern (long dash, short dash).
        /// </summary>
        Center,

        /// <summary>
        /// Hidden line pattern (short dashes).
        /// </summary>
        Hidden,

        /// <summary>
        /// Phantom line pattern (long dash, short dash, short dash).
        /// </summary>
        Phantom,

        /// <summary>
        /// Use the layer's line type.
        /// </summary>
        ByLayer
    }
}