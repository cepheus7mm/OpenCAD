using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Grips.GripProviders
{
    public sealed class GripProviderFactory : IGripProviderFactory
    {
        private readonly Dictionary<Type, IGripProvider> _providers = new();

        public GripProviderFactory()
        {
            AutoRegisterProviders();
        }

        private void AutoRegisterProviders()
        {
            var providerTypes =
                from asm in AppDomain.CurrentDomain.GetAssemblies()
                from type in asm.GetTypes()
                let attr = type.GetCustomAttribute<GripProviderAttribute>()
                where attr != null
                where typeof(IGripProvider).IsAssignableFrom(type)
                where !type.IsAbstract
                select new { ProviderType = type, attr.TargetType };

            foreach (var entry in providerTypes)
            {
                var instance = (IGripProvider)Activator.CreateInstance(entry.ProviderType)!;
                _providers[entry.TargetType] = instance;
                // Debug output
                System.Diagnostics.Debug.WriteLine(
                    $"[GripProviderFactory] Registered provider {entry.ProviderType.Name} for {entry.TargetType.Name}");
            }
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
