using OpenCAD.Grips;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD
{
    public enum HitResultKind
    {
        None,
        Entity,
        Grip
    }

    public readonly struct HitResult
    {
        public HitResultKind Kind { get; }
        public OpenCADObject? Entity { get; }
        public Grip? Grip { get; }

        private HitResult(HitResultKind kind, OpenCADObject? entity, Grip? grip)
        {
            Kind = kind;
            Entity = entity;
            Grip = grip;
        }

        public static HitResult None() =>
            new HitResult(HitResultKind.None, null, null);

        public static HitResult FromEntity(OpenCADObject entity) =>
            new HitResult(HitResultKind.Entity, entity, null);

        public static HitResult FromGrip(OpenCADObject entity, Grip grip) =>
            new HitResult(HitResultKind.Grip, entity, grip);
    }
}
