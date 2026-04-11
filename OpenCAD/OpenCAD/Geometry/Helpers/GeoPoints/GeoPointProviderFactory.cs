using OpenCAD.Geometry.Helpers.GeoPoints.GeoPointProviders;
using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints
{
    public sealed class GeoPointProviderFactory : IGeoPointProviderFactory
    {
        private readonly Dictionary<Type, IGeoPointProvider> _providers =
            new Dictionary<Type, IGeoPointProvider>();

        public GeoPointProviderFactory()
        {
            AutoRegisterProviders();
        }

        private void AutoRegisterProviders()
        {
            var providerTypes =
                from asm in AppDomain.CurrentDomain.GetAssemblies()
                from type in asm.GetTypes()
                let attr = type.GetCustomAttribute<GeoPointProviderAttribute>()
                where attr != null
                where typeof(IGeoPointProvider).IsAssignableFrom(type)
                where !type.IsAbstract
                select new { ProviderType = type, attr.TargetType };

            foreach (var entry in providerTypes)
            {
                var instance = (IGeoPointProvider)Activator.CreateInstance(entry.ProviderType)!;
                _providers[entry.TargetType] = instance;

                // Debug trace
                System.Diagnostics.Debug.WriteLine(
                    $"[GeoPointProviderFactory] Registered provider {entry.ProviderType.Name} for {entry.TargetType.Name}");
            }
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
