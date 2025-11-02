using System;

namespace OpenCAD
{
    /// <summary>
    /// Defines the line weight (thickness) for drawing lines.
    /// Values represent the line width in millimeters or as special values.
    /// </summary>
    public enum LineWeight
    {
        /// <summary>
        /// Use the layer's line weight.
        /// </summary>
        ByLayer = -1,

        /// <summary>
        /// Use the default line weight (typically 0.25mm).
        /// </summary>
        Default = 0,

        /// <summary>
        /// Hairline - thinnest possible line.
        /// </summary>
        Hairline = 1,

        /// <summary>
        /// 0.05mm line weight.
        /// </summary>
        LineWeight005 = 5,

        /// <summary>
        /// 0.09mm line weight.
        /// </summary>
        LineWeight009 = 9,

        /// <summary>
        /// 0.13mm line weight.
        /// </summary>
        LineWeight013 = 13,

        /// <summary>
        /// 0.15mm line weight.
        /// </summary>
        LineWeight015 = 15,

        /// <summary>
        /// 0.18mm line weight.
        /// </summary>
        LineWeight018 = 18,

        /// <summary>
        /// 0.20mm line weight.
        /// </summary>
        LineWeight020 = 20,

        /// <summary>
        /// 0.25mm line weight (standard).
        /// </summary>
        LineWeight025 = 25,

        /// <summary>
        /// 0.30mm line weight.
        /// </summary>
        LineWeight030 = 30,

        /// <summary>
        /// 0.35mm line weight.
        /// </summary>
        LineWeight035 = 35,

        /// <summary>
        /// 0.40mm line weight.
        /// </summary>
        LineWeight040 = 40,

        /// <summary>
        /// 0.50mm line weight.
        /// </summary>
        LineWeight050 = 50,

        /// <summary>
        /// 0.53mm line weight.
        /// </summary>
        LineWeight053 = 53,

        /// <summary>
        /// 0.60mm line weight.
        /// </summary>
        LineWeight060 = 60,

        /// <summary>
        /// 0.70mm line weight.
        /// </summary>
        LineWeight070 = 70,

        /// <summary>
        /// 0.80mm line weight.
        /// </summary>
        LineWeight080 = 80,

        /// <summary>
        /// 0.90mm line weight.
        /// </summary>
        LineWeight090 = 90,

        /// <summary>
        /// 1.00mm line weight.
        /// </summary>
        LineWeight100 = 100,

        /// <summary>
        /// 1.06mm line weight.
        /// </summary>
        LineWeight106 = 106,

        /// <summary>
        /// 1.20mm line weight.
        /// </summary>
        LineWeight120 = 120,

        /// <summary>
        /// 1.40mm line weight.
        /// </summary>
        LineWeight140 = 140,

        /// <summary>
        /// 1.58mm line weight.
        /// </summary>
        LineWeight158 = 158,

        /// <summary>
        /// 2.00mm line weight.
        /// </summary>
        LineWeight200 = 200,

        /// <summary>
        /// 2.11mm line weight.
        /// </summary>
        LineWeight211 = 211
    }
}