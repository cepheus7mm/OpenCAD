using OpenCAD.Geometry.Calculator;
using OpenCAD.Geometry.Helpers;
using OpenCAD.Interfaces;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    /// <summary>
    /// Represents a polyline - a Index of connected line and arc segments defined by vertices
    /// </summary>
    public class Polyline : GeometryBase, ICurve, IGeoPointProvider
    {
        #region Private Fields

        private readonly PolylineSegmentCache _segments;
        private readonly VertexCache _verticies;

        #endregion

        #region Constructors

        /// <summary>
        /// Parameterless constructor required for deserialization
        /// </summary>
        public Polyline() : base()
        {
            _segments = new PolylineSegmentCache(this);
            _verticies = new VertexCache(this);
        }

        public Polyline(OpenCADDocument doc) : base(doc)
        {
            _segments = new PolylineSegmentCache(this);
            _verticies = new VertexCache(this);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Whether the polyline is closed (last vertex connects to first)
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsClosed
        {
            get
            {
                var vertices = GetOrderedVertices();
                if (vertices.Count < 2)
                    return false;

                return vertices[0].Position.DistanceTo(vertices[^1].Position) < 1e-8;
            }
        }

        /// <summary>
        /// Number of vertices in the polyline
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public int VertexCount => _verticies.Count;

        #endregion

        #region GeometryBase Overrides

        /// <summary>
        /// Total length of all segments
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public override double Length => GetLength();

        /// <summary>
        /// Angle of the first segment (or overall direction)
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public override double Angle
        {
            get
            {
                if (VertexCount < 2)
                    return 0;

                var first = GetVertex(0);
                var second = GetVertex(1);

                return first!.Position.AngleTo(second!.Position);
            }
        }

        public override Extents GetExtents()
        {
            var segments = _segments.Items;

            if (segments.Count == 0)
                return new Extents(Point3D.Origin, Point3D.Origin);

            Extents ext = segments[0].GetExtents();

            for (int i = 1; i < segments.Count; i++)
                ext.Union(segments[i].GetExtents());

            return ext;
        }

        public IEnumerable<GeoPoint> GetGeoPoints(GeoPointModes geoPointType, Point3D referencePoint)
        {
            var candidates = new List<GeoPoint>();

            if (IsPreviewGeometry)
                return candidates;

            var segments = _segments.Items;
            if (segments.Count == 0)
                return candidates;

            // Deduplicate vertex snaps only
            var seenVertices = new HashSet<Point3D>(Point3DComparer.Instance);

            foreach (var seg in segments)
            {
                var segGeoPoints = seg.GetGeoPoints(referencePoint, geoPointType);

                foreach (var gp in segGeoPoints)
                {
                    gp.RelatedGeometryId = ID;

                    if (gp.PointType == GeoPointModes.Vertex)
                    {
                        if (seenVertices.Add(gp.Position))
                            candidates.Add(gp);
                    }
                    else
                    {
                        candidates.Add(gp);
                    }
                }
            }

            return candidates;
        }



        #endregion

        #region ICurve Implementation

        public bool IsPeriodic => false;

        public double DomainStart => 0;

        public double DomainEnd => IsClosed ? VertexCount : VertexCount - 1;

        public Point3D StartPoint => GetVertex(0)?.Position ?? Point3D.NotAPoint;

        public Point3D EndPoint => IsClosed ? StartPoint : GetOrderedVertices()?.LastOrDefault()?.Position ?? Point3D.NotAPoint;

        public double GetParameterAtPoint(Point3D point)
        {
            var segments = _segments.Items;

            double bestParam = 0.0;
            double minDistSq = double.MaxValue;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];

                // 1. Check if point lies exactly on this segment
                if (seg.IsPointOnSegment(point, tolerance: 1e-9))
                {
                    double local = seg.GetClosestParameter(point, extend: false);
                    return i + local;
                }

                // 2. Otherwise compute closest point on this segment
                double localParam = seg.GetClosestParameter(point, extend: false);
                Point3D closest = seg.GetPointAt(localParam);

                double distSq = (closest - point).LengthSquared;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestParam = i + localParam;
                }
            }

            return bestParam;
        }

        public Point3D GetPointAtParameter(double t)
        {
            var (segment, _, localT) = _segments.GetSegmentT(t);

            return segment.GetPointAt(localT);
        }

        public Vector3D GetFirstDerivativeAtParameter(double t)
        {
            var (seg, _, localT) = _segments.GetSegmentT(t);
            var derivative = seg.GetFirstDerivative(localT);
            if (!double.IsNaN(derivative.X) && !double.IsNaN(derivative.Y) && !double.IsNaN(derivative.Z))
            {
                return derivative;
            }
            // try previous
            (seg, _, localT) = _segments.GetSegmentT(t - 1);
            return seg.GetFirstDerivative(localT + 1);
        }

        public Vector3D GetSecondDerivativeAtParameter(double t)
        {
            var (seg, _, localT) = _segments.GetSegmentT(t);
            return seg.GetSecondDerivative(localT);
        }

        public double GetClosestParameter(Point3D point, bool extend = false)
        {
            var segments = _segments.Items;

            if (segments.Count == 0)
                return 0;

            double bestParam = 0.0;
            double minDistSq = double.MaxValue;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];

                // Local closest parameter on this segment
                double localT = seg.GetClosestParameter(point, extend);

                // Closest point on this segment
                Point3D closest = seg.GetPointAt(localT);

                // Distance to the query point
                double distSq = (closest - point).LengthSquared;

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestParam = i + localT;
                }
            }

            return bestParam;
        }

        public Point3D GetClosestPoint(Point3D point, bool extend = false)
        {
            var segments = _segments.Items;

            if (segments.Count == 0)
                return default;

            double minDistSq = double.MaxValue;
            Point3D bestPoint = default;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];

                // Local closest parameter on this segment
                double localT = seg.GetClosestParameter(point, extend);

                // Closest point on this segment
                Point3D candidate = seg.GetPointAt(localT);

                // Compare distances
                double distSq = (candidate - point).LengthSquared;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestPoint = candidate;
                }
            }

            return bestPoint;
        }

        public double GetLength()
        {
            double total = 0.0;
            foreach (var seg in _segments.Items)
                total += seg.GetLength();
            return total;
        }

        public double GetLength(double t0, double t1)
        {
            var segments = _segments.Items;

            if (segments.Count == 0)
                return 0;

            // Normalize order
            if (t1 < t0)
                (t0, t1) = (t1, t0);

            // Clamp to valid domain
            double maxT = segments.Count;
            t0 = Math.Clamp(t0, 0, maxT);
            t1 = Math.Clamp(t1, 0, maxT);

            // If both parameters fall in the same segment
            int i0 = (int)Math.Floor(t0);
            int i1 = (int)Math.Floor(t1);

            if (i0 == i1)
            {
                double local0 = t0 - i0;
                double local1 = t1 - i1;
                return segments[i0].GetLength(local0, local1);
            }

            double length = 0;

            // --- 1. Partial length on first segment ---
            {
                double local0 = t0 - i0;
                length += segments[i0].GetLength(local0, 1.0);
            }

            // --- 2. Full segments in between ---
            for (int i = i0 + 1; i < i1; i++)
                length += segments[i].GetLength();

            // --- 3. Partial length on last segment ---
            {
                double local1 = t1 - i1;
                length += segments[i1].GetLength(0.0, local1);
            }

            return length;
        }

        public ICurve Trim(double t0, double t1)
        {
            var segments = _segments.Items;

            if (segments.Count == 0)
                return null;

            // Normalize order
            if (t1 < t0)
                (t0, t1) = (t1, t0);

            double maxT = segments.Count;
            t0 = Math.Clamp(t0, 0, maxT);
            t1 = Math.Clamp(t1, 0, maxT);

            int i0 = (int)Math.Floor(t0);
            int i1 = (int)Math.Floor(t1);

            double local0 = t0 - i0;
            double local1 = t1 - i1;

            List<PolylineSegment> newSegs = new();

            if (i0 == i1)
            {
                // Entire trim lies within one segment
                newSegs.Add(segments[i0].Trim(local0, local1));
            }
            else
            {
                // First partial segment
                newSegs.Add(segments[i0].Trim(local0, 1.0));

                // Middle full segments
                for (int i = i0 + 1; i < i1; i++)
                    newSegs.Add(segments[i]);

                // Last partial segment
                newSegs.Add(segments[i1].Trim(0.0, local1));
            }

            // Convert back to a polyline curve
            return PolylineFromSegments(newSegs);
        }

        public ICurve Transform(Matrix4D transform)
        {
            var segments = _segments.Items;

            List<Point3D> verts = new();
            List<double> bulges = new();

            foreach (var seg in segments)
            {
                var tseg = seg.Transform(transform);

                verts.Add(tseg.Start);
                bulges.Add(tseg.IsLine ? 0 : Math.Tan(tseg.Sweep / 4));
            }

            verts.Add(segments[^1].End.Transform(transform));

            return PolylineFromVerticiesAndBulges(verts, bulges);
        }

        #endregion


        #region Vertex Management

        public IEnumerable<PolylineVertex> Vertices => GetOrderedVertices();

        public IEnumerable<double> Bulges => GetOrderedVertices().Select(v => v.Bulge);

        public void MarkDirty()
        {
            _segments.MarkDirty();
            _verticies.MarkDirty();
        }

        /// <summary>
        /// Adds a vertex to the end of the polyline
        /// </summary>
        public PolylineVertex AddVertex(Point3D position, double bulge = 0.0)
        {
            var vertex = new PolylineVertex(_document, position, bulge)
            {
                Index = (uint)_verticies.Count
            };

            if (Add(vertex))
                MarkDirty();
            return vertex;
        }

        public void AddVertex(PolylineVertex vertex)
        {
            vertex.Index = (uint)_verticies.Count;
            if (Add(vertex))
                MarkDirty();
        }

        /// <summary>
        /// Inserts a vertex at the specified index
        /// </summary>
        public void InsertVertex(int index, Point3D position, double bulge = 0.0)
        {
            if (index < 0 || index > VertexCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            var vertices = GetOrderedVertices();

            // Create new vertex
            var newVertex = new PolylineVertex(_document, position, bulge);

            // Add the new vertex to the document update indices if successful
            if (Add(newVertex))
            {
                for (int i = index; i < vertices.Count; i++)
                    vertices[i].Index++;

                MarkDirty();
            }
        }

        /// <summary>
        /// Removes the vertex at the specified index
        /// </summary>
        public bool RemoveVertex(int index)
        {
            var vertices = GetOrderedVertices();
            if (index < 0 || index >= vertices.Count)
                return false;

            foreach (var v in vertices)
            {
                if (v.Index > index)
                {
                    v.Index--;
                }
            }

            var result = Remove(vertices[index]);

            if (result)
            {
                MarkDirty();
            }

            return result;
        }

        /// <summary>
        /// Removes a vertex at the specified position (with tolerance)
        /// </summary>
        public bool RemoveVertexAt(Point3D position, double tolerance = 1e-6)
        {
            var vertex = GetOrderedVertices()
                .FirstOrDefault(v => v.Position.DistanceTo(position) < tolerance);

            var result = false;
            if (vertex != null && (result = Remove(vertex)))
            {
                MarkDirty();
            }

            return result;
        }

        /// <summary>
        /// Clears all vertices
        /// </summary>
        public void ClearVertices()
        {
            var vertices = GetOrderedVertices();
            foreach (var vertex in vertices)
            {
                Remove(vertex);
            }
            MarkDirty();
        }

        /// <summary>
        /// Gets all vertices in order (ordered by their addition Index, which is preserved in the children dictionary)
        /// </summary>
        public IReadOnlyList<PolylineVertex> GetVertices()
        {
            return GetOrderedVertices();
        }

        /// <summary>
        /// Gets the vertex at the specified index
        /// </summary>
        public PolylineVertex? GetVertex(int index)
        {
            var vertices = GetOrderedVertices();
            if (index < 0 || index >= vertices.Count)
                return null;

            return vertices[index];
        }

        /// <summary>
        /// Helper to get vertices in deterministic order
        /// </summary>
        public IReadOnlyList<PolylineVertex> GetOrderedVertices()
        {
            return _verticies.Items
                .Select(id => (PolylineVertex)children[id])
                .ToList();
        }

        #endregion

        #region Segment Queries

        /// <summary>
        /// Gets the number of segments (edges) in the polyline
        /// </summary>
        public int GetSegmentCount()
        {
            return _segments.Count;
        }

        /// <summary>
        /// Gets segment information (start point, end point, bulge)
        /// </summary>
        public (Point3D start, Point3D end, double bulge) GetSegment(int index)
        {
            var segment = _segments[index];

            return (segment.Start, segment.End, segment.Bulge);
        }

        #endregion

        #region Editing

        /// <summary>
        /// Sets the position of a vertex at the specified index
        /// </summary>
        public void SetVertexPosition(int index, Point3D newPosition)
        {
            var vertex = GetVertex(index);
            if (vertex != null)
            {
                vertex.Position = newPosition;
                MarkDirty();
            }
        }

        /// <summary>
        /// Sets the bulge value of a vertex at the specified index
        /// </summary>
        public void SetVertexBulge(int index, double newBulge)
        {
            var vertex = GetVertex(index);
            if (vertex != null)
            {
                vertex.Bulge = newBulge;
                MarkDirty();
            }
        }

        /// <summary>
        /// Moves a vertex by the specified offset
        /// </summary>
        public bool MoveVertex(int index, Vector3D offset)
        {
            var vertex = GetVertex(index);
            if (vertex == null)
                return false;

            vertex.Position = vertex.Position + offset;
            _segments.MarkDirty();
            return true;
        }

        /// <summary>
        /// Reverses the direction of the polyline
        /// </summary>
        public void Reverse()
        {
            // Get a mutable list of vertices in order
            var vertices = GetOrderedVertices().ToList();

            // Reverse the order
            vertices.Reverse();

            // Fix bulges and indices
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];

                // New index
                v.Index = (uint)i;

                // Bulge logic
                if (i == 0 && !IsClosed)
                {
                    v.Bulge = 0;
                }
                else
                {
                    int prev = (i - 1 + vertices.Count) % vertices.Count;
                    v.Bulge = -vertices[prev].Bulge;
                }
            }

            // Update the cached vertex order
            MarkDirty();
        }

        /// <summary>
        /// Closes the polyline (sets IsClosed = true)
        /// </summary>
        public void Close()
        {
            if (IsClosed || VertexCount < 2)
                return;
            var firstVertex = GetVertex(0);
            AddVertex(firstVertex);
        }

        /// <summary>
        /// Opens the polyline (sets IsClosed = false)
        /// </summary>
        public void Open()
        {
            if (!IsClosed)
                return;
            RemoveVertex(VertexCount - 1);
        }

        #endregion

        #region Internal Methods

        internal List<Guid> BuildOrderedVertices()
        {
            return children.Values
                .OfType<PolylineVertex>()
                .OrderBy(v => v.Index)
                .Select(v => v.ID)
                .ToList();
        }

        #endregion

        #region Private Methods

        private Polyline PolylineFromSegments(List<PolylineSegment> segments)
        {
            var polyline = new Polyline(_document);
            polyline.SetBasicPropertiesFrom(this);

            // Add all segment starts
            foreach (var seg in segments)
            {
                double bulge = seg.IsLine ? 0.0 : Math.Tan(seg.Sweep / 4);
                polyline.AddVertex(seg.Start, seg.Bulge);
            }

            // Add final vertex
            var lastSeg = segments[^1];
            double lastBulge = IsClosed ? lastSeg.Bulge : 0.0;

            polyline.AddVertex(lastSeg.End, lastBulge);

            // Preserve closure explicitly
            if (IsClosed)
                polyline.Close();

            return polyline;
        }

        private Polyline PolylineFromVerticiesAndBulges(List<Point3D> verticies, List<double> bulges)
        {
            if (verticies.Count < 2)
                return new Polyline(_document);

            if (bulges.Count != verticies.Count - 1)
                throw new ArgumentException("Bulge count must be vertexCount - 1");

            var segments = new List<PolylineSegment>();

            for (int i = 0; i < bulges.Count; i++)
            {
                var s = verticies[i];
                var e = verticies[i + 1];
                var bulge = bulges[i];

                segments.Add(CreateSegmentFromVertices(s, e, bulge));
            }

            return PolylineFromSegments(segments);
        }

        private PolylineSegment CreateSegmentFromVertices(Point3D previousVertex, Point3D currentVertex, double bulge)
        {
            if (Math.Abs(bulge) < 1e-10)
            {
                // Line segment
                return new PolylineSegment(previousVertex, currentVertex);
            }
            else
            {
                // Arc segment
                var center = GeometricCalculator.GetCenterFromBulge(previousVertex, currentVertex, bulge);
                double angle = GeometricCalculator.GetAngleFromBulge(bulge);
                return new PolylineSegment(previousVertex, currentVertex, center, angle);
            }
        }

        #endregion
    }
}