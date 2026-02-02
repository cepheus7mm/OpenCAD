using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints
{
    public sealed class GeoPointProviderFactory : IGeoPointProviderFactory
    {
        private readonly Dictionary<Type, IGeoPointProvider> _providers =
            new Dictionary<Type, IGeoPointProvider>();

        public void Register<T>(IGeoPointProvider provider)
            where T : OpenCADObject
        {
            _providers[typeof(T)] = provider;
        }

        public IGeoPointProvider? GetProvider(OpenCADObject obj)
        {
            var type = obj.GetType();

            // Exact match
            if (_providers.TryGetValue(type, out var provider))
                return provider;

            // Inheritance fallback (e.g., Polyline → Entity)
            foreach (var kvp in _providers)
            {
                if (kvp.Key.IsAssignableFrom(type))
                    return kvp.Value;
            }

            return null;
        }
    }
}
