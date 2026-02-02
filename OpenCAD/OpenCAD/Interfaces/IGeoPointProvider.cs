using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers.GeoPoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface IGeoPointProvider
    {
        /// <summary>
        /// Returns geometric snap points for this object.
        /// </summary>
        /// <param name="modes">
        /// The modes requested (Endpoint, Midpoint, Center, Quadrant, Perpendicular, etc.).
        /// </param>
        /// <param name="referencePoint">
        /// The point the user is hovering near — used for nearest, perpendicular, tangent, etc.
        /// </param>
        /// <returns>
        /// A sequence of GeoPoints describing all valid snap points for the requested modes.
        /// </returns>
        IEnumerable<GeoPoint> GetGeoPoints(
            OpenCADObject owner,
            GeoPointModes modes,
            Point3D referencePoint
        );
    }
}
