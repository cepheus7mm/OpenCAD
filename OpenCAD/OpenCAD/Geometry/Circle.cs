using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Geometry.Helpers.GeoPoints;
using OpenCAD.Interfaces;
using OpenCAD.SegmentSource;
using System.Net;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    /// <summary>
    /// Represents a circular arc defined by center point, radius, start angle, and end angle.
    /// Angles are in radians, measured counter-clockwise from the positive X-axis.
    /// </summary>
    public class Circle : GeometryBase, IDrawable, ICircularGeometry, ICurve, ISegmentSource
    {
        /// <summary>
        /// Gets or sets the center point of the arc.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Point3D Center
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(Center));
            set => SetPropertyValue(PropertyType.Point, nameof(Center), "Center", value);
        }

        /// <summary>
        /// Gets or sets the radius of the arc.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double Radius
        {
            get => GetPropertyValue<double>(PropertyType.DoubleLength, nameof(Radius));
            set => SetPropertyValue(PropertyType.DoubleLength, nameof(Radius), "Radius", Math.Max(0, value));
        
        }

        /// <summary>
        /// Gets the arc length.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public override double Length => Radius * GetSweepAngle();

        [JsonIgnore, XmlIgnore]
        public override double Angle => GetSweepAngle();

        /// <summary>
        /// Creates an arc with specified parameters.
        /// </summary>
        public Circle(Point3D center, double radius, OpenCADDocument? document = null)
            : base(document)
        {
            Center = center;
            Radius = Math.Max(0, radius);
        }

        /// <summary>
        /// Gets the sweep angle of the arc (always positive, counter-clockwise).
        /// </summary>
        public double GetSweepAngle()
        {
            return 2 * Math.PI;
        }

        /// <summary>
        /// Normalizes an angle to the range [0, 2π).
        /// </summary>
        private static double NormalizeAngle(double angle)
        {
            angle = angle % (2 * Math.PI);
            if (angle < 0)
                angle += 2 * Math.PI;
            return angle;
        }

        public override string ToString()
        {
            return $"Circle: Center={Center}, Radius={Radius:F3}";
        }

        public double GetParameterAtPoint(Point3D point)
        {
            double angle = Math.Atan2(point.Y - Center.Y, point.X - Center.X);

            if (angle < 0)
                angle += 2.0 * Math.PI;

            return angle / (2.0 * Math.PI);
        }

        public Point3D GetPointAtParameter(double parameter)
        {
            // Normalize to [0,1]
            double t = parameter - Math.Floor(parameter);

            return GeometricCalculator.GetPointAtParameter(t, StartPoint, Center, 2 * Math.PI);
        }

        public override Extents GetExtents()
        {
            // Full sweep = 2π
            return Extents.FromArc(
                new Point3D(Center.X + Radius, Center.Y, Center.Z),
                new Point3D(Center.X + Radius, Center.Y, Center.Z),
                Center,
                2 * Math.PI);
        }

        public IEnumerable<GeoPoint> GetGeoPoints(GeoPointModes geoPointType, Point3D referencePoint)
        {
            var candidates = new List<GeoPoint>();
            if (IsPreviewGeometry)
            {
                // Preview geometries do not provide geo points
                return candidates;
            }

            var segment = PolylineSegment.FromCircle(this);


            // Use unified segment calculation
            candidates.AddRange(segment.GetGeoPoints(referencePoint, geoPointType));

            // Set the RelatedGeometryId for all candidates
            foreach (var geoPoint in candidates)
            {
                geoPoint.RelatedGeometryId = ID;
            }

            //if (geoPointType.HasFlag(GeoPointModes.Center))
            //{
            //    candidates.Add(new GeoPoint(Center, GeoPointModes.Center) { RelatedGeometryId = ID });
            //}
            //if (geoPointType.HasFlag(GeoPointModes.NearestPoint))
            //{
            //    var geoPoint = new GeoPoint(GetClosestPoint(referencePoint, false), GeoPointModes.NearestPoint);
            //    geoPoint.RelatedGeometryId = ID;
            //    candidates.Add(geoPoint);
            //}
            //if (geoPointType.HasFlag(GeoPointModes.Quadrant))
            //{
            //    // Quadrant points at 0°, 90°, 180°, 270°
            //    double[] quadrantAngles = { 0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 };
            //    foreach (var angle in quadrantAngles)
            //    {
            //        var qPoint = new Point3D(
            //            Center.X + Radius * Math.Cos(angle),
            //            Center.Y + Radius * Math.Sin(angle),
            //            Center.Z
            //        );
            //        var geoPoint = new GeoPoint(qPoint, GeoPointModes.Quadrant);
            //        geoPoint.RelatedGeometryId = ID;
            //        candidates.Add(geoPoint);
            //    }
            //}
            //if (geoPointType.HasFlag(GeoPointModes.Perpendicular) && _document.PreviewPoint.HasValue)
            //{
            //    var geoPoint = new GeoPoint(GetClosestPoint(_document.PreviewPoint.Value, false), GeoPointModes.NearestPoint);
            //    geoPoint.RelatedGeometryId = ID;
            //    candidates.Add(geoPoint);
            //}

            //if (geoPointType.HasFlag(GeoPointModes.Tangent) && _document.PreviewPoint.HasValue)
            //{
            //    var geoPoint = GeometricCalculator.GetClosestTangent(Center, Radius, _document.PreviewPoint.Value, referencePoint);
            //    geoPoint.RelatedGeometryId = ID;
            //    candidates.Add(geoPoint);
            //}

            return candidates;
        }
        // ---------------------------------------------------------------------
        // ICurve implementation
        // ---------------------------------------------------------------------

        [JsonIgnore, XmlIgnore]
        public bool IsClosed => true;

        [JsonIgnore, XmlIgnore]
        public bool IsPeriodic => true;

        [JsonIgnore, XmlIgnore]
        public double DomainStart => 0.0;

        [JsonIgnore, XmlIgnore]
        public double DomainEnd => 1.0;

        [JsonIgnore, XmlIgnore]
        public Point3D StartPoint => new Point3D(Center.X + Radius, Center.Y, Center.Z);

        [JsonIgnore, XmlIgnore]
        public Point3D EndPoint => StartPoint;

        public Vector3D GetFirstDerivativeAtParameter(double t)
        {
            return GeometricCalculator.GetFirstDerivative(t, StartPoint, Center, GetSweepAngle());
        }

        public Vector3D GetSecondDerivativeAtParameter(double t)
        {
            return GeometricCalculator.GetSecondDerivative(t, StartPoint, Center, GetSweepAngle());
        }

        public double GetClosestParameter(Point3D point, bool extend = false)
        {
            return GeometricCalculator.GetClosestParameter(point, StartPoint, Center, GetSweepAngle(), extend);
        }

        public Point3D GetClosestPoint(Point3D point, bool extend = false)
        {
            return GeometricCalculator.GetClosestPoint(point, StartPoint, Center, GetSweepAngle(), extend);
        }

        public double GetLength()
        {
            return GeometricCalculator.GetLength(StartPoint, Center, GetSweepAngle());
        }

        public double GetLength(double t0, double t1)
        {
            double dt = Math.Abs(t1 - t0);
            var fullLength = GeometricCalculator.GetLength(StartPoint, Center, GetSweepAngle());
            return fullLength * dt;
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

            // Clamp to domain [0,1]
            double a = Math.Max(0.0, Math.Min(1.0, t0));
            double b = Math.Max(0.0, Math.Min(1.0, t1));

            // Evaluate new angles
            var point = GeometricCalculator.GetPointAtParameter(a, StartPoint, Center, 2 * Math.PI);
            var newStart = Math.Atan2(point.Y - Center.Y, point.X - Center.X);

            point = GeometricCalculator.GetPointAtParameter(b, StartPoint, Center, 2 * Math.PI);
            var newEnd = Math.Atan2(point.Y - Center.Y, point.X - Center.X);

            // Return a new Arc segment
            var newCircle = new Circle(Center, Radius, Document);
            newCircle.SetBasicPropertiesFrom(this);

            return newCircle;
        }

        public ICurve Transform(Matrix4D transform)
        {
            // Transform center and endpoints
            var transformedCenter = transform.Transform(Center);
            var transformedRadius = transform.Transform(Radius);

            // Update normal as well
            var tn = transform.TransformVector(_normal);

            // If radius collapsed, set angles to 0 and keep center
            if (transformedRadius < 1e-12)
            {
                var newCircle1 = new Circle(transformedCenter, transformedRadius, Document);
                newCircle1.SetBasicPropertiesFrom(this);
                newCircle1.SetNormal(tn);
                return newCircle1;
            }

            // Transform the stored normal vector
            var transformedNormal = transform.TransformVector(_normal);
            _normal = transformedNormal.Length > double.Epsilon ? transformedNormal.Normalized : transformedNormal;



            // Return a new Arc segment
            var newCircle = new Circle(transformedCenter, transformedRadius, Document);
            newCircle.SetBasicPropertiesFrom(this);
            newCircle.SetNormal(tn);

            return newCircle;
        }

        public ICurve[] GetOffsetCurves(double d)
        {
            return new ICurve[]
            {
                new Circle(Center, Radius + d, Document)
            };
        }

        public IEnumerable<Segment> GetSegments(float maxSagitta)
        {
            var geo = new List<GeoSegment>();

            CurveTessellator.TessellateCircle(
                new Vector2((float)Center.X, (float)Center.Y),
                (float)Radius,
                maxSagitta,
                geo
            );

            float cumulative = 0f;
            float widthMm = LineWeight.ToMillimeters();

            foreach (var g in geo)
            {
                float len = Vector2.Distance(g.A, g.B);

                yield return new Segment(
                    g.A,
                    g.B,
                    widthMm,
                    widthMm,
                    cumulative,
                    cumulative + len,
                    0,
                    ColorVector
                );

                cumulative += len;
            }
        }
    }
}