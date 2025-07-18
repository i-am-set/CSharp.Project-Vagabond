using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the registration and updating of all game systems, allowing for
    /// different systems to run at different frequencies.
    /// </summary>
    public class SystemManager
    {
        private readonly List<SystemEntry> _systems = new List<SystemEntry>();

        /// <summary>
        /// Registers a system with the manager.
        /// </summary>
        /// <param name="system">The system instance to register.</param>
        /// <param name="updateIntervalSeconds">The desired interval between updates. 0 means every frame.</param>
        public void RegisterSystem(ISystem system, float updateIntervalSeconds = 0f)
        {
            if (system is InterpolationSystem || system is CombatInitiationSystem)
            {
                // These systems are now updated directly by Core.cs and should not be in this manager.
                return;
            }
            _systems.Add(new SystemEntry(system, updateIntervalSeconds));
        }

        /// <summary>
        /// Updates all registered systems according to their specified update intervals.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var entry in _systems)
            {
                // For systems that run every frame
                if (entry.UpdateInterval == 0f)
                {
                    entry.System.Update(gameTime);
                    continue;
                }

                // For systems that run on a timer
                entry.Accumulator += deltaTime;
                if (entry.Accumulator >= entry.UpdateInterval)
                {
                    entry.System.Update(gameTime);
                    entry.Accumulator -= entry.UpdateInterval;
                }
            }
        }
    }
}