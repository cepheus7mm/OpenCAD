using OpenCAD.Geometry;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips.GripProviders
{
    public class GripProviderBase
    {
        protected const double MinRadius = 1e-6;
        protected Vector2 _pivot;
        protected Vector2 _referenceDirection;
        protected Vector2 _mirrorAxis;
        protected Vector2 _mirrorNormal;

        public Vector2 Point3DToVector2(Point3D pt) 
        {
            return new Vector2((float)pt.X, (float)pt.Y);
        }

        public Point3D Vector2ToPoint3D(Vector2 v, double z) 
        {
            return new Point3D(v.X, v.Y, z);
        }

        public void InitializeGripEdit(Grip activeGrip)
        {
            _pivot = activeGrip.Position;
            _referenceDirection = ComputeReferenceDirection(activeGrip);
        }

        public OpenCADObject ApplyGripDelta(Grip activeGrip, OpenCADObject targetObject, Vector2 delta, GripEditMode gripEditMode)
        {

            InitializeGripEdit(activeGrip);

            return gripEditMode switch
            {
                GripEditMode.Move => ApplyMove(targetObject, delta),
                GripEditMode.Rotate => ApplyRotate(targetObject, delta),
                GripEditMode.Mirror => ApplyMirror(targetObject, delta),
                GripEditMode.Scale => ApplyScale(targetObject, delta),
                _ => targetObject
            };
        }

        private OpenCADObject ApplyMove(OpenCADObject targetObject, Vector2 delta)
        {
            var m = TransformBuilder.Translate(delta);
            return ((ICurve)targetObject).Transform(m) as OpenCADObject;
        }

        private OpenCADObject ApplyRotate(OpenCADObject targetObject, Vector2 delta)
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
            
            var m = TransformBuilder.Rotate(_pivot, angle);
            return ((ICurve)targetObject).Transform(m) as OpenCADObject;
        }

        private OpenCADObject ApplyMirror(OpenCADObject targetObject, Vector2 delta)
        {
            var m = TransformBuilder.Mirror(_pivot, _pivot + delta);
            return ((ICurve)targetObject).Transform(m) as OpenCADObject;
        }

        private OpenCADObject ApplyScale(OpenCADObject targetObject, Vector2 delta)
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
                return targetObject;

            // 3. Compute scale factor
            float scale = curLen / refLen;

            var m = TransformBuilder.Scale(_pivot, scale);
            return ((ICurve)targetObject).Transform(m) as OpenCADObject;
        }

        protected void InitializeMirror(Vector2 delta)
        {
            var axis = delta;
            if (axis.LengthSquared() < 1e-12f)
            {
                _mirrorAxis = new Vector2(1, 0);
                _mirrorNormal = new Vector2(0, 1);
                return;
            }

            _mirrorAxis = Vector2.Normalize(axis);
            _mirrorNormal = new Vector2(-_mirrorAxis.Y, _mirrorAxis.X);
        }

        protected Vector2 ReflectPoint(Vector2 p)
        {
            Vector2 v = p - _pivot;

            float projAxis = Vector2.Dot(v, _mirrorAxis);
            float projNormal = Vector2.Dot(v, _mirrorNormal);

            Vector2 reflected = (projAxis * _mirrorAxis) - (projNormal * _mirrorNormal);

            return reflected + _pivot;
        }

        protected Vector2 ComputeReferenceDirection(Grip activeGrip)
        {
            return activeGrip.Owner switch
            {
                Line => GetLineReferenceDirection(activeGrip),
                Arc => GetArcReferenceDirection(activeGrip),
                _ => new Vector2(1, 0) // Default reference direction
            };
        }

        private Vector2 GetArcReferenceDirection(Grip activeGrip)
        {
            var arc = (Arc)activeGrip.Owner;
            var vec = arc.StartPoint - arc.Center;
            return new Vector2((float)vec.X, (float)vec.Y);
        }

        private Vector2 GetLineReferenceDirection(Grip activeGrip)
        {
            Point3D p1 = default, p2 = default;
            switch (activeGrip.SubIndex)
            {
                case 0:
                case 2:
                    p1 = ((Line)activeGrip.Owner).StartPoint;
                    p2 = ((Line)activeGrip.Owner).EndPoint;
                    break;
                case 1:
                    p1 = ((Line)activeGrip.Owner).EndPoint;
                    p2 = ((Line)activeGrip.Owner).StartPoint;
                    break;

            }
            var vec = p2 - p1;
            return new Vector2((float)vec.X, (float)vec.Y);
        }
    }
}
