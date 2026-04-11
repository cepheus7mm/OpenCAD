using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD
{
    /// <summary>
    /// Provides the world-space context needed for a hit test.
    /// </summary>
    public readonly struct HitTestContext
    {
        /// <summary>World-space position of the cursor.</summary>
        public Point3D WorldPosition { get; }

        /// <summary>Top-left corner of the pick box in world space.</summary>
        public Point3D PickBoxCorner1 { get; }

        /// <summary>Bottom-right corner of the pick box in world space.</summary>
        public Point3D PickBoxCorner2 { get; }

        public HitTestContext(Point3D worldPosition, Point3D pickBoxCorner1, Point3D pickBoxCorner2)
        {
            WorldPosition = worldPosition;
            PickBoxCorner1 = pickBoxCorner1;
            PickBoxCorner2 = pickBoxCorner2;
        }

        /// <summary>
        /// Returns true if <paramref name="point"/> falls inside the pick box.
        /// </summary>
        public bool IsInsidePickBox(Point3D point)
        {
            return point.IsValid &&
                   point.X >= PickBoxCorner1.X && point.X <= PickBoxCorner2.X &&
                   point.Y <= PickBoxCorner1.Y && point.Y >= PickBoxCorner2.Y;
        }
    }
}