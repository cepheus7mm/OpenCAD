using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using System.Numerics;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;

namespace OpenCAD.Geometry
{
    public class Line : GeometryBase, ILinearGeometry
    {
        private readonly object _propertyLock = new();

        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public Line() : base()
        {
        }

        public Line(OpenCADDocument doc, Point3D start, Point3D end) : base(doc)
        {
            StartPoint = start;
            EndPoint = end;
        }

        [JsonIgnore, XmlIgnore]
        public Point3D StartPoint
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(StartPoint)) ?? new Point3D();
            set => SetPropertyValue(PropertyType.Point, nameof(StartPoint), OpenCADStrings.StartPoint, value);
        }

        [JsonIgnore, XmlIgnore]
        public Point3D EndPoint
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(EndPoint)) ?? new Point3D();
            set => SetPropertyValue(PropertyType.Point, nameof(EndPoint), OpenCADStrings.EndPoint, value);
        }

        [JsonIgnore, XmlIgnore]
        public override double Length => StartPoint.DistanceTo(EndPoint);

        [JsonIgnore, XmlIgnore]
        public override double Angle => StartPoint.AngleTo(EndPoint);
        
        public override Vector3D? GetFirstDerivate(Point3D point)
        {
            // For a straight line the first derivative (tangent) is constant:
            // the normalized direction vector from StartPoint to EndPoint.
            var dir = EndPoint - StartPoint; // returns Vector3D
            double len = dir.Length;

            // If the line has zero length there is no well-defined tangent.
            if (len <= double.Epsilon)
                return null;

            return new Vector3D(dir.X / len, dir.Y / len, dir.Z / len);
        }

        public override double GetParameterAtPoint(Point3D point)
        {
            throw new NotImplementedException();
        }

        public override Point3D GetPointAtParameter(double parameter)
        {
            throw new NotImplementedException();
        }

        public override Point3D GetClosestPointTo(Point3D point, bool extend = false)
        {
            var lineVec = EndPoint - StartPoint;
            var pointVec = point - StartPoint;
            double lineLenSq = lineVec.LengthSquared;
            if (lineLenSq < double.Epsilon)
                return StartPoint; // Line is a point
            double t = Vector3D.Dot(pointVec, lineVec) / lineLenSq;
            if (!extend)
                t = Math.Max(0, Math.Min(1, t)); // Clamp to [0, 1]
            return StartPoint + lineVec * t;
        }

        public override Vector3D? GetSecondDerivate(Point3D point)
        {
            var firstDerivate = GetFirstDerivate(EndPoint);
            return firstDerivate?.Rotate(Math.PI / 2, _normal);
        }

        public override Extents GetExtents()
        {
            return new Extents
            {
                Min = new Point3D(
                    Math.Min(StartPoint.X, EndPoint.X),
                    Math.Min(StartPoint.Y, EndPoint.Y),
                    Math.Min(StartPoint.Z, EndPoint.Z)
                ),
                Max = new Point3D(
                    Math.Max(StartPoint.X, EndPoint.X),
                    Math.Max(StartPoint.Y, EndPoint.Y),
                    Math.Max(StartPoint.Z, EndPoint.Z)
                )
            };
        }
        
        #region Editing

        public override bool Transform(Matrix4D transformation)
        {
            // Convert to System.Numerics.Vector3 (float precision is OK for rendering/transforms)
            var s = Vector3D.Transform(new Vector3D(StartPoint.X, StartPoint.Y, StartPoint.Z), transformation);
            var e = Vector3D.Transform(new Vector3D(EndPoint.X, EndPoint.Y, EndPoint.Z), transformation);

            StartPoint = new Point3D(s.X, s.Y, s.Z);
            EndPoint = new Point3D(e.X, e.Y, e.Z);
            return true;
        }

        public override IEnumerable<GeoPoint> GetGeoPoints(Point3D referencePoint, GeoPointModes geoPointType)
        {
            var candidates = new List<GeoPoint> ();
            if (IsPreviewGeometry)
            {
                // Preview geometries do not provide geo points
                return candidates;
            }
            if (geoPointType.HasFlag(GeoPointModes.Vertex))
            {
                candidates.Add(new GeoPoint(StartPoint, GeoPointModes.Vertex) { RelatedGeometryId = ID});
                candidates.Add(new GeoPoint(EndPoint, GeoPointModes.Vertex) { RelatedGeometryId = ID });
            }
            if (geoPointType.HasFlag(GeoPointModes.Middle))
            {
                candidates.Add(GeometricCalculator.MidPoint(this));
            }
            if (geoPointType.HasFlag(GeoPointModes.Perpendicular) && _document.PreviewPoint is not null)
            {
                candidates.Add(GeometricCalculator.Perpendicular(_document.PreviewPoint, this));
            }
            if (geoPointType.HasFlag(GeoPointModes.NearestPoint))
            {
                var geoPoint = new GeoPoint(GetClosestPointTo(referencePoint), GeoPointModes.NearestPoint);
                candidates.Add(geoPoint);
            }

            return candidates;
        }

        #endregion
    }
}
