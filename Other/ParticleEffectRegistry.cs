using ProjectVagabond.Particles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A static helper class that uses reflection to discover and create particle effects
    /// defined in the ParticleEffects class. This decouples systems like the ActionAnimator
    /// from needing direct knowledge of specific particle effect implementations.
    /// </summary>
    public static class ParticleEffectRegistry
    {
        private static List<string> _effectNames;

        /// <summary>
        /// Gets a cached list of all public static methods in ParticleEffects that return
        /// either a ParticleEmitterSettings or a List<ParticleEmitterSettings>.
        /// </summary>
        /// <returns>An ordered list of available particle effect names.</returns>
        public static List<string> GetEffectNames()
        {
            if (_effectNames == null)
            {
                _effectNames = typeof(ParticleEffects)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.ReturnType == typeof(List<ParticleEmitterSettings>) || m.ReturnType == typeof(ParticleEmitterSettings))
                    .Select(m => m.Name)
                    .OrderBy(name => name)
                    .ToList();
            }
            return _effectNames;
        }

        /// <summary>
        /// Creates a list of particle emitter settings by invoking a method on the ParticleEffects class by name.
        /// </summary>
        /// <param name="name">The name of the public static method to invoke.</param>
        /// <returns>A list of settings objects, or null if the method is not found or returns an incompatible type.</returns>
        public static List<ParticleEmitterSettings> CreateEffect(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var method = typeof(ParticleEffects).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            if (method == null) return null;

            object result = method.Invoke(null, null);

            if (result is List<ParticleEmitterSettings> list)
            {
                return list;
            }
            if (result is ParticleEmitterSettings single)
            {
                return new List<ParticleEmitterSettings> { single };
            }
            return null;
        }
    }
}