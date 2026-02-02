using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips
{
    public readonly struct Grip : IEquatable<Grip>
    {
        public Vector2 Position { get; }
        public GripKind Kind { get; }
        public int SubIndex { get; }
        public OpenCADObject Owner { get; } // add this!

        public Grip(OpenCADObject owner, Vector2 position, GripKind kind, int subIndex)
        {
            Owner = owner;
            Position = position;
            Kind = kind;
            SubIndex = subIndex;
        }

        public bool Equals(Grip other)
        {
            return Owner == other.Owner &&
                   SubIndex == other.SubIndex &&
                   Kind == other.Kind;
        }

        public override bool Equals(object? obj)
            => obj is Grip g && Equals(g);

        public override int GetHashCode()
            => HashCode.Combine(Owner, SubIndex, Kind);
    }
}
