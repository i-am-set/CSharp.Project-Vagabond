﻿using Microsoft.Xna.Framework;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the fine-grained, tick-based passage of time and action execution
    /// on the local map when the player's action queue is being processed.
    /// This ensures all entities act concurrently based on their relative speeds.
    /// </summary>
    public class LocalMapTurnSystem : ISystem
    {
        private readonly GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly WorldClockManager _worldClockManager;
        private AISystem _aiSystem; // Lazy loaded

        private const float BASE_STEP_DURATION = 0.15f;

        public LocalMapTurnSystem()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
        }

        public void Update(GameTime gameTime)
        {
            if (!_gameState.IsExecutingActions || _gameState.PathExecutionMapView != MapView.Local || _gameState.IsInCombat)
            {
                return;
            }

            // If any entity is currently animating its movement, wait for it to finish.
            if (_gameState.ActiveEntities.Any(id => _componentStore.HasComponent<InterpolationComponent>(id)))
            {
                return;
            }

            // If the player's queue is empty, we are done.
            var playerActionQueue = _componentStore.GetComponent<ActionQueueComponent>(_gameState.PlayerEntityId);
            if (playerActionQueue == null || !playerActionQueue.ActionQueue.Any())
            {
                _gameState.ToggleExecutingActions(false);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Local movement complete." });
                return;
            }

            _aiSystem ??= ServiceLocator.Get<AISystem>();
            _aiSystem.UpdateAIBehavior();

            // The time tick is now determined solely by the player's next action.
            var playerStats = _componentStore.GetComponent<StatsComponent>(_gameState.PlayerEntityId);
            if (playerStats == null)
            {
                _gameState.ToggleExecutingActions(false); // Failsafe
                return;
            }

            bool isPlayerRunning = false;
            if (playerActionQueue.ActionQueue.Peek() is MoveAction move)
            {
                isPlayerRunning = move.IsRunning;
            }
            float playerSpeed = isPlayerRunning ? playerStats.LocalMapSpeed * 3 : playerStats.LocalMapSpeed;
            float timeTick = 1.0f / playerSpeed; // Time it takes the player to make one move.

            _worldClockManager.PassTime(timeTick);

            // Now, update progress and execute moves for ALL entities based on that fixed time tick.
            foreach (var entityId in _gameState.ActiveEntities)
            {
                var progressComp = _componentStore.GetComponent<MovementProgressComponent>(entityId);
                var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);

                if (progressComp == null)
                {
                    progressComp = new MovementProgressComponent();
                    _componentStore.AddComponent(entityId, progressComp);
                }
                if (statsComp == null) continue;

                if (entityId == _gameState.PlayerEntityId)
                {
                    // The player always moves exactly one step per tick they initiate.
                    progressComp.Progress = 1.0f;
                }
                else // It's an AI
                {
                    var intent = _componentStore.GetComponent<AIIntentComponent>(entityId);
                    bool isRunning = (intent?.CurrentIntent == AIIntent.Pursuing || intent?.CurrentIntent == AIIntent.Fleeing);
                    if (isRunning && !statsComp.CanExertEnergy(1))
                    {
                        isRunning = false;
                    }
                    float currentSpeed = isRunning ? statsComp.LocalMapSpeed * 3 : statsComp.LocalMapSpeed;
                    progressComp.Progress += timeTick * currentSpeed;
                }

                // Execute all full moves the entity has accumulated.
                while (progressComp.Progress >= 1.0f)
                {
                    progressComp.Progress -= 1.0f;
                    ExecuteNextMove(entityId);
                }
            }
        }

        private void ExecuteNextMove(int entityId)
        {
            Vector2? nextStep = null;
            bool isRunning = false;

            if (entityId == _gameState.PlayerEntityId)
            {
                var playerActionQueue = _componentStore.GetComponent<ActionQueueComponent>(entityId);
                if (playerActionQueue.ActionQueue.TryDequeue(out IAction action))
                {
                    if (action is MoveAction move)
                    {
                        nextStep = move.Destination;
                        isRunning = move.IsRunning;
                    }
                }
            }
            else // It's an AI
            {
                _aiSystem ??= ServiceLocator.Get<AISystem>();
                nextStep = _aiSystem.GetNextStepForExecution(entityId);
                var intent = _componentStore.GetComponent<AIIntentComponent>(entityId);
                isRunning = (intent?.CurrentIntent == AIIntent.Pursuing || intent?.CurrentIntent == AIIntent.Fleeing);
            }

            if (nextStep.HasValue)
            {
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                var statsComp = _componentStore.GetComponent<StatsComponent>(entityId);
                if (localPosComp != null && statsComp != null)
                {
                    if (isRunning && statsComp.CanExertEnergy(1))
                    {
                        statsComp.ExertEnergy(1);
                    }
                    else
                    {
                        isRunning = false; // Ensure we don't run without energy
                    }

                    float currentSpeed = isRunning ? statsComp.RunSpeed : statsComp.WalkSpeed;
                    float visualDuration = (BASE_STEP_DURATION / currentSpeed) / _worldClockManager.TimeScale;

                    var interp = new InterpolationComponent(localPosComp.LocalPosition, nextStep.Value, visualDuration, isRunning);
                    _componentStore.AddComponent(entityId, interp);
                }
            }
        }
    }
}
