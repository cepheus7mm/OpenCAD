using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenCAD.Geometry
{
    public class Line : GeometryBase, ILinearGeometry
    {
        private readonly object _propertyLock = new();

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
    }
}
