using System.Text.Json.Serialization;
using System.Xml.Serialization;
using System.Numerics;
using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;

namespace OpenCAD.Geometry
{
    public class Line : GeometryBase, ICurve
    {
        // ---------------------------------------------------------------------
        // Private fields
        // ---------------------------------------------------------------------
        private readonly object _propertyLock = new();

        // ---------------------------------------------------------------------
        // Constructors
        // ---------------------------------------------------------------------

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

        // ---------------------------------------------------------------------
        // Public properties
        // ---------------------------------------------------------------------

        [JsonIgnore, XmlIgnore]
        public Point3D StartPoint
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(StartPoint));
            set => SetPropertyValue(PropertyType.Point, nameof(StartPoint), OpenCADStrings.StartPoint, value);
        }

        [JsonIgnore, XmlIgnore]
        public Point3D EndPoint
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(EndPoint));
            set => SetPropertyValue(PropertyType.Point, nameof(EndPoint), OpenCADStrings.EndPoint, value);
        }

        [JsonIgnore, XmlIgnore]
        public override double Length => StartPoint.DistanceTo(EndPoint);

        [JsonIgnore, XmlIgnore]
        public override double Angle => StartPoint.AngleTo(EndPoint);

        // ---------------------------------------------------------------------
        // Base class overrides (GeometryBase)
        // ---------------------------------------------------------------------

        public override Extents GetExtents()
        {
            return new Extents(StartPoint, EndPoint);
        }

        public override IEnumerable<GeoPoint> GetGeoPoints(Point3D referencePoint, GeoPointModes geoPointType)
        {
            var candidates = new List<GeoPoint>();
            if (IsPreviewGeometry)
            {
                // Preview geometries do not provide geo points
                return candidates;
            }

            var segment = PolylineSegment.FromLine(this);

            // Use unified segment calculation with bulge = 0 for line
            candidates.AddRange(segment.GetGeoPoints(referencePoint, geoPointType));

            // Set the RelatedGeometryId for all candidates
            foreach (var geoPoint in candidates)
            {
                geoPoint.RelatedGeometryId = ID;
            }

            //// Line-specific: Perpendicular point (requires document preview point)
            //if (geoPointType.HasFlag(GeoPointModes.Perpendicular) && _document.PreviewPoint.HasValue)
            //{
            //    candidates.Add(GeometricCalculator.Perpendicular(_document.PreviewPoint.Value, this));
            //}

            return candidates;
        }

        // ---------------------------------------------------------------------
        // ICurve implementation
        // ---------------------------------------------------------------------

        public bool IsClosed => false;

        public bool IsPeriodic => false;

        public double DomainStart => 0.0;

        public double DomainEnd => 1.0;

        public double GetParameterAtPoint(Point3D point)
        {
            return GeometricCalculator.GetParameterAtPoint(point, StartPoint, EndPoint);
        }

        public Point3D GetPointAtParameter(double parameter)
        {
            return GeometricCalculator.GetPointAtParameter(parameter, StartPoint, EndPoint);
        }

        public Vector3D GetFirstDerivativeAtParameter(double t)
        {
            return GeometricCalculator.GetFirstDerivative(t, StartPoint, EndPoint);
        }

        public Vector3D GetSecondDerivativeAtParameter(double t)
        {
            return GeometricCalculator.GetSecondDerivative(t, StartPoint, EndPoint);
        }

        public double GetClosestParameter(Point3D point, bool extend = false)
        {
            return GeometricCalculator.GetClosestParameter(point, StartPoint, EndPoint, extend);
        }

        public Point3D GetClosestPoint(Point3D point, bool extend = false)
        {
            return GeometricCalculator.GetClosestPoint(point, StartPoint, EndPoint, extend);
        }

        public double GetLength()
        {
            return StartPoint.DistanceTo(EndPoint);
        }

        public double GetLength(double t0, double t1)
        {
            return GetPointAtParameter(t0).DistanceTo(GetPointAtParameter(t1));
        }

        public ICurve Trim(double t0, double t1)
        {
            // Normalize order
            if (t1 < t0)
            {
                double tmp = t0;
                t0 = t1;
                t1 = tmp;
            }

            // Clamp to domain if needed (optional)
            double a = Math.Max(DomainStart, Math.Min(DomainEnd, t0));
            double b = Math.Max(DomainStart, Math.Min(DomainEnd, t1));

            // Evaluate endpoints
            Point3D p0 = GetPointAtParameter(a);
            Point3D p1 = GetPointAtParameter(b);

            // Return a new line segment
            var newLine = new Line(Document, p0, p1);
            newLine.SetBasicPropertiesFrom(this);

            return newLine;
        }

        public ICurve Transform(Matrix4D transform)
        {
            // Transform endpoints
            Point3D s = StartPoint.Transform(transform);
            Point3D e = EndPoint.Transform(transform);
            // Update normal as well
            var tn = transform.TransformVector(_normal);


            // Return a new line curve
            var newLine = new Line(Document, s, e);
            newLine.SetBasicPropertiesFrom(this);
            newLine._normal = tn;

            return newLine;
        }

        // ---------------------------------------------------------------------
        // Internal methods
        // ---------------------------------------------------------------------
        // (none at present — reserved for future helpers)

        // ---------------------------------------------------------------------
        // Private methods
        // ---------------------------------------------------------------------
        // (none at present)
    }
}