using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips.GripProviders
{
    public sealed class GripProviderFactory : IGripProviderFactory
    {
        private readonly Dictionary<Type, IGripProvider> _providers = new();

        public void Register<T>(IGripProvider provider) where T : OpenCADObject
        {
            _providers[typeof(T)] = provider;
        }

        public IGripProvider? GetProvider(OpenCADObject entity)
        {
            var type = entity.GetType();

            // Exact match
            if (_providers.TryGetValue(type, out var provider))
                return provider;

            // Optional: support inheritance (e.g., Polyline : Curve)
            foreach (var kvp in _providers)
            {
                if (kvp.Key.IsAssignableFrom(type))
                    return kvp.Value;
            }

            return null;
        }
    }
}
