using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD
{
    public enum PropertyType
    {
        Boolean,
        Integer,
        Double,
        String,
        Color,
        Point,
        PointStart,
        PointEnd,
        PointRad,
        Vector,
        Curve,
        Surface,
        Solid,
        Material,
        Texture,
        Layer
    }

    public class Property
    {
        public PropertyType Type { get; set; }
        public object Value { get; set; }
        public Property(PropertyType type, object value)
        {
            Type = type;
            Value = value;
        }
    }
}
