using System.Drawing;

namespace OpenCAD.Interfaces
{
    /// <summary>
    /// Represents an object that can be drawn with visual properties.
    /// Provides access to layer, color, line type, and line weight.
    /// </summary>
    public interface IDrawable
    {
        /// <summary>
        /// Gets the layer object this drawable is on.
        /// </summary>
        OpenCADLayer Layer { get; set; }

        /// <summary>
        /// Gets the effective color for rendering this object.
        /// If the object has a color override, returns it; otherwise returns the layer's color.
        /// </summary>
        /// <param name="layer">The layer to use for ByLayer resolution.</param>
        Color Color { get; set; }

        /// <summary>
        /// Gets the effective line type for rendering this object.
        /// If the object has a line type override, returns it; otherwise returns the layer's line type.
        /// </summary>
        /// <param name="layer">The layer to use for ByLayer resolution.</param>
        LineType LineType { get; set; }

        /// <summary>
        /// Gets the effective line weight for rendering this object.
        /// If the object has a line weight override, returns it; otherwise returns the layer's line weight.
        /// </summary>
        /// <param name="layer">The layer to use for ByLayer resolution.</param>
        LineWeight LineWeight { get; set; }
    }
}