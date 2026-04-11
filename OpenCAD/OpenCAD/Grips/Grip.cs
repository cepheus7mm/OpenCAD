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
        public Vector2 Position { get; init; }
        public GripKind Kind { get; init; }
        public int SubIndex { get; init; }
        public OpenCADObject Owner { get; init; }
        public object? Tag { get; init; }

        public Grip(OpenCADObject owner, Vector2 position, GripKind kind, int subIndex, object? tag = null)
        {
            Owner = owner;
            Position = position;
            Kind = kind;
            SubIndex = subIndex;
            Tag = tag;
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
