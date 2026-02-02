using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers.GeoPoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface IGeoPointManager
    {
        /// <summary>
        /// Computes the best geoPoint for the given cursor position.
        /// </summary>
        GeoPoint? GetBestGeoPoint(
            Point3D cursorWorld,
            IEnumerable<OpenCADObject> visibleObjects,
            GeoPointModes activeModes,
            double aperture
        );

        /// <summary>
        /// The last computed geoPoint (for rendering).
        /// </summary>
        GeoPoint? CurrentSnap { get; }

        /// <summary>
        /// Clears the current snap state.
        /// </summary>
        void Clear();
    }
}
