using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the behavior of AI-controlled entities.
    /// </summary>
    public class AISystem : ISystem
    {
        private GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly ChunkManager _chunkManager;
        private readonly WorldClockManager _worldClockManager;
        private readonly Random _random = new();

        public AISystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _chunkManager = ServiceLocator.Get<ChunkManager>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _worldClockManager.OnTimePassed += HandleTimePassed;
            EventBus.Subscribe<GameEvents.PlayerMoved>(HandlePlayerMoved);
        }

        private void HandleTimePassed(float secondsPassed, ActivityType activity)
        {
            _gameState ??= ServiceLocator.Get<GameState>();

            foreach (var entityId in _gameState.ActiveEntities)
            {
                var aiComp = _componentStore.GetComponent<AIComponent>(entityId);
                if (aiComp != null && _componentStore.HasComponent<NPCTagComponent>(entityId))
                {
                    aiComp.ActionTimeBudget += secondsPassed;
                }
            }
        }

        private void HandlePlayerMoved(GameEvents.PlayerMoved e)
        {
            // This logic is now inert as it relies on local map concepts.
        }

        public void Update(GameTime gameTime)
        {
        }

        public void UpdateDecisions()
        {
        }
    }
}