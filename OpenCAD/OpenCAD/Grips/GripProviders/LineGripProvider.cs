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
    public sealed class LineGripProvider : GripProviderBase, IGripProvider
    {
        public IEnumerable<Grip> GetGrips(OpenCADObject entity)
        {
            if (entity is not Line line)
                yield break;

            var start = Point3DToVector2(line.StartPoint);
            var end = Point3DToVector2(line.EndPoint);

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

        public OpenCADObject ApplyGripDelta(Grip activeGrip, Grip targetGrip, Vector2 delta, GripEditMode gripEditMode)
        {
            if (targetGrip.Owner is not Line line)
                return targetGrip.Owner;
            
            InitializeGripEdit(activeGrip);

            if (gripEditMode == GripEditMode.Stretch)
                return ApplyStretch(line, targetGrip, delta);
            if (gripEditMode == GripEditMode.Lengthen)
                return ApplyLengthen(line, targetGrip, delta);

            return line;
        }

        //public OpenCADObject ApplyGripDelta(Grip activeGrip, OpenCADObject targetObject, Vector2 delta, GripEditMode gripEditMode)
        //{
        //    if (targetObject is not Line line)
        //        return targetObject;

        //    InitializeGripEdit(activeGrip);

        //    return gripEditMode switch
        //    {
        //        GripEditMode.Move => ApplyMove(line, delta),
        //        GripEditMode.Rotate => ApplyRotate(line, delta),
        //        GripEditMode.Mirror => ApplyMirror(line, delta),
        //        GripEditMode.Scale => ApplyScale(line, delta),
        //        _ => line
        //    };
        //}

        private OpenCADObject ApplyStretch(Line line, Grip grip, Vector2 delta)
        {
            var start = Point3DToVector2(line.StartPoint);
            var end = Point3DToVector2(line.EndPoint);

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

            // Return a NEW immutable line
            return NewLine(line, start, end);
        }

        private OpenCADObject ApplyMove(Line original, Vector2 delta)
        {
            var start = Point3DToVector2(original.StartPoint) + delta;
            var end = Point3DToVector2(original.EndPoint) + delta;
            return NewLine(original, start, end);
        }

        private OpenCADObject ApplyRotate(Line original, Vector2 delta)
        {
            // 1. Compute the current mouse world position
            //    (delta is always world-space delta from the pivot)
            Vector2 currentMouse = _pivot + delta;

            // 2. Build the two rays:
            //    - startVec: reference direction captured at BeginGripEdit
            //    - currentVec: direction from pivot to current mouse
            Vector2 startVec = _referenceDirection;
            Vector2 currentVec = currentMouse - _pivot;

            // 3. Compute rotation angle between the two rays
            float angle =
                MathF.Atan2(currentVec.Y, currentVec.X) -
                MathF.Atan2(startVec.Y, startVec.X);

            // 4. Precompute sin/cos
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            // 5. Local helper to rotate a point around the pivot
            Vector2 RotatePoint(Vector2 p)
            {
                Vector2 v = p - _pivot;
                return new Vector2(
                    v.X * cos - v.Y * sin,
                    v.X * sin + v.Y * cos
                ) + _pivot;
            }

            // 6. Rotate the original endpoints
            Vector2 p1 = RotatePoint(new Vector2((float)original.StartPoint.X, (float)original.StartPoint.Y));
            Vector2 p2 = RotatePoint(new Vector2((float)original.EndPoint.X, (float)original.EndPoint.Y));

            // 7. Return a new immutable Line
            return NewLine(original, p1, p2);
        }

        private OpenCADObject ApplyMirror(Line original, Vector2 delta)
        {
            InitializeMirror(delta);

            // 1. Define mirror line
            Vector2 p0 = _pivot;
            Vector2 p1 = _pivot + delta;

            // Avoid division by zero if the user hasn't moved yet
            Vector2 axis = p1 - p0;
            if (axis.LengthSquared() < 1e-12f)
                return original;

            // 2. Normalize axis direction
            Vector2 d = Vector2.Normalize(axis);

            // 3. Perpendicular normal
            Vector2 n = new Vector2(-d.Y, d.X);

            // 4. Reflect original endpoints
            Vector2 pStart = ReflectPoint(new Vector2((float)original.StartPoint.X, (float)original.StartPoint.Y));
            Vector2 pEnd = ReflectPoint(new Vector2((float)original.EndPoint.X, (float)original.EndPoint.Y));

            // 5. Return new immutable line
            return NewLine(original, pStart, pEnd);
        }

        private OpenCADObject ApplyScale(Line original, Vector2 delta)
        {
            // 1. Compute current mouse world position
            Vector2 currentMouse = _pivot + delta;

            // 2. Compute the reference and current vectors
            Vector2 refVec = _referenceDirection;
            Vector2 curVec = currentMouse - _pivot;

            float refLen = refVec.Length();
            float curLen = curVec.Length();

            // Avoid division by zero
            if (refLen < 1e-12f)
                return original;

            // 3. Compute scale factor
            float scale = curLen / refLen;

            // 4. Local helper to scale a point around the pivot
            Vector2 ScalePoint(Vector2 p)
            {
                Vector2 v = p - _pivot;
                return _pivot + v * scale;
            }

            // 5. Scale original endpoints
            Vector2 p1 = ScalePoint(new Vector2((float)original.StartPoint.X, (float)original.StartPoint.Y));
            Vector2 p2 = ScalePoint(new Vector2((float)original.EndPoint.X, (float)original.EndPoint.Y));

            // 6. Return new immutable line
            return NewLine(original, p1, p2);
        }

        private OpenCADObject ApplyLengthen(Line original, Grip targetGrip, Vector2 delta)
        {
            // 1. Normalize the reference direction to get the line direction
            Vector2 dir = _referenceDirection;
            float len = dir.Length();
            if (len < 1e-12f)
                return original;

            dir /= len; // normalize

            // 2. Compute current mouse world position
            Vector2 currentMouse = _pivot + delta;

            // 3. Project mouse onto the line direction to get new length
            float t = Vector2.Dot(currentMouse - _pivot, dir);

            // 4. Compute the new endpoint position
            Vector2 newEnd = _pivot + dir * t;

            // 5. Convert original endpoints to vectors
            Vector2 pStart = new Vector2((float)original.StartPoint.X, (float)original.StartPoint.Y);
            Vector2 pEnd = new Vector2((float)original.EndPoint.X, (float)original.EndPoint.Y);

            // 6. Replace only the endpoint corresponding to the active grip
            Vector2 p1 = pStart;
            Vector2 p2 = pEnd;

            switch (targetGrip.SubIndex)
            {
                case 0: // start grip
                    p1 = newEnd;
                    break;

                case 1: // end grip
                    p2 = newEnd;
                    break;

                default:
                    // midpoint or unsupported grip → do nothing
                    return original;
            }

            // 7. Use your helper to construct the new line
            return NewLine(original, p1, p2);
        }

        private OpenCADObject NewLine(Line line, Vector2 start, Vector2 end)
        {
            var start3d = Vector2ToPoint3D(start, line.StartPoint.Z);
            var end3d = Vector2ToPoint3D(end, line.EndPoint.Z);
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
