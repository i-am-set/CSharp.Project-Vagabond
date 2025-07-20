﻿﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes the player's OUT-OF-COMBAT action queue, executing one action at a time
    /// and applying its effects to the game state. This system drives the game clock.
    /// </summary>
    public class ActionExecutionSystem : ISystem
    {
        private GameState _gameState;
        private readonly ComponentStore _componentStore;
        private WorldClockManager _worldClockManager;
        private readonly ChunkManager _chunkManager;

        private bool _isFirstPlayerAction = true;
        private const float VISUAL_INTERPOLATION_DURATION = 0.2f;

        public ActionExecutionSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _chunkManager = ServiceLocator.Get<ChunkManager>();
        }

        public void StartExecution()
        {
            _isFirstPlayerAction = true;
        }

        public void StopExecution()
        {
            _isFirstPlayerAction = true;
        }

        /// <summary>
        /// Called when an action queue is forcefully stopped mid-execution.
        /// It applies partial effects for the action that was in progress.
        /// </summary>
        public void HandleInterruption()
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

            int playerEntityId = _gameState.PlayerEntityId;

            var moveAction = _componentStore.GetComponent<MoveAction>(playerEntityId);
            if (moveAction != null && _gameState.PathExecutionMapView == MapView.World)
            {
                // Interruption logic for continuous time is complex and may not be needed.
                // For now, we just clear the action.
            }
            _componentStore.RemoveComponent<MoveAction>(playerEntityId);
            _componentStore.RemoveComponent<RestAction>(playerEntityId);
        }

        /// <summary>
        /// Updates the action system, processing actions for the player out of combat.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

            if (_gameState.IsPaused || _gameState.IsInCombat || !_gameState.IsExecutingActions || _worldClockManager.IsInterpolatingTime)
            {
                return;
            }

            // Handle World Map execution
            if (_gameState.PathExecutionMapView == MapView.World)
            {
                ProcessNextActionInQueue();
            }
        }

        private void ProcessNextActionInQueue()
        {
            int playerEntityId = _gameState.PlayerEntityId;
            var playerActionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(playerEntityId);

            if (playerActionQueueComp != null && playerActionQueueComp.ActionQueue.Count > 0)
            {
                // If an action is visually interpolating, wait for it to finish.
                if (_componentStore.HasComponent<InterpolationComponent>(playerEntityId))
                {
                    return;
                }

                IAction nextAction = playerActionQueueComp.ActionQueue.Dequeue();
                float actionCostInGameSeconds = CalculateSecondsForAction(_gameState, nextAction);

                // PassTime now calculates the real-world duration internally based on the current TimeScale.
                _worldClockManager.PassTime(actionCostInGameSeconds, 0);

                if (nextAction is MoveAction ma)
                {
                    ApplyMoveActionEffects(_gameState, playerEntityId, ma);
                }
                else if (nextAction is RestAction ra)
                {
                    ApplyRestActionEffects(_gameState, playerEntityId, ra);
                }
            }
            else if (_gameState.IsExecutingActions)
            {
                _gameState.ToggleExecutingActions(false);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Action queue completed." });
            }
        }

        private float CalculateSecondsForAction(GameState gameState, IAction action)
        {
            if (action is MoveAction moveAction)
            {
                var previousPosition = gameState.PlayerWorldPos;
                var mapData = gameState.GetMapDataAt((int)moveAction.Destination.X, (int)moveAction.Destination.Y);
                Vector2 moveDirection = moveAction.Destination - previousPosition;
                var playerStats = gameState.PlayerStats;
                if (playerStats == null) return 0;

                float fullDuration = gameState.GetSecondsPassedDuringMovement(playerStats, moveAction.IsRunning, mapData, moveDirection, false);

                float finalDuration = fullDuration;
                if (_isFirstPlayerAction)
                {
                    float scaleFactor = gameState.GetFirstMoveTimeScaleFactor(moveDirection);
                    finalDuration *= scaleFactor;
                }
                return finalDuration;
            }
            else if (action is RestAction restAction)
            {
                switch (restAction.RestType)
                {
                    case RestType.ShortRest: return gameState.PlayerStats.ShortRestDuration * 60;
                    case RestType.LongRest: return gameState.PlayerStats.LongRestDuration * 60;
                    case RestType.FullRest: return gameState.PlayerStats.FullRestDuration * 60;
                }
            }
            return 0;
        }

        private void ApplyMoveActionEffects(GameState gameState, int entityId, MoveAction action)
        {
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (localPosComp == null) return;

            int energyCost = gameState.GetMovementEnergyCost(action, false);
            stats?.ExertEnergy(energyCost);

            var posComp = _componentStore.GetComponent<PositionComponent>(entityId);
            Vector2 oldWorldPos = posComp.WorldPosition;
            posComp.WorldPosition = action.Destination;

            _chunkManager.UpdateEntityChunk(entityId, oldWorldPos, action.Destination);

            Vector2 moveDir = action.Destination - oldWorldPos;
            Vector2 newLocalPos = new Vector2(32, 32);
            if (moveDir.X > 0) newLocalPos.X = 0; else if (moveDir.X < 0) newLocalPos.X = 63;
            if (moveDir.Y > 0) newLocalPos.Y = 0; else if (moveDir.Y < 0) newLocalPos.Y = 63;
            if (moveDir.X != 0 && moveDir.Y == 0) newLocalPos.Y = 32;
            if (moveDir.Y != 0 && moveDir.X == 0) newLocalPos.X = 32;

            var interp = new InterpolationComponent(localPosComp.LocalPosition, newLocalPos, VISUAL_INTERPOLATION_DURATION, action.IsRunning);
            _componentStore.AddComponent(entityId, interp);

            _isFirstPlayerAction = false;
        }

        private void ApplyRestActionEffects(GameState gameState, int entityId, RestAction action)
        {
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            if (stats == null) return;

            stats.Rest(action.RestType);
            string restType = action.RestType.ToString().Replace("Rest", "");
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[rest]Completed {restType.ToLower()} rest. Energy is now {stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints}." });
            _isFirstPlayerAction = false;
        }
    }
}
