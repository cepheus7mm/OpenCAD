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
    public sealed class ArcGripProvider : GripProviderBase, IGripProvider
    {
        public IEnumerable<Grip> GetGrips(OpenCADObject owner)
        {
            if (owner is not Arc a)
                yield break;

            // Convert to Vector2 for convenience
            var center = new Vector2((float)a.Center.X, (float)a.Center.Y);
            var start = new Vector2((float)a.StartPoint.X, (float)a.StartPoint.Y);
            var end = new Vector2((float)a.EndPoint.X, (float)a.EndPoint.Y);

            // 1. Center grip
            yield return new Grip(
                owner: owner,
                position: center,
                kind: GripKind.Move,
                subIndex: 0
            );

            // 2. Start point grip
            yield return new Grip(
                owner: owner,
                position: start,
                kind: GripKind.Stretch,
                subIndex: 1
            );

            // 3. End point grip
            yield return new Grip(
                owner: owner,
                position: end,
                kind: GripKind.Stretch,
                subIndex: 2
            );

            // 4. Radius grip (midpoint of the arc)
            var mid = ComputeArcMidpoint(a);
            yield return new Grip(
                owner: owner,
                position: mid,
                kind: GripKind.Stretch,
                subIndex: 3
            );
        }

        public OpenCADObject ApplyGripDelta(
            Grip activeGrip,
            Grip targetGrip,
            Vector2 delta,
            GripEditMode gripEditMode)
        {
            if (targetGrip.Owner is not Arc arc)
                return targetGrip.Owner;

            InitializeGripEdit(activeGrip);

            return gripEditMode switch
            {
                GripEditMode.Stretch => ApplyStretch(arc, targetGrip, delta),
                GripEditMode.Lengthen => ApplyLengthen(arc, targetGrip, delta),
                _ => arc
            };
        }

        //public OpenCADObject ApplyGripDelta(
        //    Grip activeGrip,
        //    OpenCADObject targetObject,
        //    Vector2 delta,
        //    GripEditMode gripEditMode)
        //{
        //    if (targetObject is not Arc arc)
        //        return targetObject;

        //    InitializeGripEdit(activeGrip);

        //    return gripEditMode switch
        //    {
        //        GripEditMode.Move => ApplyMove(arc, delta),
        //        GripEditMode.Rotate => ApplyRotate(arc, delta),
        //        GripEditMode.Mirror => ApplyMirror(arc, delta),
        //        GripEditMode.Scale => ApplyScale(arc, delta),
        //        _ => arc
        //    };
        //}

        private OpenCADObject ApplyStretch(Arc arc, Grip grip, Vector2 delta)
        {
            var center = Point3DToVector2(arc.Center);
            var start = Point3DToVector2(arc.StartPoint);
            var end = Point3DToVector2(arc.EndPoint);
            double radius = arc.Radius;

            switch (grip.SubIndex)
            {
                case 0: // center
                    center += delta;
                    start += delta;
                    end += delta;
                    break;

                case 1: // start point
                    start += delta;
                    radius = (start - center).Length();
                    break;

                case 2: // end point
                    end += delta;
                    radius = (end - center).Length();
                    break;

                case 3: // radius grip
                    var mid = ComputeArcMidpoint(arc) + delta;
                    var newRadius = (mid - center).Length();
                    return NewArcFromCenterAngles(arc, center, newRadius, arc.StartAngle, arc.EndAngle);
            }

            // Recompute angles from new positions
            double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);


            return NewArcFromCenterAngles(arc, center, radius, startAngle, endAngle);
        }

        private OpenCADObject ApplyLengthen(Arc original, Grip targetGrip, Vector2 delta)
        {
            // Only start (1) and end (2) grips support Lengthen
            if (targetGrip.SubIndex != 1 && targetGrip.SubIndex != 2)
                return original;

            // 1. Extract geometry
            Vector2 center = Point3DToVector2(original.Center);

            // 2. Compute current mouse world position
            Vector2 currentMouse = _pivot + delta;

            // 3. Compute angle of mouse relative to center
            double newAngle = Math.Atan2(
                currentMouse.Y - center.Y,
                currentMouse.X - center.X
            );

            // 4. Determine which angle to replace
            double startAngle = original.StartAngle;
            double endAngle = original.EndAngle;

            if (targetGrip.SubIndex == 1)      // start grip
                startAngle = newAngle;
            else if (targetGrip.SubIndex == 2) // end grip
                endAngle = newAngle;

            // 5. Rebuild arc with same center + radius
            return NewArcFromCenterAngles(
                original,
                center,
                original.Radius,
                startAngle,
                endAngle
            );
        }

        private OpenCADObject ApplyRotate(Arc arc, Vector2 delta)
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

            var c = RotatePoint(Point3DToVector2(arc.Center));
            var s = RotatePoint(Point3DToVector2(arc.StartPoint));
            var e = RotatePoint(Point3DToVector2(arc.EndPoint));

            return NewArcFromPoints(arc, c, s, e);
        }

        private OpenCADObject ApplyMirror(Arc arc, Vector2 delta)
        {
            InitializeMirror(delta);

            Vector2 c = ReflectPoint(Point3DToVector2(arc.Center));
            Vector2 s = ReflectPoint(Point3DToVector2(arc.StartPoint));
            Vector2 e = ReflectPoint(Point3DToVector2(arc.EndPoint));

            return NewArcFromPoints(arc, c, s, e);
        }

        private OpenCADObject ApplyScale(Arc arc, Vector2 delta)
        {
            Vector2 currentMouse = _pivot + delta;

            Vector2 refVec = _referenceDirection;
            Vector2 curVec = currentMouse - _pivot;

            float refLen = refVec.Length();
            float curLen = curVec.Length();

            if (refLen < 1e-12f)
                return arc;

            float scale = curLen / refLen;

            Vector2 ScalePoint(Vector2 p)
            {
                Vector2 v = p - _pivot;
                return _pivot + v * scale;
            }

            var c = ScalePoint(Point3DToVector2(arc.Center));
            var s = ScalePoint(Point3DToVector2(arc.StartPoint));
            var e = ScalePoint(Point3DToVector2(arc.EndPoint));

            return NewArcFromPoints(arc, c, s, e);
        }

        private OpenCADObject NewArcFromCenterAngles(
            Arc original,
            Vector2 center,
            double radius,
            double startAngle,
            double endAngle)
        {
            var c3 = Vector2ToPoint3D(center, original.Center.Z);

            return new Arc(c3, radius, startAngle, endAngle, original.Document!)
            {
                Layer = original.Layer,
                Color = original.Color,
                LineTypeID = original.LineTypeID,
                LineWeight = original.LineWeight
            };
        }

        private OpenCADObject NewArcFromPoints(
            Arc original,
            Vector2 center,
            Vector2 start,
            Vector2 end)
        {
            // 1. Compute raw angles
            double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);

            // 2. Normalize both into [0, 2π)
            startAngle = NormalizeAngle(startAngle);
            endAngle = NormalizeAngle(endAngle);

            // 3. Preserve original sweep direction
            bool wasCCW = original.SweepAngle >= 0;

            if (wasCCW)
            {
                // Ensure endAngle is CCW from startAngle
                if (endAngle < startAngle)
                    endAngle += Math.PI * 2;
            }
            else
            {
                // Ensure endAngle is CW from startAngle
                if (endAngle > startAngle)
                    endAngle -= Math.PI * 2;
            }

            // 4. Compute radius
            double radius = (start - center).Length();

            // 5. Build new arc
            var c3 = Vector2ToPoint3D(center, original.Center.Z);

            return new Arc(c3, radius, startAngle, endAngle, original.Document!)
            {
                Layer = original.Layer,
                Color = original.Color,
                LineTypeID = original.LineTypeID,
                LineWeight = original.LineWeight
            };
        }

        private double NormalizeAngle(double a)
        {
            a %= (Math.PI * 2);
            if (a < 0) a += Math.PI * 2;
            return a;
        }

        private OpenCADObject ApplyMove(Arc arc, Vector2 delta)
        {
            var c = Point3DToVector2(arc.Center) + delta;
            var s = Point3DToVector2(arc.StartPoint) + delta;
            var e = Point3DToVector2(arc.EndPoint) + delta;

            return NewArcFromPoints(arc, c, s, e);
        }

        private Vector2 ComputeArcMidpoint(Arc a)
        {
            // Midpoint angle = (startAngle + endAngle) / 2
            double midAngle = a.StartAngle + (a.SweepAngle / 2.0);

            var x = a.Center.X + a.Radius * Math.Cos(midAngle);
            var y = a.Center.Y + a.Radius * Math.Sin(midAngle);

            return new Vector2((float)x, (float)y);
        }

    }
}
