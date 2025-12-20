using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    /// <summary>
    /// Represents a circular arc defined by center point, radius, start angle, and end angle.
    /// Angles are in radians, measured counter-clockwise from the positive X-axis.
    /// </summary>
    public class Circle : GeometryBase, IDrawable, ICircularGeometry
    {
        /// <summary>
        /// Gets or sets the center point of the arc.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Point3D Center
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(Center)) ?? new Point3D(0, 0, 0);
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

        [JsonIgnore, XmlIgnore]
        public double StartAngle => 0;

        [JsonIgnore, XmlIgnore]
        public double EndAngle => 2 * Math.PI;

        /// <summary>
        /// Creates an arc with specified parameters.
        /// </summary>
        public Circle(Point3D center, double radius, OpenCADDocument? document = null)
            : base(document)
        {
            Center = center ?? new Point3D(0, 0, 0);
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

        public override Vector3D? GetFirstDerivate(Point3D point)
        {
            throw new NotImplementedException();
        }

        public override Vector3D? GetSecondDerivate(Point3D point)
        {
            throw new NotImplementedException();
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
            // Vector from center to query point
            var pointVec = point - Center;

            // Handle degenerate radius
            if (Radius < double.Epsilon)
                return Center;

            // Direction from center to point
            var dir = pointVec.Normalized;

            // Project onto circle
            var projected = Center + dir * Radius;

            // Angle of direction
            double angle = Math.Atan2(dir.Y, dir.X);

            angle = NormalizeAngle(angle);

            // Return point on circle
            return Center + new Vector3D(Math.Cos(angle), Math.Sin(angle), 0) * Radius;
        }

        public override Extents GetExtents()
        {
            // Initialize
            var minX = double.MaxValue;
            var maxX = double.MinValue;
            var minY = double.MaxValue;
            var maxY = double.MinValue;
            var minZ = double.MaxValue;
            var maxZ = double.MinValue;

            // Check if cardinal points fall within the arc's sweep
            // Cardinal angles: 0° (right), 90° (top), 180° (left), 270° (bottom)
            double[] cardinalAngles = { 0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 };

            foreach (var angle in cardinalAngles)
            {
                var x = Center.X + Radius * Math.Cos(angle);
                var y = Center.Y + Radius * Math.Sin(angle);

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }

            return new Extents
            {
                Min = new Point3D(minX, minY, minZ),
                Max = new Point3D(maxX, maxY, maxZ)
            };
        }

        public override bool Transform(Matrix4D transformation)
        {
            // Preserve original geometry points
            var originalCenter = Center.Clone();
            var originalRadius = Radius;

            // Transform center and endpoints
            var transformedCenter = transformation.Transform(originalCenter);
            var transformedRadius = transformation.Transform(originalRadius);

            // Update center
            Center = transformedCenter;

            Radius = transformedRadius;

            // If radius collapsed, reset to original radius
            if (Radius < double.Epsilon)
            {
                Radius = originalRadius;
                return false;
            }

            return true;
        }

        public override IEnumerable<GeoPoint> GetGeoPoints(Point3D referencePoint, GeoPointModes geoPointType)
        {
            var candidates = new List<GeoPoint>();
            if (IsPreviewGeometry)
            {
                // Preview geometries do not provide geo points
                return candidates;
            }
            if (geoPointType.HasFlag(GeoPointModes.Center))
            {
                candidates.Add(new GeoPoint(Center, GeoPointModes.Center) { RelatedGeometryId = ID });
            }
            if (geoPointType.HasFlag(GeoPointModes.NearestPoint))
            {
                var geoPoint = new GeoPoint(GetClosestPointTo(referencePoint, false), GeoPointModes.NearestPoint);
                geoPoint.RelatedGeometryId = ID;
                candidates.Add(geoPoint);
            }
            if (geoPointType.HasFlag(GeoPointModes.Quadrant))
            {
                // Quadrant points at 0°, 90°, 180°, 270°
                double[] quadrantAngles = { 0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 };
                foreach (var angle in quadrantAngles)
                {
                    var qPoint = new Point3D(
                        Center.X + Radius * Math.Cos(angle),
                        Center.Y + Radius * Math.Sin(angle),
                        Center.Z
                    );
                    var geoPoint = new GeoPoint(qPoint, GeoPointModes.Quadrant);
                    geoPoint.RelatedGeometryId = ID;
                    candidates.Add(geoPoint);
                }
            }
            if (geoPointType.HasFlag(GeoPointModes.Perpendicular) && _document.PreviewPoint is not null)
            {
                var geoPoint = new GeoPoint(GetClosestPointTo(_document.PreviewPoint, false), GeoPointModes.NearestPoint);
                geoPoint.RelatedGeometryId = ID;
                candidates.Add(geoPoint);
            }

            if (geoPointType.HasFlag(GeoPointModes.Tangent) && _document.PreviewPoint is not null)
            {
                var geoPoint = GeometricCalculator.GetClosestTangent(Center, Radius, _document.PreviewPoint, referencePoint);
                geoPoint.RelatedGeometryId = ID;
                candidates.Add(geoPoint);
            }

            return candidates;
        }
    }
}