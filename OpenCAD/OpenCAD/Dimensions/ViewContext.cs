using OpenCAD.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Dimensions
{
    public sealed class ViewContext
    {
        // World <-> Screen transforms
        public Matrix3x2 WorldToScreen { get; }
        public Matrix3x2 ScreenToWorld { get; }

        // Viewport information
        public float PixelsPerUnit { get; }
        public float AnnotationScale { get; }

        // Drawing units
        public LinearType LinearUnits { get; }
        public AngularType AngularUnits { get; }

        // Text rendering info
        public float TextPixelHeight { get; }
        public float DpiScale { get; }

        // Optional: camera direction, for 3D dims
        public Vector3 CameraDirection { get; }
    }
}
