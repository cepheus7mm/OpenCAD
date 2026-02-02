using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Geometry.Helpers.GeoPoints;
using OpenCAD.Grips;
using OpenCAD.Interfaces;
using OpenCAD.SegmentSource;
using OpenCAD.Settings;
using System.Numerics;
using System.Security.Principal;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    public class Line : GeometryBase, ICurve, ISegmentSource
    {
        // ---------------------------------------------------------------------
        // Private fields
        // ---------------------------------------------------------------------
        private readonly object _propertyLock = new();
        private const int StartPointIndex = 0;
        private const int EndPointIndex = 1;
        private const int MidPointIndex = 2;

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
            Start = start;
            End = end;
        }

        // ---------------------------------------------------------------------
        // Public properties
        // ---------------------------------------------------------------------

        [JsonIgnore, XmlIgnore]
        public Point3D Start
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(Start));
            set => SetPropertyValue(PropertyType.Point, nameof(Start), OpenCADStrings.StartPoint, value);
        }

        [JsonIgnore, XmlIgnore]
        public Point3D End
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(End));
            set => SetPropertyValue(PropertyType.Point, nameof(End), OpenCADStrings.EndPoint, value);
        }

        [JsonIgnore, XmlIgnore]
        public Point3D MidPoint
        {
            get
            {
                return new Point3D(
                    (Start.X + End.X) / 2.0,
                    (Start.Y + End.Y) / 2.0,
                    (Start.Z + End.Z) / 2.0
                );
            }
        }

        [JsonIgnore, XmlIgnore]
        public override double Length => Start.DistanceTo(End);

        [JsonIgnore, XmlIgnore]
        public override double Angle => Start.AngleTo(End);

        // ---------------------------------------------------------------------
        // Base class overrides (GeometryBase)
        // ---------------------------------------------------------------------

        public override Extents GetExtents()
        {
            return new Extents(Start, End);
        }

        //public IEnumerable<GeoPoint> GetGeoPoints(Point3D referencePoint, GeoPointModes geoPointType)
        //{

        //}

        // ---------------------------------------------------------------------
        // ICurve implementation
        // ---------------------------------------------------------------------

        public bool IsClosed => false;

        public bool IsPeriodic => false;

        public double DomainStart => 0.0;

        public double DomainEnd => 1.0;

        public double GetParameterAtPoint(Point3D point)
        {
            return GeometricCalculator.GetParameterAtPoint(point, Start, End);
        }

        public Point3D GetPointAtParameter(double parameter)
        {
            return GeometricCalculator.GetPointAtParameter(parameter, Start, End);
        }

        public Vector3D GetFirstDerivativeAtParameter(double t)
        {
            return GeometricCalculator.GetFirstDerivative(t, Start, End);
        }

        public Vector3D GetSecondDerivativeAtParameter(double t)
        {
            return GeometricCalculator.GetSecondDerivative(t, Start, End);
        }

        public double GetClosestParameter(Point3D point, bool extend = false)
        {
            return GeometricCalculator.GetClosestParameter(point, Start, End, extend);
        }

        public Point3D GetClosestPoint(Point3D point, bool extend = false)
        {
            return GeometricCalculator.GetClosestPoint(point, Start, End, extend);
        }

        public double GetLength()
        {
            return Start.DistanceTo(End);
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
            Point3D s = Start.Transform(transform);
            Point3D e = End.Transform(transform);
            // Update normal as well
            var tn = transform.TransformVector(_normal);


            // Return a new line curve
            var newLine = new Line(Document, s, e);
            newLine.SetBasicPropertiesFrom(this);
            newLine._normal = tn;

            return newLine;
        }

        public IEnumerable<GeoPoint> GetGeoPoints(GeoPointModes modes, Point3D referencePoint)
        {
            var candidates = new List<GeoPoint>();
            if (IsPreviewGeometry)
            {
                // Preview geometries do not provide geo points
                return candidates;
            }

            var segment = PolylineSegment.FromLine(this);

            // Use unified segment calculation with bulge = 0 for line
            candidates.AddRange(segment.GetGeoPoints(referencePoint, modes));

            // Set the RelatedGeometryId for all candidates
            foreach (var geoPoint in candidates)
            {
                geoPoint.RelatedGeometryId = ID;
            }

            return candidates;
        }

        public IEnumerable<Segment> GetSegments(float maxSagitta = 0)
        {
            var lineweightMm = LineWeight.ToMillimeters();
            var length = (float)Length;
            yield return new Segment(
            
                new Vector2((float)Start.X, (float)Start.Y),
                new Vector2((float)End.X, (float)End.Y),
                lineweightMm,
                lineweightMm,
                0.0f,
                length,
                0,
                ColorVector
            );
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