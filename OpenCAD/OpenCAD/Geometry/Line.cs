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
        // Define point indices for the Point property type
        private const int START_POINT_INDEX = 0;
        private const int END_POINT_INDEX = 1;

        /// <summary>
        /// Parameterless constructor required for deserialization.
        /// </summary>
        public Line() : base()
        {
        }

        public Line(OpenCADDocument doc, Point3D start, Point3D end) : base(doc)
        {
            // Store both points in a single Point property with named values
            properties.TryAdd((int)PropertyType.Point, new Property(PropertyType.Point, 
                (OpenCADStrings.StartPoint, start),
                (OpenCADStrings.EndPoint, end)
            ));
        }

        [JsonIgnore, XmlIgnore]
        public Point3D Start
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Point, out var prop))
                    return (Point3D)prop.GetValue(START_POINT_INDEX);
                throw new InvalidOperationException("Line start point not found.");
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.Point, out var prop))
                {
                    prop.SetValue(START_POINT_INDEX, value);
                }
                else
                {
                    // If property doesn't exist, create it with both named points
                    properties.TryAdd((int)PropertyType.Point, new Property(PropertyType.Point, 
                        (OpenCADStrings.StartPoint, value),
                        (OpenCADStrings.EndPoint, new Point3D(0, 0, 0))
                    ));
                }
            }
        }

        [JsonIgnore, XmlIgnore]
        public Point3D End
        {
            get
            {
                if (properties.TryGetValue((int)PropertyType.Point, out var prop))
                    return (Point3D)prop.GetValue(END_POINT_INDEX);
                throw new InvalidOperationException("Line end point not found.");
            }
            set
            {
                if (properties.TryGetValue((int)PropertyType.Point, out var prop))
                {
                    prop.SetValue(END_POINT_INDEX, value);
                }
                else
                {
                    // If property doesn't exist, create it with both named points
                    properties.TryAdd((int)PropertyType.Point, new Property(PropertyType.Point,
                        (OpenCADStrings.StartPoint, new Point3D(0, 0, 0)),
                        (OpenCADStrings.EndPoint, value)
                    ));
                }
            }
        }

        [JsonIgnore, XmlIgnore]
        public double Length => Start.DistanceTo(End);
    }
}
