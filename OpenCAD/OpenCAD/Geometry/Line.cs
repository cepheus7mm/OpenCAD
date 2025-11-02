using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry
{
    public class Line : GeometryBase
    {
        // Define point indices for the Point property type
        private const int START_POINT_INDEX = 0;
        private const int END_POINT_INDEX = 1;

        public Line(Point3D start, Point3D end)
        {
            // Store both points in a single Point property with named values
            properties.TryAdd((int)PropertyType.Point, new Property(PropertyType.Point, 
                ("Start Point", start),
                ("End Point", end)
            ));
            _isDrawable = true;
        }

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
                        ("Start Point", value),
                        ("End Point", new Point3D(0, 0, 0))
                    ));
                }
            }
        }

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
                        ("Start Point", new Point3D(0, 0, 0)),
                        ("End Point", value)
                    ));
                }
            }
        }

        public double Length => Start.DistanceTo(End);
    }
}
