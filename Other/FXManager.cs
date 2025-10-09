#nullable enable
using Microsoft.Xna.Framework;
using ProjectVagabond.Particles;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A static manager for triggering "fire-and-forget" particle effects.
    /// Effects are defined in ParticleEffects and retrieved by name.
    /// </summary>
    public static class FXManager
    {
        private static ParticleSystemManager? _particleSystemManager;

        /// <summary>
        /// Plays a pre-defined particle effect at a given position.
        /// The emitter(s) for this effect will be created, run, and automatically destroyed.
        /// </summary>
        /// <param name="effectName">The name of the effect to play, corresponding to a method in ParticleEffects.</param>
        /// <param name="position">The world-space position to spawn the effect.</param>
        public static void Play(string effectName, Vector2 position)
        {
            _particleSystemManager ??= ServiceLocator.Get<ParticleSystemManager>();

            Debug.WriteLine($"[FXManager] Play called for effect '{effectName}' at position {position}.");

            List<ParticleEmitterSettings>? settingsList = ParticleEffectRegistry.CreateEffect(effectName);
            if (settingsList == null)
            {
                Debug.WriteLine($"[FXManager] ERROR: Particle effect '{effectName}' not found in registry.");
                return;
            }

            Debug.WriteLine($"[FXManager] Found {settingsList.Count} emitter(s) for effect '{effectName}'.");

            foreach (var settings in settingsList)
            {
                var emitter = _particleSystemManager.CreateEmitter(settings);
                emitter.Position = position;

                // If the effect is a burst, trigger it immediately upon creation.
                if (settings.BurstCount > 0)
                {
                    emitter.EmitBurst(settings.BurstCount);
                }
            }
        }
    }
}
#nullable restore