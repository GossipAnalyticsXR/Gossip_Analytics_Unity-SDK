using System;
using System.Collections.Generic;
using System.Reflection;

namespace GossipSDK.Core.Utilities
{
    /// <summary>
    /// Assembly-agnostic type resolver.
    /// Searches all loaded assemblies so OVR/Meta types are found regardless
    /// of whether they live in Assembly-CSharp, Oculus.VR, OculusIntegration, etc.
    /// Results are cached so repeated calls are cheap.
    /// </summary>
    public static class ReflectionUtil
    {
        private static readonly Dictionary<string, Type> _cache =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>
        /// Finds a type by its simple (unqualified) name across all loaded assemblies.
        /// Returns null if not found.
        /// </summary>
        public static Type FindType(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return null;

            if (_cache.TryGetValue(simpleName, out Type cached))
                return cached;

            // Fast path: fully-qualified name already resolvable
            Type t = Type.GetType(simpleName);
            if (t != null)
            {
                _cache[simpleName] = t;
                return t;
            }

            // Slow path: scan all loaded assemblies
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Try the direct lookup first (cheaper than GetTypes())
                try
                {
                    t = asm.GetType(simpleName);
                    if (t != null)
                    {
                        _cache[simpleName] = t;
                        return t;
                    }
                }
                catch { /* skip inaccessible assemblies */ }

                // Full scan — catches types in nested namespaces
                try
                {
                    foreach (Type candidate in asm.GetTypes())
                    {
                        if (candidate.Name == simpleName)
                        {
                            _cache[simpleName] = candidate;
                            return candidate;
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Some types failed to load; check what we did get
                    foreach (Type candidate in ex.Types)
                    {
                        if (candidate != null && candidate.Name == simpleName)
                        {
                            _cache[simpleName] = candidate;
                            return candidate;
                        }
                    }
                }
                catch { /* skip assemblies that cannot be reflected */ }
            }

            // Cache negative result to avoid rescanning every frame
            _cache[simpleName] = null;
            return null;
        }
    }
}
