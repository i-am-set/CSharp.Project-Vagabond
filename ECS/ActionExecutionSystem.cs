using Microsoft.Xna.Framework;
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
        private bool _localRunCostApplied = false;
        private float _currentActionDuration = 0f;

        public ActionExecutionSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
            _chunkManager = ServiceLocator.Get<ChunkManager>();
        }

        public void StartExecution()
        {
            _isFirstPlayerAction = true;
            _localRunCostApplied = false;
        }

        public void StopExecution()
        {
            _isFirstPlayerAction = true;
            _localRunCostApplied = false;
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
                float progress = _worldClockManager.GetInterpolationProgress();
                if (progress > 0.01f) // Only apply if some progress was made
                {
                    var posComp = _componentStore.GetComponent<PositionComponent>(playerEntityId);
                    var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(playerEntityId);
                    var statsComp = _componentStore.GetComponent<StatsComponent>(playerEntityId);

                    Vector2 moveDir = moveAction.Destination - posComp.WorldPosition;

                    Vector2 startLocalPos = new Vector2(32, 32);
                    Vector2 targetLocalPos = startLocalPos;
                    if (moveDir.X > 0) targetLocalPos.X = 63; else if (moveDir.X < 0) targetLocalPos.X = 0;
                    if (moveDir.Y > 0) targetLocalPos.Y = 63; else if (moveDir.Y < 0) targetLocalPos.Y = 0;

                    Vector2 newLocalPos = Vector2.Lerp(startLocalPos, targetLocalPos, progress);
                    localPosComp.LocalPosition = new Vector2(
                        MathHelper.Clamp((int)newLocalPos.X, 0, Global.LOCAL_GRID_SIZE - 1),
                        MathHelper.Clamp((int)newLocalPos.Y, 0, Global.LOCAL_GRID_SIZE - 1)
                    );

                    int fullEnergyCost = _gameState.GetMovementEnergyCost(moveAction, false);
                    int energyToExert = (int)Math.Ceiling(fullEnergyCost * progress);
                    statsComp.ExertEnergy(energyToExert);
                }
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

            if (_gameState.IsPaused || _gameState.IsInCombat || (_gameState.IsExecutingActions && _gameState.PathExecutionMapView == MapView.Local))
            {
                return;
            }

            int playerEntityId = _gameState.PlayerEntityId;
            var playerActionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(playerEntityId);

            if (playerActionQueueComp != null && playerActionQueueComp.ActionQueue.Count > 0 && _gameState.IsExecutingActions)
            {
                bool hasAction = _componentStore.HasComponent<MoveAction>(playerEntityId) ||
                                 _componentStore.HasComponent<RestAction>(playerEntityId);

                if (!hasAction)
                {
                    IAction nextAction = playerActionQueueComp.ActionQueue.Peek();
                    if (PreActionChecks(_gameState, playerEntityId, nextAction))
                    {
                        float secondsToPass = CalculateSecondsForAction(_gameState, nextAction);
                        _currentActionDuration = secondsToPass; // Store the calculated duration
                        playerActionQueueComp.ActionQueue.Dequeue();
                        if (nextAction is MoveAction ma) _componentStore.AddComponent(playerEntityId, ma);
                        else if (nextAction is RestAction ra) _componentStore.AddComponent(playerEntityId, ra);
                        _worldClockManager.PassTime(secondsToPass, 0.5f);
                    }
                }
            }

            if (_worldClockManager.IsInterpolatingTime) return;

            var playerMoveAction = _componentStore.GetComponent<MoveAction>(playerEntityId);
            if (playerMoveAction != null && !playerMoveAction.IsComplete)
            {
                ApplyMoveActionEffects(_gameState, playerEntityId, playerMoveAction);
                playerMoveAction.IsComplete = true;
                _isFirstPlayerAction = false;
            }

            var playerRestAction = _componentStore.GetComponent<RestAction>(playerEntityId);
            if (playerRestAction != null && !playerRestAction.IsComplete)
            {
                ApplyRestActionEffects(_gameState, playerEntityId, playerRestAction);
                playerRestAction.IsComplete = true;
                _isFirstPlayerAction = false;
            }

            if (playerMoveAction?.IsComplete == true) _componentStore.RemoveComponent<MoveAction>(playerEntityId);
            if (playerRestAction?.IsComplete == true) _componentStore.RemoveComponent<RestAction>(playerEntityId);

            if (_gameState.IsExecutingActions && playerActionQueueComp != null && playerActionQueueComp.ActionQueue.Count == 0 && !_componentStore.HasComponent<MoveAction>(playerEntityId) && !_componentStore.HasComponent<RestAction>(playerEntityId))
            {
                _gameState.ToggleExecutingActions(false);
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Action queue completed." });
            }
        }

        private bool PreActionChecks(GameState gameState, int entityId, IAction action)
        {
            if (action is MoveAction moveAction)
            {
                if (!gameState.IsPositionPassable(moveAction.Destination, gameState.PathExecutionMapView))
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Movement blocked at {moveAction.Destination}." });
                    gameState.CancelExecutingActions(true);
                    return false;
                }
                int energyCost = gameState.GetMovementEnergyCost(moveAction, false); // Local moves are ignored by this system
                if (!gameState.PlayerStats.CanExertEnergy(energyCost))
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Not enough energy to move! Need {energyCost}, have {gameState.PlayerStats.CurrentEnergyPoints}" });
                    gameState.CancelExecutingActions(true);
                    return false;
                }
            }
            return true;
        }

        private float CalculateSecondsForAction(GameState gameState, IAction action)
        {
            // This system now only calculates for world moves.
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
            _worldClockManager ??= ServiceLocator.Get<WorldClockManager>();

            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            if (localPosComp == null) return;

            // This method now ONLY handles world moves.
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

            var interp = new InterpolationComponent(localPosComp.LocalPosition, newLocalPos, _worldClockManager.InterpolationDurationRealSeconds);
            _componentStore.AddComponent(entityId, interp);

            string moveType = action.IsRunning ? "Ran" : "Walked";
            string timeString = _worldClockManager.GetPreciseFormattedTimeFromSeconds(_currentActionDuration);
            var mapData = gameState.GetMapDataAt((int)action.Destination.X, (int)action.Destination.Y);
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[khaki]{moveType} through[gold] {gameState.GetTerrainDescription(mapData).ToLower()}[khaki].[dim] ({timeString})" });
        }

        private void ApplyRestActionEffects(GameState gameState, int entityId, RestAction action)
        {
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            if (stats == null) return;

            stats.Rest(action.RestType);
            string restType = action.RestType.ToString().Replace("Rest", "");
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[rest]Completed {restType.ToLower()} rest. Energy is now {stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints}." });
        }
    }
}