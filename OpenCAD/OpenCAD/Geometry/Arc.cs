using OpenCAD.Interfaces;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    /// <summary>
    /// Represents a circular arc defined by center point, radius, start angle, and end angle.
    /// Angles are in radians, measured counter-clockwise from the positive X-axis.
    /// </summary>
    public class Arc : GeometryBase, IDrawable
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
        /// Gets or sets the start angle in radians (counter-clockwise from positive X-axis).
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double StartAngle
        {
            get => GetPropertyValue<double>(PropertyType.DoubleAngle, nameof(StartAngle));
            set => SetPropertyValue(PropertyType.DoubleAngle, nameof(StartAngle), "Start Angle", NormalizeAngle(value));
        }

        /// <summary>
        /// Gets or sets the end angle in radians (counter-clockwise from positive X-axis).
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double EndAngle
        {
            get => GetPropertyValue<double>(PropertyType.DoubleAngle, nameof(EndAngle));
            set => SetPropertyValue(PropertyType.DoubleAngle, nameof(EndAngle), "End Angle", NormalizeAngle(value));
        }

        /// <summary>
        /// Gets the start point of the arc.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Point3D StartPoint => new Point3D(
            Center.X + Radius * Math.Cos(StartAngle),
            Center.Y + Radius * Math.Sin(StartAngle),
            Center.Z
        );

        /// <summary>
        /// Gets the end point of the arc.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Point3D EndPoint => new Point3D(
            Center.X + Radius * Math.Cos(EndAngle),
            Center.Y + Radius * Math.Sin(EndAngle),
            Center.Z
        );

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
        public Arc(Point3D center, double radius, double startAngle, double endAngle, OpenCADDocument? document = null)
            : base(document)
        {
            Center = center ?? new Point3D(0, 0, 0);
            Radius = Math.Max(0, radius);
            StartAngle = NormalizeAngle(startAngle);
            EndAngle = NormalizeAngle(endAngle);
        }

        /// <summary>
        /// Gets the sweep angle of the arc (always positive, counter-clockwise).
        /// </summary>
        public double GetSweepAngle()
        {
            double sweep = EndAngle - StartAngle;
            if (sweep < 0)
                sweep += 2 * Math.PI;
            return sweep;
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
            return $"Arc: Center={Center}, Radius={Radius:F3}, StartAngle={StartAngle * 180 / Math.PI:F1}°, EndAngle={EndAngle * 180 / Math.PI:F1}°";
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

            // Normalize angles to [0, 2π)
            double start = NormalizeAngle(StartAngle);
            double end = NormalizeAngle(EndAngle);
            angle = NormalizeAngle(angle);

            if (!extend)
            {
                if (StartAngle <= EndAngle)
                {
                    // Normal case
                    if (angle < StartAngle) angle = StartAngle;
                    if (angle > EndAngle) angle = EndAngle;
                }
                else
                {
                    // Wrap-around case
                    bool inArc = (angle >= StartAngle) || (angle <= EndAngle);
                    if (!inArc)
                    {
                        // Clamp to whichever endpoint is closer
                        double distToStart = Math.Abs(angle - StartAngle);
                        double distToEnd = Math.Abs(angle - EndAngle);
                        angle = (distToStart < distToEnd) ? StartAngle : EndAngle;
                    }
                }

            }

            // Return point on arc
            return Center + new Vector3D(Math.Cos(angle), Math.Sin(angle), 0) * Radius;
        }

        public override bool Transform(Matrix4D transformation)
        {
            // Preserve original geometry points
            var originalCenter = Center.Clone();
            var originalStart = StartPoint;
            var originalEnd = EndPoint;

            // Transform center and endpoints
            var transformedCenter = transformation.Transform(originalCenter);
            var transformedStart = transformation.Transform(originalStart);
            var transformedEnd = transformation.Transform(originalEnd);

            // Update center
            Center = transformedCenter;

            // Compute new radius from transformed endpoints (average to better tolerate slight non-uniform scaling)
            var distStart = transformedCenter.DistanceTo(transformedStart);
            var distEnd = transformedCenter.DistanceTo(transformedEnd);
            var newRadius = (distStart + distEnd) / 2.0;
            Radius = newRadius;

            // If radius collapsed, set angles to 0 and keep center
            if (Radius < double.Epsilon)
            {
                StartAngle = 0;
                EndAngle = 0;
                // Update normal as well
                var tn = transformation.TransformVector(_normal);
                _normal = tn.Length > double.Epsilon ? tn.Normalized : tn;
                return true;
            }

            // Recompute start/end angles from transformed geometry (angles measured from +X counter-clockwise)
            var sAngle = transformedCenter.AngleTo(transformedStart);
            var eAngle = transformedCenter.AngleTo(transformedEnd);

            StartAngle = NormalizeAngle(sAngle);
            EndAngle = NormalizeAngle(eAngle);

            // Transform the stored normal vector
            var transformedNormal = transformation.TransformVector(_normal);
            _normal = transformedNormal.Length > double.Epsilon ? transformedNormal.Normalized : transformedNormal;

            return true;
        }
    }
}