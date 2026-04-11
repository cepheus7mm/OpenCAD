using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    /// <summary>
    /// Represents a closed, filled polygon defined by straight edges.
    /// Used for solid fills such as closed arrowheads on dimensions.
    /// Parametric domain: t ∈ [0, N] where N = vertex count.
    /// Edge i runs from Vertices[i] to Vertices[(i+1) % N] over t ∈ [i, i+1].
    /// </summary>
    public class Polygon : GeometryBase, ICurve
    {
        #region Constructors

        public Polygon() : base() { }

        public Polygon(OpenCADDocument? doc) : base(doc) { }

        public Polygon(Point3D[] vertices, Color fillColor, OpenCADDocument? doc = null)
            : base(doc)
        {
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            FillColor = fillColor;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Ordered vertices defining the polygon boundary (implicitly closed).
        /// Stored via the property collection for consistent serialization.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Point3D[] Vertices
        {
            get => GetPropertyValue<Point3D[]>(PropertyType.PointArray, nameof(Vertices))
                   ?? Array.Empty<Point3D>();
            set => SetPropertyValue(PropertyType.PointArray, nameof(Vertices), "Vertices", value);
        }

        /// <summary>
        /// Number of vertices.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public int VertexCount => Vertices.Length;

        /// <summary>
        /// Fill color (includes alpha for transparency).
        /// Separate from the inherited <see cref="DrawableBase.Color"/> which represents the outline.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Color FillColor
        {
            get => GetPropertyValue<Color?>(PropertyType.Color, nameof(FillColor))
                   ?? Color.FromArgb(255, 255, 255, 255);
            set => SetPropertyValue<Color?>(PropertyType.Color, nameof(FillColor), "FillColor", value);
        }

        #endregion

        #region GeometryBase Overrides

        /// <summary>
        /// Perimeter of the polygon.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public override double Length => GetLength();

        [JsonIgnore, XmlIgnore]
        public override double Angle
        {
            get
            {
                var verts = Vertices;
                if (verts.Length < 2) return 0;
                return verts[0].AngleTo(verts[1]);
            }
        }

        public override Extents GetExtents()
        {
            var verts = Vertices;
            if (verts.Length == 0)
                return new Extents(Point3D.Origin, Point3D.Origin);

            var ext = new Extents(verts[0], verts[0]);
            for (int i = 1; i < verts.Length; i++)
                ext.Union(verts[i]);

            return ext;
        }

        #endregion

        #region ICurve Implementation

        public bool IsClosed => true;

        public bool IsPeriodic => true;

        public double DomainStart => 0;

        public double DomainEnd => VertexCount;

        public Point3D StartPoint => VertexCount > 0 ? Vertices[0] : Point3D.Origin;

        public Point3D EndPoint => StartPoint; // Closed

        public Point3D GetPointAtParameter(double t)
        {
            var verts = Vertices;
            int n = verts.Length;
            if (n == 0) return Point3D.Origin;

            // Wrap t into [0, n)
            t = ((t % n) + n) % n;

            int i = (int)Math.Floor(t);
            double frac = t - i;

            var a = verts[i % n];
            var b = verts[(i + 1) % n];

            return new Point3D(
                a.X + (b.X - a.X) * frac,
                a.Y + (b.Y - a.Y) * frac,
                a.Z + (b.Z - a.Z) * frac);
        }

        public Vector3D GetFirstDerivativeAtParameter(double t)
        {
            var verts = Vertices;
            int n = verts.Length;
            if (n < 2) return Vector3D.Zero;

            t = ((t % n) + n) % n;
            int i = (int)Math.Floor(t) % n;

            var a = verts[i];
            var b = verts[(i + 1) % n];

            return b - a; // Constant tangent along each edge
        }

        public Vector3D GetSecondDerivativeAtParameter(double t)
        {
            return Vector3D.Zero; // Straight edges have zero curvature
        }

        public double GetParameterAtPoint(Point3D point)
        {
            var verts = Vertices;
            int n = verts.Length;
            if (n < 2) return 0;

            double bestParam = 0;
            double minDistSq = double.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % n];

                double localT = ProjectOntoSegment(point, a, b);
                var cp = Lerp(a, b, localT);
                double distSq = (cp - point).LengthSquared;

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestParam = i + localT;
                }
            }

            return bestParam;
        }

        public double GetClosestParameter(Point3D point, bool extend = false)
        {
            return GetParameterAtPoint(point);
        }

        public Point3D GetClosestPoint(Point3D point, bool extend = false)
        {
            return GetPointAtParameter(GetClosestParameter(point, extend));
        }

        public ProjectionResult ProjectPoint(Point3D point)
        {
            double t = GetParameterAtPoint(point);
            var cp = GetPointAtParameter(t);
            double dist = cp.DistanceTo(point);
            return new ProjectionResult(t, cp, dist);
        }

        public double GetLength()
        {
            var verts = Vertices;
            int n = verts.Length;
            if (n < 2) return 0;

            double perimeter = 0;
            for (int i = 0; i < n; i++)
                perimeter += verts[i].DistanceTo(verts[(i + 1) % n]);

            return perimeter;
        }

        public double GetLength(double t0, double t1)
        {
            if (t1 < t0) (t0, t1) = (t1, t0);

            var verts = Vertices;
            int n = verts.Length;
            if (n < 2) return 0;

            double length = 0;
            int i0 = (int)Math.Floor(t0);
            int i1 = (int)Math.Floor(t1);

            if (i0 == i1)
            {
                var a = verts[i0 % n];
                var b = verts[(i0 + 1) % n];
                return Lerp(a, b, t0 - i0).DistanceTo(Lerp(a, b, t1 - i1));
            }

            // Partial first edge
            {
                var a = verts[i0 % n];
                var b = verts[(i0 + 1) % n];
                length += Lerp(a, b, t0 - i0).DistanceTo(b);
            }

            // Full edges in between
            for (int i = i0 + 1; i < i1; i++)
                length += verts[i % n].DistanceTo(verts[(i + 1) % n]);

            // Partial last edge
            {
                var a = verts[i1 % n];
                var b = verts[(i1 + 1) % n];
                length += a.DistanceTo(Lerp(a, b, t1 - i1));
            }

            return length;
        }

        public ICurve Trim(double t0, double t1)
        {
            // Trimming a closed polygon produces an open polyline
            if (t1 < t0) (t0, t1) = (t1, t0);

            var points = new List<Point3D> { GetPointAtParameter(t0) };

            int i0 = (int)Math.Ceiling(t0);
            int i1 = (int)Math.Floor(t1);
            var verts = Vertices;
            int n = verts.Length;

            for (int i = i0; i <= i1; i++)
                points.Add(verts[i % n]);

            points.Add(GetPointAtParameter(t1));

            // Return as a polyline (open)
            var pl = new Polyline(Document);
            foreach (var pt in points)
                pl.AddVertex(pt);

            return pl;
        }

        public ICurve ExtendTo(Point3D point)
        {
            // Closed polygons don't extend; return self unchanged
            return this;
        }

        public ICurve Transform(Matrix4D transform)
        {
            var verts = Vertices;
            var transformed = new Point3D[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                transformed[i] = verts[i].Transform(transform);

            var result = new Polygon(transformed, FillColor, Document);
            result.SetBasicPropertiesFrom(this);
            return result;
        }

        public ICurve[] GetOffsetCurves(double distance)
        {
            var verts = Vertices;
            int n = verts.Length;
            if (n < 3) return Array.Empty<ICurve>();

            var offset = new Point3D[n];
            for (int i = 0; i < n; i++)
            {
                var prev = verts[(i - 1 + n) % n];
                var curr = verts[i];
                var next = verts[(i + 1) % n];

                // Average of the two edge normals at this vertex
                var e1 = (curr - prev).Normalized;
                var e2 = (next - curr).Normalized;

                var n1 = new Vector3D(-e1.Y, e1.X, 0);
                var n2 = new Vector3D(-e2.Y, e2.X, 0);

                var avg = (n1 + n2).Normalized;

                // Miter length correction
                double dot = Vector3D.Dot(avg, n1);
                double miter = Math.Abs(dot) > 1e-10 ? distance / dot : distance;

                offset[i] = curr + avg * miter;
            }

            return [new Polygon(offset, FillColor, Document)];
        }

        #endregion

        #region Private Helpers

        private static double ProjectOntoSegment(Point3D p, Point3D a, Point3D b)
        {
            var ab = b - a;
            var ap = p - a;
            double denom = Vector3D.Dot(ab, ab);
            if (denom < 1e-12) return 0;
            return Math.Clamp(Vector3D.Dot(ap, ab) / denom, 0, 1);
        }

        private static Point3D Lerp(Point3D a, Point3D b, double t)
        {
            return new Point3D(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }

        #endregion
    }
}