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
        private readonly Random _random = new();

        public AISystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _chunkManager = ServiceLocator.Get<ChunkManager>();
            EventBus.Subscribe<GameEvents.PlayerMoved>(HandlePlayerMoved);
            EventBus.Subscribe<GameEvents.PlayerActionExecuted>(HandlePlayerActionExecuted);
        }

        private void HandlePlayerActionExecuted(GameEvents.PlayerActionExecuted e)
        {
            // This is the new entry point for AI to take their turn.
            // For each player action, every active AI gets a chance to act.
            _gameState ??= ServiceLocator.Get<GameState>();

            foreach (var entityId in _gameState.ActiveEntities)
            {
                if (_componentStore.HasComponent<AIComponent>(entityId))
                {
                    // TODO: Implement AI turn logic here.
                    // For example, grant the AI one "action point" to spend.
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