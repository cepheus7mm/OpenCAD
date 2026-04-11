using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips.GripProviders
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class GripProviderAttribute : Attribute
    {
        public Type TargetType { get; }

        public GripProviderAttribute(Type targetType)
        {
            TargetType = targetType;
        }
    }
}
