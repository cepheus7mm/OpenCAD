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
    public class Polyline : GeometryBase
    {
        /// <summary>
        /// Parameterless constructor required for deserialization
        /// </summary>
        public Polyline() : base()
        {
        }

        public Polyline(OpenCADDocument doc) : base(doc)
        {
        }

        public Polyline(OpenCADDocument doc, IEnumerable<Point3D> vertices, bool closed = false) : base(doc)
        {
            foreach (var vertex in vertices)
            {
                AddVertex(vertex);
            }
            IsClosed = closed;
        }

        public Polyline(OpenCADDocument doc, IEnumerable<(Point3D position, double bulge)> vertices, bool closed = false) : base(doc)
        {
            foreach (var (position, bulge) in vertices)
            {
                AddVertex(position, bulge);
            }
            IsClosed = closed;
        }

        /// <summary>
        /// Whether the polyline is closed (last vertex connects to first)
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsClosed
        {
            get => GetPropertyValue<bool>(PropertyType.Boolean, nameof(IsClosed));
            set => SetPropertyValue(PropertyType.Boolean, nameof(IsClosed), "Closed", value);
        }

        /// <summary>
        /// Number of vertices in the polyline
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public int VertexCount => children.Count;

        private uint _nextIndex = 0;

        #region Vertex Management

        /// <summary>
        /// Adds a vertex to the end of the polyline
        /// </summary>
        public PolylineVertex AddVertex(Point3D position, double bulge = 0.0)
        {
            var vertex = new PolylineVertex(_document, position, bulge)
            {
                Index = _nextIndex++
            };
            Add(vertex);
            return vertex;
        }

        public void AddVertex(PolylineVertex vertex)
        {
            vertex.Index = _nextIndex++;
            Add(vertex);
        }

        /// <summary>
        /// Inserts a vertex at the specified index
        /// </summary>
        public void InsertVertex(int index, Point3D position, double bulge = 0.0)
        {
            if (index < 0 || index > VertexCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            var vertices = GetOrderedVertices().ToList();
            var newVertex = new PolylineVertex(_document, position, bulge);

            // Re-Index: build new list with insertion and assign Index deterministically
            vertices.Insert(index, newVertex);

            // Remove all existing vertices
            foreach (var v in GetOrderedVertices().ToList())
                Remove(v);

            // Re-add and assign Indexs in order
            _nextIndex = 0;
            foreach (var v in vertices)
            {
                v.Index = _nextIndex++;
                Add(v);
            }
        }

        /// <summary>
        /// Removes the vertex at the specified index
        /// </summary>
        public bool RemoveVertex(int index)
        {
            var vertices = GetOrderedVertices().ToList();
            if (index < 0 || index >= vertices.Count)
                return false;

            return Remove(vertices[index]);
        }

        /// <summary>
        /// Removes a vertex at the specified position (with tolerance)
        /// </summary>
        public bool RemoveVertexAt(Point3D position, double tolerance = 1e-6)
        {
            var vertex = GetOrderedVertices()
                .FirstOrDefault(v => v.Position.DistanceTo(position) < tolerance);

            return vertex != null && Remove(vertex);
        }

        /// <summary>
        /// Clears all vertices
        /// </summary>
        public void ClearVertices()
        {
            var vertices = GetOrderedVertices().ToList();
            foreach (var vertex in vertices)
            {
                Remove(vertex);
            }
        }

        /// <summary>
        /// Gets all vertices in order (ordered by their addition Index, which is preserved in the children dictionary)
        /// </summary>
        public IReadOnlyList<PolylineVertex> GetVertices()
        {
            return GetOrderedVertices().ToList();
        }

        /// <summary>
        /// Gets the vertex at the specified index
        /// </summary>
        public PolylineVertex? GetVertex(int index)
        {
            var vertices = GetOrderedVertices().ToList();
            if (index < 0 || index >= vertices.Count)
                return null;

            return vertices[index];
        }

        /// <summary>
        /// Helper to get vertices in deterministic order
        /// </summary>
        private IEnumerable<PolylineVertex> GetOrderedVertices()
        {
            // Sort by Index to guarantee order
            return children.Values.OfType<PolylineVertex>().OrderBy(v => v.Index);
        }

        #endregion

        #region Segment Queries

        /// <summary>
        /// Gets the number of segments (edges) in the polyline
        /// </summary>
        public int GetSegmentCount()
        {
            int count = VertexCount;
            if (count < 2)
                return 0;

            return IsClosed ? count : count - 1;
        }

        /// <summary>
        /// Gets segment information (start point, end point, bulge)
        /// </summary>
        public (Point3D start, Point3D end, double bulge) GetSegment(int index)
        {
            var vertices = GetOrderedVertices().ToList();
            int segmentCount = GetSegmentCount();

            if (index < 0 || index >= segmentCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            var startVertex = vertices[index];
            var endVertex = index == vertices.Count - 1 ? vertices[0] : vertices[index + 1];

            return (startVertex.Position, endVertex.Position, startVertex.Bulge);
        }

        /// <summary>
        /// Returns true if the segment at the specified index is an arc
        /// </summary>
        public bool IsSegmentArc(int index)
        {
            var segment = GetSegment(index);
            return Math.Abs(segment.bulge) > 1e-10;
        }

        /// <summary>
        /// Returns true if the segment at the specified index is a line
        /// </summary>
        public bool IsSegmentLine(int index)
        {
            return !IsSegmentArc(index);
        }

        /// <summary>
        /// Gets the length of a specific segment
        /// </summary>
        public double GetSegmentLength(int index)
        {
            var (start, end, bulge) = GetSegment(index);

            if (Math.Abs(bulge) < 1e-10)
            {
                // Line segment
                return start.DistanceTo(end);
            }
            else
            {
                // Arc segment
                double chordLength = start.DistanceTo(end);
                double angle = GeometricCalculator.GetAngleFromBulge(bulge);
                double radius = GeometricCalculator.GetRadiusFromBulge(start, end, bulge);

                return Math.Abs(radius * angle);
            }
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
            return true;
        }

        /// <summary>
        /// Reverses the direction of the polyline
        /// </summary>
        public void Reverse()
        {
            var vertices = GetOrderedVertices().ToList();
            vertices.Reverse();

            // Remove all vertices
            foreach (var v in GetOrderedVertices().ToList())
                Remove(v);

            // Re-add in reversed order and fix bulges and Index
            _nextIndex = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];

                int prevIndex = (i - 1 + vertices.Count) % vertices.Count;
                if (i == 0 && !IsClosed)
                    v.Bulge = 0;
                else
                    v.Bulge = -vertices[prevIndex].Bulge;

                v.Index = _nextIndex++;
                Add(v);
            }
        }

        /// <summary>
        /// Closes the polyline (sets IsClosed = true)
        /// </summary>
        public void Close()
        {
            IsClosed = true;
        }

        /// <summary>
        /// Opens the polyline (sets IsClosed = false)
        /// </summary>
        public void Open()
        {
            IsClosed = false;
        }

        #endregion

        #region GeometryBase Overrides

        /// <summary>
        /// Total length of all segments
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public override double Length
        {
            get
            {
                double totalLength = 0;
                int segmentCount = GetSegmentCount();

                for (int i = 0; i < segmentCount; i++)
                {
                    totalLength += GetSegmentLength(i);
                }

                return totalLength;
            }
        }

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
            var vertices = GetOrderedVertices().ToList();
            if (vertices.Count == 0)
            {
                return new Extents
                {
                    Min = Point3D.Origin,
                    Max = Point3D.Origin
                };
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double minZ = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            double maxZ = double.MinValue;

            foreach (var vertex in vertices)
            {
                minX = Math.Min(minX, vertex.Position.X);
                minY = Math.Min(minY, vertex.Position.Y);
                minZ = Math.Min(minZ, vertex.Position.Z);
                maxX = Math.Max(maxX, vertex.Position.X);
                maxY = Math.Max(maxY, vertex.Position.Y);
                maxZ = Math.Max(maxZ, vertex.Position.Z);

                // TODO: For arc segments, we should also check the arc extents
                // This is a simplified implementation that only checks vertices
            }

            return new Extents
            {
                Min = new Point3D(minX, minY, minZ),
                Max = new Point3D(maxX, maxY, maxZ)
            };
        }

        public override bool Transform(Matrix4D transformation)
        {
            var vertices = GetOrderedVertices().ToList();

            foreach (var vertex in vertices)
            {
                var pos = vertex.Position;
                var transformed = Vector3D.Transform(new Vector3D(pos.X, pos.Y, pos.Z), transformation);
                vertex.Position = new Point3D(transformed.X, transformed.Y, transformed.Z);
            }

            return true;
        }

        public override IEnumerable<GeoPoint> GetGeoPoints(Point3D referencePoint, GeoPointModes geoPointType)
        {
            var candidates = new List<GeoPoint>();

            if (IsPreviewGeometry)
                return candidates;

            // Add vertices as GeoPoints
            if (geoPointType.HasFlag(GeoPointModes.Vertex))
            {
                foreach (var vertex in GetOrderedVertices())
                {
                    candidates.Add(new GeoPoint(vertex.Position, GeoPointModes.Vertex) { RelatedGeometryId = ID });
                }
            }

            // Add midpoints of segments
            if (geoPointType.HasFlag(GeoPointModes.Middle))
            {
                int segmentCount = GetSegmentCount();
                for (int i = 0; i < segmentCount; i++)
                {
                    var (start, end, bulge) = GetSegment(i);
                    Point3D midpoint = GeometricCalculator.GetMidpointFromBulge(start, end, bulge);
                    candidates.Add(new GeoPoint(midpoint, GeoPointModes.Middle) { RelatedGeometryId = ID });
                }
            }

            // TODO: Implement NearestPoint, Perpendicular, and other GeoPoint modes
            // This requires finding the closest point on any segment, which is more complex

            return candidates;
        }

        public override Point3D GetClosestPointTo(Point3D point, bool extend = false)
        {
            Point3D closestPoint = Point3D.Origin;
            double minDistance = double.MaxValue;

            int segmentCount = GetSegmentCount();
            for (int i = 0; i < segmentCount; i++)
            {
                var (start, end, bulge) = GetSegment(i);

                Point3D segmentClosest;
                if (Math.Abs(bulge) < 1e-10)
                {
                    // Line segment - use line closest point logic
                    var lineVec = end - start;
                    var pointVec = point - start;
                    double lineLenSq = lineVec.LengthSquared;

                    if (lineLenSq < double.Epsilon)
                    {
                        segmentClosest = start;
                    }
                    else
                    {
                        double t = Vector3D.Dot(pointVec, lineVec) / lineLenSq;
                        if (!extend)
                            t = Math.Max(0, Math.Min(1, t));

                        segmentClosest = start + lineVec * t;
                    }
                }
                else
                {
                    // Arc segment - simplified: just check start, end, and midpoint
                    // TODO: Implement proper arc closest point calculation
                    var midpoint = GeometricCalculator.GetMidpointFromBulge(start, end, bulge);

                    double distStart = start.DistanceTo(point);
                    double distEnd = end.DistanceTo(point);
                    double distMid = midpoint.DistanceTo(point);

                    if (distStart < distEnd && distStart < distMid)
                        segmentClosest = start;
                    else if (distEnd < distMid)
                        segmentClosest = end;
                    else
                        segmentClosest = midpoint;
                }

                double distance = segmentClosest.DistanceTo(point);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = segmentClosest;
                }
            }

            return closestPoint;
        }

        public override Vector3D? GetFirstDerivate(Point3D point)
        {
            // Find the segment containing the point and return its tangent
            // This is simplified - proper implementation would interpolate along the segment
            int segmentCount = GetSegmentCount();

            for (int i = 0; i < segmentCount; i++)
            {
                var (start, end, bulge) = GetSegment(i);

                if (Math.Abs(bulge) < 1e-10)
                {
                    // Line segment - constant derivative
                    var dir = end - start;
                    double len = dir.Length;
                    if (len > double.Epsilon)
                        return new Vector3D(dir.X / len, dir.Y / len, dir.Z / len);
                }
            }

            return null;
        }

        public override Vector3D? GetSecondDerivate(Point3D point)
        {
            // Second derivative for polyline segments
            // For lines: zero, For arcs: perpendicular to first derivative
            var firstDeriv = GetFirstDerivate(point);
            if (firstDeriv != null)
            {
                return firstDeriv.Rotate(Math.PI / 2, _normal);
            }

            return null;
        }

        public override double GetParameterAtPoint(Point3D point)
        {
            throw new NotImplementedException();
        }

        public override Point3D GetPointAtParameter(double parameter)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}