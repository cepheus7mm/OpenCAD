using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints.GeoPointProviders
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class GeoPointProviderAttribute : Attribute
    {
        public Type TargetType { get; }

        public GeoPointProviderAttribute(Type targetType)
        {
            TargetType = targetType;
        }
    }
}
