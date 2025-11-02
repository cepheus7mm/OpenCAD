using System;

namespace OpenCAD
{
    /// <summary>
    /// Extension methods for LineWeight conversion and utilities.
    /// </summary>
    public static class LineWeightExtensions
    {
        /// <summary>
        /// Converts a LineWeight enum value to OpenGL line width (0.5 to 10.0 pixels).
        /// </summary>
        public static float ToOpenGLWidth(this LineWeight lineWeight)
        {
            return lineWeight switch
            {
                LineWeight.ByLayer => 2.5f, // Default fallback (0.25mm equivalent)
                LineWeight.Default => 2.5f,  // 0.25mm -> 2.5 pixels
                LineWeight.Hairline => 0.5f, // Minimum OpenGL line width
                _ => Math.Clamp((int)lineWeight / 10.0f, 0.5f, 10.0f)
            };
        }

        /// <summary>
        /// Converts a LineWeight to millimeters for display.
        /// </summary>
        public static float ToMillimeters(this LineWeight lineWeight)
        {
            return lineWeight switch
            {
                LineWeight.ByLayer => 0.25f,
                LineWeight.Default => 0.25f,
                LineWeight.Hairline => 0.0f,
                _ => (int)lineWeight / 100.0f
            };
        }

        /// <summary>
        /// Converts a LineWeight to a display-friendly string.
        /// </summary>
        public static string ToDisplayString(this LineWeight lineWeight)
        {
            return lineWeight switch
            {
                LineWeight.ByLayer => "By Layer",
                LineWeight.Default => "Default (0.25mm)",
                LineWeight.Hairline => "Hairline",
                _ => $"{lineWeight.ToMillimeters():F2}mm"
            };
        }
    }
}