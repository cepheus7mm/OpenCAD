using OpenCAD.Geometry;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAD.Grips.GripProviders
{
    public enum PolylineGripKind
    {
        Vertex,
        Midpoint,
        Bulge,
        Close
    }

    [GripProvider(typeof(Polyline))]
    public sealed class PolylineGripProvider : GripProviderBase, IGripProvider
    {
        private Polyline _targetPolyline;

        public IEnumerable<Grip> GetGrips(OpenCADObject owner)
        {
            var pl = (Polyline)owner;
            var verts = pl.Vertices.ToList();
            var bulges = pl.Bulges.ToList();

            // 1. Vertex grips
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                yield return new Grip
                {
                    Owner = owner,
                    Position = Point3DToVector2(v.Position),
                    SubIndex = i,
                    Kind = GripKind.Stretch,
                    Tag = PolylineGripKind.Vertex
                };
            }

            // 2. Midpoint grips (between vertices)
            for (int i = 0; i < verts.Count - 1; i++)
            {
                var p1 = Point3DToVector2(verts[i].Position);
                var p2 = Point3DToVector2(verts[i + 1].Position);
                var mid = 0.5f * (p1 + p2);

                yield return new Grip
                {
                    Owner = owner,
                    Position = mid,
                    SubIndex = i,
                    Kind = GripKind.Stretch,
                    Tag = PolylineGripKind.Midpoint
                };
            }

            // Closing midpoint (for closed polylines)
            if (pl.IsClosed && verts.Count > 1)
            {
                var pLast = Point3DToVector2(verts[^1].Position);
                var pFirst = Point3DToVector2(verts[0].Position);
                var mid = 0.5f * (pLast + pFirst);

                yield return new Grip
                {
                    Owner = owner,
                    Position = mid,
                    SubIndex = verts.Count - 1,
                    Kind = GripKind.Stretch,
                    Tag = PolylineGripKind.Midpoint
                };
            }

            // 3. Bulge grips (midpoint of arc segments)
            for (int i = 0; i < bulges.Count; i++)
            {
                if (Math.Abs(bulges[i]) <= 1e-12)
                    continue;

                var v1 = verts[i].Position;
                var v2 = (i == verts.Count - 1 && pl.IsClosed)
                    ? verts[0].Position
                    : verts[i + 1].Position;

                var seg = BulgeUtils.GetPolylineSegment(v1, v2, bulges[i]);

                double midAngle = seg.StartAngle + seg.Sweep / 2.0;
                var mid = new Vector2(
                    (float)(seg.Center.X + seg.Radius * Math.Cos(midAngle)),
                    (float)(seg.Center.Y + seg.Radius * Math.Sin(midAngle))
                );

                yield return new Grip
                {
                    Owner = owner,
                    Position = mid,
                    SubIndex = i,
                    Kind = GripKind.Stretch,
                    Tag = PolylineGripKind.Bulge
                };
            }

            // 4. Close grip (only when open)
            if (!pl.IsClosed && verts.Count > 1)
            {
                var start = Point3DToVector2(verts[0].Position);
                yield return new Grip
                {
                    Owner = owner,
                    Position = start,
                    SubIndex = 0,
                    Kind = GripKind.Stretch,
                    Tag = PolylineGripKind.Close
                };
            }
        }

        public OpenCADObject ApplyGripDelta(
            Grip activeGrip,
            OpenCADObject targetObject,
            Vector2 delta,
            GripEditMode gripEditMode)
        {
            // Transform modes → base
            if (gripEditMode != GripEditMode.Stretch)
                return base.ApplyGripDelta(activeGrip, targetObject, delta, gripEditMode);

            _targetPolyline = (Polyline)targetObject;
            var kind = (PolylineGripKind)activeGrip.Tag;

            return kind switch
            {
                PolylineGripKind.Vertex => MoveVertex(activeGrip.SubIndex, delta),
                PolylineGripKind.Midpoint => InsertVertex(activeGrip.SubIndex, delta),
                PolylineGripKind.Bulge => AdjustBulge(activeGrip.SubIndex, delta),
                PolylineGripKind.Close => ToggleClosed(),
                _ => targetObject
            };
        }

        public OpenCADObject ApplyGripDelta(
            Grip activeGrip,
            Grip targetGrip,
            Vector2 delta,
            GripEditMode gripEditMode)
        {
            if (targetGrip.Owner is not Polyline)
                return targetGrip.Owner;
            _targetPolyline = (Polyline)targetGrip.Owner;

            InitializeGripEdit(activeGrip);

            return gripEditMode switch
            {
                GripEditMode.Stretch => ApplyStretch(targetGrip, delta),
                //GripEditMode.Lengthen => ApplyLengthen(targetGrip, delta),
                _ => targetGrip.Owner
            };
        }

        private OpenCADObject ApplyStretch(Grip targetGrip, Vector2 delta)
        {
            var kind = (PolylineGripKind)targetGrip.Tag;

            return kind switch
            {
                PolylineGripKind.Vertex => MoveVertex(targetGrip.SubIndex, delta),
                PolylineGripKind.Midpoint => InsertVertex(targetGrip.SubIndex, delta),
                PolylineGripKind.Bulge => AdjustBulge(targetGrip.SubIndex, delta),
                PolylineGripKind.Close => ToggleClosed(),
                _ => _targetPolyline
            };
        }

        private Polyline MoveVertex(int index, Vector2 delta)
        {
            var verts = _targetPolyline.Vertices.ToArray();
            var bulges = _targetPolyline.Bulges.ToArray();

            var polyline = CreatePolyline(verts, bulges);
            verts = polyline.Vertices.ToArray();
            var v = verts[index];
            v.Position = new Point3D(v.Position.X + delta.X, v.Position.Y + delta.Y, v.Position.Z);
            verts[index] = v;

            return polyline;
        }

        private Polyline InsertVertex(int segIndex, Vector2 delta)
        {
            var verts = _targetPolyline.Vertices.ToList();
            var bulges = _targetPolyline.Bulges.ToList();

            int nextIndex = (segIndex + 1) % verts.Count;

            var p1 = Point3DToVector2(verts[segIndex].Position);
            var p2 = Point3DToVector2(verts[nextIndex].Position);

            var baseMid = 0.5f * (p1 + p2);
            var newPos2D = baseMid + delta;
            var z = verts[segIndex].Position.Z;

            var newVertex = new PolylineVertex(_targetPolyline.Document, Vector2ToPoint3D(newPos2D, z));

            int insertIndex = nextIndex;
            verts.Insert(insertIndex, newVertex);
            bulges.Insert(insertIndex, 0.0);

            return CreatePolyline(verts.ToArray(), bulges.ToArray());
        }

        private Polyline AdjustBulge(int index, Vector2 delta)
        {
            var verts = _targetPolyline.Vertices.ToArray();
            var bulges = _targetPolyline.Bulges.ToArray();

            int nextIndex = (index + 1) % verts.Length;
            var v1 = verts[index];
            var v2 = verts[nextIndex];

            // Compute tangent at v1
            Vector2 tangent = ComputeTangent(_targetPolyline, index);

            // Chord from v1 to v2
            var chord = new Vector2(
                (float)(v2.Position.X - v1.Position.X),
                (float)(v2.Position.Y - v1.Position.Y)
            );

            if (chord.LengthSquared() < 1e-12f)
                return _targetPolyline;

            chord = Vector2.Normalize(chord);

            // Use drag direction to influence curvature
            // Current bulge midpoint as base, then apply delta
            var currentBulge = bulges[index];
            var seg = BulgeUtils.GetPolylineSegment(v1.Position, v2.Position, currentBulge);

            double midAngle = seg.StartAngle + seg.Sweep / 2.0;
            var mid = new Vector2(
                (float)(seg.Center.X + seg.Radius * Math.Cos(midAngle)),
                (float)(seg.Center.Y + seg.Radius * Math.Sin(midAngle))
            );

            var dragged = mid + delta;
            var centerToDragged = dragged - new Vector2((float)seg.Center.X, (float)seg.Center.Y);

            if (centerToDragged.LengthSquared() < 1e-12f)
                return _targetPolyline;

            centerToDragged = Vector2.Normalize(centerToDragged);

            // Deflection between tangent and chord
            var tangent3 = new Vector3D(tangent.X, tangent.Y, 0);
            var chord3 = new Vector3D(chord.X, chord.Y, 0);
            var normal3 = Vector3D.UnitZ;

            double deflection = Vector3D.SignedAngleBetween(tangent3, chord3, normal3);
            double included = 2.0 * deflection;
            double newBulge = Math.Tan(included / 4.0);

            bulges[index] = newBulge;

            return CreatePolyline(verts, bulges);
        }

        private Polyline ToggleClosed()
        {
            return _targetPolyline.Clone() as Polyline;
        }

        private Vector2 ComputeTangent(Polyline pl, int index)
        {
            var verts = pl.Vertices.ToArray();

            if (verts.Length < 2)
                return new Vector2(1, 0);

            int prevIndex;
            int nextIndex;

            if (index > 0)
            {
                prevIndex = index - 1;
                nextIndex = index;
            }
            else if (pl.IsClosed)
            {
                prevIndex = verts.Length - 1;
                nextIndex = 0;
            }
            else
            {
                prevIndex = 0;
                nextIndex = 1;
            }

            var pPrev = Point3DToVector2(verts[prevIndex].Position);
            var pNext = Point3DToVector2(verts[nextIndex].Position);

            var dir = pNext - pPrev;
            if (dir.LengthSquared() < 1e-12f)
                return new Vector2(1, 0);

            return Vector2.Normalize(dir);
        }

        private Polyline CreatePolyline(PolylineVertex[] vertices, double[] bulges)
        {
            var polyline = new Polyline(_targetPolyline.Document);
            for (int i = 0; i < bulges.Length; i++)
            {
                var refVert = vertices[i];
                var v = new PolylineVertex(_targetPolyline.Document, refVert.Position, refVert.Bulge)
                {
                    StartWidth = refVert.StartWidth,
                    EndWidth = refVert.EndWidth,
                };
                polyline.AddVertex(v);
            }
            if (_targetPolyline.IsClosed)
                polyline.Close();

            return polyline;
        }
    }
}