using OpenCAD.Geometry.Helpers.GeoPoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.Snaps
{
    public sealed class SnapPoint
    {
        public Vector2 Position { get; }
        public GeoPointModes Mode { get; }
        public OpenCADObject? Entity { get; }

        public SnapPoint(Vector2 pos, GeoPointModes mode, OpenCADObject? entity)
        {
            Position = pos;
            Mode = mode;
            Entity = entity;
        }
    }
}
