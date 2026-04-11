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
    [GripProvider(typeof(Circle))]
    public sealed class CircleGripProvider : GripProviderBase, IGripProvider
    {
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

        public OpenCADObject ApplyGripDelta(
            Grip activeGrip,
            Grip targetGrip,
            Vector2 delta,
            GripEditMode gripEditMode)
        {
            if (targetGrip.Owner is not Circle c)
                return targetGrip.Owner;

            InitializeGripEdit(activeGrip);

            return gripEditMode switch
            {
                GripEditMode.Stretch => ApplyStretch(c, targetGrip, delta),
                GripEditMode.Lengthen => ApplyStretch(c, targetGrip, delta), //Same as stretch for circles
                _ => c
            };
        }

        //public OpenCADObject ApplyGripDelta(
        //    Grip activeGrip,
        //    OpenCADObject targetObject,
        //    Vector2 delta,
        //    GripEditMode gripEditMode)
        //{
        //    if (targetObject is not Circle c)
        //        return targetObject;

        //    InitializeGripEdit(activeGrip);

        //    return gripEditMode switch
        //    {
        //        GripEditMode.Move => ApplyMove(c, delta),
        //        GripEditMode.Rotate => ApplyRotate(c, delta),
        //        GripEditMode.Mirror => ApplyMirror(c, delta),
        //        GripEditMode.Scale => ApplyScale(c, delta),
        //        _ => c
        //    };
        //}

        private OpenCADObject ApplyStretch(Circle c, Grip grip, Vector2 delta)
        {
            var center = Point3DToVector2(c.Center);

            if (grip.SubIndex == 0)
            {
                // Move entire circle
                center += delta;
                return NewCircle(c, center, c.Radius);
            }

            // Radius grips
            Vector2 newGripPos = grip.Position + delta;
            double newRadius = (newGripPos - center).Length();

            if (newRadius < 1e-6)
                newRadius = 1e-6;

            return NewCircle(c, center, newRadius);
        }

        private OpenCADObject ApplyMove(Circle c, Vector2 delta)
        {
            var center = Point3DToVector2(c.Center) + delta;
            return NewCircle(c, center, c.Radius);
        }

        private OpenCADObject ApplyRotate(Circle c, Vector2 delta)
        {
            Vector2 currentMouse = _pivot + delta;

            Vector2 startVec = _referenceDirection;
            Vector2 currentVec = currentMouse - _pivot;

            float angle =
                MathF.Atan2(currentVec.Y, currentVec.X) -
                MathF.Atan2(startVec.Y, startVec.X);

            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            Vector2 RotatePoint(Vector2 p)
            {
                Vector2 v = p - _pivot;
                return new Vector2(
                    v.X * cos - v.Y * sin,
                    v.X * sin + v.Y * cos
                ) + _pivot;
            }

            Vector2 newCenter = RotatePoint(Point3DToVector2(c.Center));

            return NewCircle(c, newCenter, c.Radius);
        }

        private OpenCADObject ApplyMirror(Circle c, Vector2 delta)
        {
            InitializeMirror(delta);

            Vector2 newCenter = ReflectPoint(Point3DToVector2(c.Center));
            return NewCircle(c, newCenter, c.Radius);
        }

        private OpenCADObject ApplyScale(Circle c, Vector2 delta)
        {
            Vector2 currentMouse = _pivot + delta;

            Vector2 refVec = _referenceDirection;
            Vector2 curVec = currentMouse - _pivot;

            float refLen = refVec.Length();
            float curLen = curVec.Length();

            if (refLen < 1e-12f)
                return c;

            float scale = curLen / refLen;

            Vector2 ScalePoint(Vector2 p)
            {
                Vector2 v = p - _pivot;
                return _pivot + v * scale;
            }

            Vector2 newCenter = ScalePoint(Point3DToVector2(c.Center));
            double newRadius = c.Radius * scale;

            if (newRadius < 1e-6)
                newRadius = 1e-6;

            return NewCircle(c, newCenter, newRadius);
        }

        private OpenCADObject NewCircle(Circle original, Vector2 center, double radius)
        {
            var c3 = Vector2ToPoint3D(center, original.Center.Z);

            return new Circle(c3, radius, original.Document!)
            {
                Layer = original.Layer,
                Color = original.Color,
                LineTypeID = original.LineTypeID,
                LineWeight = original.LineWeight
            };
        }
    }
}
