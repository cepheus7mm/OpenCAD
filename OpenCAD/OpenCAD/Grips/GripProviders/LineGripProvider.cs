using OpenCAD.Geometry;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips.GripProviders
{
    public sealed class LineGripProvider : GripProviderBase, IGripProvider
    {
        public IEnumerable<Grip> GetGrips(OpenCADObject entity)
        {
            if (entity is not Line line)
                yield break;

            var start = Point3DToVector2(line.Start);
            var end = Point3DToVector2(line.End);

            // Start grip
            yield return new Grip(
                owner: line,
                position: start,
                kind: GripKind.Stretch,
                subIndex: 0
            );

            // End grip
            yield return new Grip(
                owner: line,
                position: end,
                kind: GripKind.Stretch,
                subIndex: 1
            );

            // Midpoint grip
            var mid = (start + end) * 0.5f;

            yield return new Grip(
                owner: line,
                position: mid,
                kind: GripKind.Move,
                subIndex: 2
            );
        }

        public OpenCADObject ApplyGripDelta(Grip grip, Vector2 delta)
        {
            if (grip.Owner is not Line line)
                return grip.Owner;

            var start = Point3DToVector2(line.Start);
            var end = Point3DToVector2(line.End);

            switch (grip.SubIndex)
            {
                case 0: // Start point
                    start += delta;
                    break;

                case 1: // End point
                    end += delta;
                    break;

                case 2: // Midpoint (move entire line)
                    start += delta;
                    end += delta;
                    break;
            }

            var start3d = Vector2ToPoint3D(start, line.Start.Z);
            var end3d = Vector2ToPoint3D(end, line.End.Z);
            // Return a NEW immutable line
            return new Line(line.Document!, start3d, end3d)
            {
                Layer = line.Layer,
                Color = line.Color,
                LineTypeID = line.LineTypeID,
                LineWeight = line.LineWeight
            };
        }
    }
}
