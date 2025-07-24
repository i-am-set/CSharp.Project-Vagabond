using System;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// A system responsible for regenerating energy for entities over time.
    /// </summary>
    public class EnergySystem : ISystem
    {
        private readonly ComponentStore _componentStore;
        private readonly GameState _gameState;

        public EnergySystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _gameState = ServiceLocator.Get<GameState>();
            var worldClockManager = ServiceLocator.Get<WorldClockManager>();
            worldClockManager.OnTimePassed += HandleTimePassed;
        }

        private void HandleTimePassed(float secondsPassed, ActivityType activity)
        {
            // Don't regenerate energy during combat ticks
            if (activity == ActivityType.Combat) return;

            var entitiesToProcess = _componentStore.GetAllEntitiesWithComponent<EnergyRegenComponent>().ToList();

            foreach (var entityId in entitiesToProcess)
            {
                var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);
                var regenComp = _componentStore.GetComponent<EnergyRegenComponent>(entityId);

                if (statsComp == null || statsComp.SecondsPerEnergyPoint <= 0)
                {
                    continue;
                }

                // Don't regenerate if already at max energy
                if (statsComp.CurrentEnergyPoints >= statsComp.MaxEnergyPoints)
                {
                    regenComp.RegenerationProgress = 0; // Reset progress if at full
                    continue;
                }

                float timeMultiplier = 1.0f;
                if (activity == ActivityType.Jogging)
                {
                    timeMultiplier = 1.5f;
                }

                float effectiveSecondsPerPoint = statsComp.SecondsPerEnergyPoint * timeMultiplier;
                if (effectiveSecondsPerPoint <= 0) continue;

                float progressThisTick = secondsPassed / effectiveSecondsPerPoint;
                regenComp.RegenerationProgress += progressThisTick;

                if (regenComp.RegenerationProgress >= 1.0f)
                {
                    int pointsToRestore = (int)Math.Floor(regenComp.RegenerationProgress);
                    statsComp.RestoreEnergy(pointsToRestore);
                    regenComp.RegenerationProgress -= pointsToRestore;
                }
            }
        }

        public void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            // This system is entirely event-driven by OnTimePassed.
        }
    }
}