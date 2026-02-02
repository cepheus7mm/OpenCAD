using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips.GripProviders
{
    public sealed class CircleGripProvider : GripProviderBase, IGripProvider
    {
        private const double MinRadius = 1e-6;

        public IEnumerable<Grip> GetGrips(OpenCADObject owner)
        {
            if (owner is not Circle c)
                yield break;
            var center = new Vector2((float)c.Center.X, (float)c.Center.Y);
            var radius = (float)c.Radius;
            // 1. Center grip
            yield return new Grip(
                owner: owner,
                position: center,
                kind: GripKind.Move,
                subIndex: 0
            );

            // 2. Radius grip (to the right)
            yield return new Grip(
                owner: owner,
                position: new Vector2(center.X + radius, center.Y),
                kind: GripKind.Stretch,
                subIndex: 1
            );

            // 3. Optional quadrant grips
            yield return new Grip(
                owner: owner,
                position: new Vector2(center.X, center.Y + radius),
                kind: GripKind.Stretch,
                subIndex: 2
            );

            yield return new Grip(
                owner: owner,
                position: new Vector2(center.X - radius, center.Y),
                kind: GripKind.Stretch,
                subIndex: 3
            );

            yield return new Grip(
                owner: owner,
                position: new Vector2(center.X, center.Y - radius),
                kind: GripKind.Stretch,
                subIndex: 4
            );
        }

        public OpenCADObject? ApplyGripDelta(Grip grip, Vector2 delta)
        {
            if (grip.Owner is not Circle c)
                return null;

            // Convert delta to 2D
            var dx = delta.X;
            var dy = delta.Y;

            var center = new Vector2((float)c.Center.X, (float)c.Center.Y);
            // 1. Center grip → move entire circle
            if (grip.Kind == GripKind.Move)
            {
                var newCenter = new Vector2(
                    center.X + dx,
                    center.Y + dy
                );
                var center3D = Vector2ToPoint3D(newCenter, c.Center.Z);
                return new Circle(center3D, c.Radius, c.Document);
            }

            // 2. Radius grips → resize
            // Compute new radius based on dragged grip position
            var newGripPos = new Point3D(
                grip.Position.X + dx,
                grip.Position.Y + dy,
                c.Center.Z
            );

            double newRadius = (newGripPos - c.Center).Length;
            if (newRadius < MinRadius)
                newRadius = MinRadius;

            return new Circle(c.Center, newRadius, c.Document);
        }
    }
}
