using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Geometry.Helpers.GeoPoints;
using OpenCAD.Interfaces;
using OpenCAD.SegmentSource;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Runtime.Intrinsics;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    /// <summary>
    /// Represents a circular arc defined by center point, radius, start angle, and end angle.
    /// Angles are in radians, measured counter-clockwise from the positive X-axis.
    /// </summary>
    public class Arc : GeometryBase, IDrawable, ICurve, ICircularGeometry, ISegmentSource
    {
        // ---------------------------------------------------------------------
        // Private fields
        // ---------------------------------------------------------------------
        // (none declared in this class — reserved for future use)

        // ---------------------------------------------------------------------
        // Constructors
        // ---------------------------------------------------------------------

        /// <summary>
        /// Creates an arc with specified parameters.
        /// </summary>
        public Arc(Point3D center, double radius, double startAngle, double endAngle, OpenCADDocument? document = null, bool sweep = false)
            : base(document)
        {
            Center = center;
            Radius = Math.Max(0, radius);
            StartAngle = GeometricCalculator.NormalizeUnsigned(startAngle);
            EndAngle = GeometricCalculator.NormalizeUnsigned(sweep ? startAngle + endAngle : endAngle);
        }

        public Arc(Point3D center, Point3D start, Point3D end, OpenCADDocument? document = null)
            : base(document)
        {
            Center = center;
            Radius = center.DistanceTo(start);
            StartAngle = GeometricCalculator.NormalizeUnsigned(center.AngleTo(start));
            EndAngle = GeometricCalculator.NormalizeUnsigned(center.AngleTo(end));
        }
        public Arc(Point3D center, Point3D start, double sweep, OpenCADDocument? document = null)
            : base(document)
        {
            Center = center;
            Radius = center.DistanceTo(start);
            var angle1 = GeometricCalculator.NormalizeUnsigned(center.AngleTo(start));
            var angle2 = angle1 + sweep;
            StartAngle = sweep >= 0 ? angle1 : angle2;
            EndAngle = sweep >= 0 ? angle2 : angle1;
        }

        public static Arc Empty => new Arc(Point3D.Origin, 0, 0, 0);

        // ---------------------------------------------------------------------
        // Base class overrides (GeometryBase)
        // ---------------------------------------------------------------------

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
        /// Gets or sets the start angle in radians (counter-clockwise from positive X-axis).
        /// Stored normalized to (0, 2π].
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double StartAngle
        {
            get => GetPropertyValue<double>(PropertyType.DoubleAngle, nameof(StartAngle));
            set => SetPropertyValue(PropertyType.DoubleAngle, nameof(StartAngle), "Start Angle", GeometricCalculator.NormalizeUnsigned(value));
        }

        /// <summary>
        /// Gets or sets the end angle in radians (counter-clockwise from positive X-axis).
        /// Stored normalized to (0, 2π].
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double EndAngle
        {
            get => GetPropertyValue<double>(PropertyType.DoubleAngle, nameof(EndAngle));
            set => SetPropertyValue(PropertyType.DoubleAngle, nameof(EndAngle), "End Angle", GeometricCalculator.NormalizeUnsigned(value));
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

        public override string ToString()
        {
            return $"Arc: Center={Center}, Radius={Radius:F3}, StartAngle={StartAngle * 180 / Math.PI:F1}°, EndAngle={EndAngle * 180 / Math.PI:F1}°";
        }

        public override Extents GetExtents()
        {
            return Extents.FromArc(StartPoint, EndPoint, Center, Angle);
        }

        public IEnumerable<GeoPoint> GetGeoPoints(GeoPointModes geoPointType, Point3D referencePoint)
        {
            var candidates = new List<GeoPoint>();
            if (IsPreviewGeometry)
            {
                // Preview geometries do not provide geo points
                return candidates;
            }

            //// Calculate bulge from arc parameters
            //double bulge = Math.Tan(Angle / 4.0);

            var segment = PolylineSegment.FromArc(this);

            // Use unified segment calculation
            candidates.AddRange(segment.GetGeoPoints(referencePoint, geoPointType));

            // Set the RelatedGeometryId for all candidates
            foreach (var geoPoint in candidates)
            {
                geoPoint.RelatedGeometryId = ID;
            }

            //// Arc-specific: Perpendicular point (requires document preview point)
            //if (geoPointType.HasFlag(GeoPointModes.Perpendicular) && _document.PreviewPoint.HasValue)
            //{
            //    candidates.Add(GeometricCalculator.Perpendicular(_document.PreviewPoint.Value, this));
            //}

            //// Arc-specific: Tangent point (requires document preview point)
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

        public bool IsClosed => false;

        public bool IsPeriodic => false;

        public double DomainStart => 0.0;

        public double DomainEnd => 1.0;

        public double SweepAngle => GetSweepAngle();

        public double GetParameterAtPoint(Point3D point)
        {
            return GeometricCalculator.GetParameterAtPoint(point, StartPoint, Center, GetSweepAngle());
        }

        public Point3D GetPointAtParameter(double parameter)
        {
            return GeometricCalculator.GetPointAtParameter(parameter, StartPoint, Center, GetSweepAngle());
        }

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
            //double a = Math.Max(0.0, Math.Min(1.0, t0));
            //double b = Math.Max(0.0, Math.Min(1.0, t1));

            // Evaluate new angles
            var point = GeometricCalculator.GetPointAtParameter(t0, StartPoint, Center, GetSweepAngle());
            var newStart = Math.Atan2(point.Y - Center.Y, point.X - Center.X);

            point = GeometricCalculator.GetPointAtParameter(t1, StartPoint, Center, GetSweepAngle());
            var newEnd = Math.Atan2(point.Y - Center.Y, point.X - Center.X);

            // Return a new Arc segment
            var newArc = new Arc(Center, Radius, newStart, newEnd, Document);
            newArc.SetBasicPropertiesFrom(this);

            return newArc;
        }

        public ICurve Transform(Matrix4D transform)
        {
            // Transform center and endpoints
            var transformedCenter = transform.Transform(Center);
            var transformedStart = transform.Transform(transform.IsMirror ? EndPoint : StartPoint);
            var transformedEnd = transform.Transform(transform.IsMirror ? StartPoint : EndPoint);

            // Compute new radius from transformed endpoints (average to better tolerate slight non-uniform scaling)
            var distStart = transformedCenter.DistanceTo(transformedStart);
            var distEnd = transformedCenter.DistanceTo(transformedEnd);
            var newRadius = (distStart + distEnd) / 2.0;

            // Recompute start/end angles from transformed geometry (angles measured from +X counter-clockwise)
            var sAngle = transformedCenter.AngleTo(transformedStart);
            var eAngle = transformedCenter.AngleTo(transformedEnd);
            // Update normal as well
            var tn = transform.TransformVector(_normal);

            // If radius collapsed, set angles to 0 and keep center
            if (Radius < double.Epsilon)
            {
                sAngle = 0;
                eAngle = 0;
                var newArc1 = new Arc(Center, Radius, sAngle, eAngle, Document);
                newArc1.SetBasicPropertiesFrom(this);
                newArc1.SetNormal(tn);
                return newArc1;
            }


            sAngle = GeometricCalculator.NormalizeUnsigned(sAngle);
            eAngle = GeometricCalculator.NormalizeUnsigned(eAngle);


            // Return a new Arc segment
            var newArc = new Arc(transformedCenter, newRadius, sAngle, eAngle, Document);
            newArc.SetBasicPropertiesFrom(this);
            newArc.SetNormal(tn);

            return newArc;
        }

        public ICurve[] GetOffsetCurves(double d)
        {
            return new ICurve[]
            {
                new Arc(Center, Radius - d, StartAngle, EndAngle, Document)
            };
        }

        // ---------------------------------------------------------------------
        // ICircularGeometry / IDrawable (interface-related) - no explicit members beyond properties
        // ---------------------------------------------------------------------

        /// <summary>
        /// Gets the sweep angle of the arc (always positive, counter-clockwise).
        /// </summary>
        public double GetSweepAngle()
        {
            return AngleUtils.NormalizeSweepCCW(EndAngle - StartAngle);
        }

        // ---------------------------------------------------------------------
        // Internal methods
        // ---------------------------------------------------------------------
        // (none beyond existing public overrides and helpers)

        // ---------------------------------------------------------------------
        // Private methods
        // ---------------------------------------------------------------------

        /// <summary>
        /// Checks if a given angle falls within the arc's sweep.
        /// </summary>
        private bool IsAngleInArc(double angle)
        {
            angle = GeometricCalculator.NormalizeUnsigned(angle);
            var start = GeometricCalculator.NormalizeUnsigned(StartAngle);
            var end = GeometricCalculator.NormalizeUnsigned(EndAngle);

            if (start <= end)
            {
                // Normal case: no wrap-around
                return angle >= start && angle <= end;
            }
            else
            {
                // Wrap-around case: arc crosses 0°
                return angle >= start || angle <= end;
            }
        }

        public IEnumerable<Segment> GetSegments(float maxSagitta)
        {
            var geo = new List<GeoSegment>();
            
            CurveTessellator.TessellateArc(
                new Vector2((float)Center.X, (float)Center.Y),
                (float)Radius,
                (float)StartAngle,
                (float)(StartAngle + SweepAngle),
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

        public ProjectionResult ProjectPoint(Point3D point)
        {
            // Vector from center to point
            var v = point - Center;

            // Angle of point relative to center
            var ang = Math.Atan2(v.Y, v.X);

            // Normalize angle into same CCW frame as arc
            var rel = GeometricCalculator.NormalizeUnsigned(ang - StartAngle);

            // Unclamped parameter
            var t = rel / SweepAngle;

            // Closest point ON THE CIRCLE (not the arc)
            var cp = new Point3D(
                Center.X + Radius * Math.Cos(ang),
                Center.Y + Radius * Math.Sin(ang),
                Center.Z
            );

            var diff = point - cp;
            var dist = Math.Sqrt(Vector3D.Dot(diff, diff));

            return new ProjectionResult(t, cp, dist);
        }

        public ICurve ExtendTo(Point3D point)
        {
            var proj = ProjectPoint(point);
            var t = proj.Parameter;

            if (t <= DomainStart)
                return Trim(t, DomainEnd);

            if (t >= DomainEnd)
                return Trim(DomainStart, t);

            // Inside domain: extend whichever end is closer
            var distToStart = Math.Abs(t - DomainStart);
            var distToEnd = Math.Abs(t - DomainEnd);

            if (distToStart < distToEnd)
                return Trim(t, DomainEnd);
            else
                return Trim(DomainStart, t);
        }
    }
}