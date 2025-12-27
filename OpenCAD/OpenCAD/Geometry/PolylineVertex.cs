using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    /// <summary>
    /// Represents a single vertex in a polyline with optional arc bulge
    /// </summary>
    public class PolylineVertex : OpenCADObject
    {
        /// <summary>
        /// Parameterless constructor required for deserialization
        /// </summary>
        public PolylineVertex() : base()
        {
        }

        public PolylineVertex(OpenCADDocument? doc, Point3D position, double bulge = 0.0) : base(doc)
        {
            Position = position;
            Bulge = bulge;
        }

        /// <summary>
        /// Position of this vertex in 3D space
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public Point3D Position
        {
            get => GetPropertyValue<Point3D>(PropertyType.Point, nameof(Position)) ?? Point3D.Origin;
            set => SetPropertyValue(PropertyType.Point, nameof(Position), OpenCADStrings.Position, value);
        }

        /// <summary>
        /// Bulge factor for arc segment TO the next vertex.
        /// 0 = line segment, positive = CCW arc, negative = CW arc.
        /// Bulge = tan(angle/4) where angle is the included angle of the arc.
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double Bulge
        {
            get => GetPropertyValue<double>(PropertyType.DoubleUnitLess, nameof(Bulge));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(Bulge), OpenCADStrings.Bulge, value);
        }

        /// <summary>
        /// Starting width of the segment (half of the total width at this vertex)
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double StartWidth
        {
            get => GetPropertyValue<double>(PropertyType.DoubleLength, nameof(StartWidth));
            set => SetPropertyValue(PropertyType.DoubleLength, nameof(StartWidth), OpenCADStrings.StartWidth, value);
        }

        /// <summary>
        /// Ending width of the segment (half of the total width at the next vertex)
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public double EndWidth
        {
            get => GetPropertyValue<double>(PropertyType.DoubleLength, nameof(EndWidth));
            set => SetPropertyValue(PropertyType.DoubleLength, nameof(EndWidth), OpenCADStrings.EndWidth, value);
        }

        [JsonIgnore, XmlIgnore]
        public uint Index
        {
            get => GetPropertyValue<uint>(PropertyType.DoubleUnitLess, nameof(Index));
            set => SetPropertyValue(PropertyType.DoubleUnitLess, nameof(Index), OpenCADStrings.Index, value);
        }

        /// <summary>
        /// Returns true if this vertex defines an arc segment to the next vertex
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsArcSegment => Math.Abs(Bulge) > 1e-10;

        /// <summary>
        /// Returns true if this vertex defines a line segment to the next vertex
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public bool IsLineSegment => Math.Abs(Bulge) <= 1e-10;
    }
}