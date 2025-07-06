using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes the action queue of the PLAYER, executing one action at a time
    /// and applying its effects to the game state. This system drives the game clock.
    /// </summary>
    public class ActionExecutionSystem : ISystem
    {
        private bool _isFirstPlayerAction = true;
        private bool _localRunCostApplied = false;

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
            var gameState = Core.CurrentGameState;
            int playerEntityId = gameState.PlayerEntityId;

            var moveAction = Core.ComponentStore.GetComponent<MoveAction>(playerEntityId);
            if (moveAction != null && gameState.PathExecutionMapView == MapView.World)
            {
                float progress = Core.CurrentWorldClockManager.GetInterpolationProgress();
                if (progress > 0.01f) // Only apply if some progress was made
                {
                    // Player does NOT change world chunks on interruption.
                    // We only update their local position within the CURRENT chunk.
                    var posComp = Core.ComponentStore.GetComponent<PositionComponent>(playerEntityId);
                    var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(playerEntityId);
                    var statsComp = Core.ComponentStore.GetComponent<StatsComponent>(playerEntityId);

                    Vector2 moveDir = moveAction.Destination - posComp.WorldPosition;

                    // The player was at the center of the local map when the world move started.
                    // We calculate their new position based on how far they moved towards the edge.
                    Vector2 startLocalPos = new Vector2(32, 32);
                    Vector2 targetLocalPos = startLocalPos;
                    if (moveDir.X > 0) targetLocalPos.X = 63; else if (moveDir.X < 0) targetLocalPos.X = 0;
                    if (moveDir.Y > 0) targetLocalPos.Y = 63; else if (moveDir.Y < 0) targetLocalPos.Y = 0;

                    Vector2 newLocalPos = Vector2.Lerp(startLocalPos, targetLocalPos, progress);
                    localPosComp.LocalPosition = new Vector2(
                        MathHelper.Clamp((int)newLocalPos.X, 0, Global.LOCAL_GRID_SIZE - 1),
                        MathHelper.Clamp((int)newLocalPos.Y, 0, Global.LOCAL_GRID_SIZE - 1)
                    );

                    // Also apply partial energy cost
                    int fullEnergyCost = gameState.GetMovementEnergyCost(moveAction, false);
                    int energyToExert = (int)Math.Ceiling(fullEnergyCost * progress);
                    statsComp.ExertEnergy(energyToExert);
                }
            }
            // Clean up the action components
            Core.ComponentStore.RemoveComponent<MoveAction>(playerEntityId);
            Core.ComponentStore.RemoveComponent<RestAction>(playerEntityId);
        }

        /// <summary>
        /// Updates the action system, processing actions for the player.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            var gameState = Core.CurrentGameState;
            if (gameState.IsPaused) return;

            // This system now ONLY handles the player.
            int playerEntityId = gameState.PlayerEntityId;

            // --- Phase 1: INITIATION ---
            var actionQueueComp = Core.ComponentStore.GetComponent<ActionQueueComponent>(playerEntityId);
            if (actionQueueComp != null && actionQueueComp.ActionQueue.Count > 0 && gameState.IsExecutingActions)
            {
                bool hasAction = Core.ComponentStore.HasComponent<MoveAction>(playerEntityId) ||
                                 Core.ComponentStore.HasComponent<RestAction>(playerEntityId);

                if (!hasAction)
                {
                    IAction nextAction = actionQueueComp.ActionQueue.Peek();

                    // Pre-start checks for the player's action
                    if (nextAction is MoveAction moveAction)
                    {
                        if (!gameState.IsPositionPassable(moveAction.Destination, gameState.PathExecutionMapView))
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Movement blocked at {moveAction.Destination}.");
                            gameState.CancelExecutingActions(true);
                            return;
                        }
                        int energyCost = gameState.GetMovementEnergyCost(moveAction, gameState.PathExecutionMapView == MapView.Local);
                        if (gameState.PathExecutionMapView == MapView.Local && moveAction.IsRunning && !_localRunCostApplied)
                        {
                            energyCost = 1; // Initial cost to start running locally
                        }
                        if (!gameState.PlayerStats.CanExertEnergy(energyCost))
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Not enough energy to move! Need {energyCost}, have {gameState.PlayerStats.CurrentEnergyPoints}");
                            gameState.CancelExecutingActions(true);
                            return;
                        }
                    }

                    // If checks pass, calculate time, dequeue, add component, and pass time.
                    int secondsToPass = CalculateSecondsForAction(gameState, nextAction);
                    actionQueueComp.ActionQueue.Dequeue();
                    if (nextAction is MoveAction ma) Core.ComponentStore.AddComponent(playerEntityId, ma);
                    else if (nextAction is RestAction ra) Core.ComponentStore.AddComponent(playerEntityId, ra);

                    Core.CurrentWorldClockManager.PassTime(seconds: secondsToPass);
                }
            }

            // If time is passing, halt all further processing for this frame.
            if (Core.CurrentWorldClockManager.IsInterpolatingTime) return;

            // --- Phase 2: REAL PROCESSING ---
            // This phase applies the effects of the player's action that has just finished.
            var playerMoveAction = Core.ComponentStore.GetComponent<MoveAction>(playerEntityId);
            if (playerMoveAction != null && !playerMoveAction.IsComplete)
            {
                ApplyMoveActionEffects(gameState, playerEntityId, playerMoveAction);
                playerMoveAction.IsComplete = true;
                _isFirstPlayerAction = false;
            }

            var playerRestAction = Core.ComponentStore.GetComponent<RestAction>(playerEntityId);
            if (playerRestAction != null && !playerRestAction.IsComplete)
            {
                ApplyRestActionEffects(gameState, playerEntityId, playerRestAction);
                playerRestAction.IsComplete = true;
                _isFirstPlayerAction = false;
            }

            // --- Phase 3: CLEANUP ---
            // This phase removes the player's completed action component.
            if (playerMoveAction?.IsComplete == true)
            {
                Core.ComponentStore.RemoveComponent<MoveAction>(playerEntityId);
            }
            if (playerRestAction?.IsComplete == true)
            {
                Core.ComponentStore.RemoveComponent<RestAction>(playerEntityId);
            }

            // After all phases, check if the player's queue is now empty to end the execution state.
            if (gameState.IsExecutingActions)
            {
                bool playerHasAction = Core.ComponentStore.HasComponent<MoveAction>(playerEntityId) ||
                                       Core.ComponentStore.HasComponent<RestAction>(playerEntityId);

                if (actionQueueComp != null && actionQueueComp.ActionQueue.Count == 0 && !playerHasAction)
                {
                    gameState.ToggleExecutingActions(false);
                    Core.CurrentTerminalRenderer.AddOutputToHistory("Action queue completed.");
                }
            }
        }

        private int CalculateSecondsForAction(GameState gameState, IAction action)
        {
            bool isLocalMove = gameState.PathExecutionMapView == MapView.Local;

            if (action is MoveAction moveAction)
            {
                Vector2 previousPosition;
                MapData mapData;
                if (isLocalMove)
                {
                    previousPosition = gameState.PlayerLocalPos;
                    mapData = default;
                }
                else
                {
                    previousPosition = gameState.PlayerWorldPos;
                    mapData = gameState.GetMapDataAt((int)moveAction.Destination.X, (int)moveAction.Destination.Y);
                }
                Vector2 moveDirection = moveAction.Destination - previousPosition;
                int fullDuration = gameState.GetSecondsPassedDuringMovement(moveAction.IsRunning, mapData, moveDirection, isLocalMove);

                int finalDuration = fullDuration;
                // If this is the first world move, scale the time based on local position.
                if (!isLocalMove && _isFirstPlayerAction)
                {
                    float scaleFactor = gameState.GetFirstMoveTimeScaleFactor(moveDirection);
                    finalDuration = (int)Math.Ceiling(fullDuration * scaleFactor);
                }

                // A move must always take at least 1 second to ensure time passes.
                return Math.Max(1, finalDuration);
            }
            else if (action is RestAction restAction)
            {
                switch (restAction.RestType)
                {
                    case RestType.ShortRest:
                        return gameState.PlayerStats.ShortRestDuration * 60;
                    case RestType.LongRest:
                        return gameState.PlayerStats.LongRestDuration * 60;
                    case RestType.FullRest:
                        return gameState.PlayerStats.FullRestDuration * 60;
                }
            }

            return 0;
        }

        private void ApplyMoveActionEffects(GameState gameState, int entityId, MoveAction action)
        {
            var stats = Core.ComponentStore.GetComponent<StatsComponent>(entityId);
            if (stats == null) return;

            bool isLocalMove = gameState.PathExecutionMapView == MapView.Local;
            Vector2 nextPosition = action.Destination;

            string moveType = action.IsRunning ? "Ran" : "Walked";
            int secondsPassedForAction = CalculateSecondsForAction(gameState, action);
            string timeString = Core.CurrentWorldClockManager.GetCommaFormattedTimeFromSeconds(secondsPassedForAction);

            if (isLocalMove)
            {
                if (action.IsRunning && !_localRunCostApplied)
                {
                    stats.ExertEnergy(1);
                    _localRunCostApplied = true;
                }
                var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(entityId);
                localPosComp.LocalPosition = new Vector2(
                    MathHelper.Clamp(nextPosition.X, 0, Global.LOCAL_GRID_SIZE - 1),
                    MathHelper.Clamp(nextPosition.Y, 0, Global.LOCAL_GRID_SIZE - 1)
                );
            }
            else // World Move
            {
                int energyCost = gameState.GetMovementEnergyCost(action, false);
                stats.ExertEnergy(energyCost);

                var posComp = Core.ComponentStore.GetComponent<PositionComponent>(entityId);
                Vector2 oldWorldPos = posComp.WorldPosition;
                posComp.WorldPosition = nextPosition;

                Core.ChunkManager.UpdateEntityChunk(entityId, oldWorldPos, nextPosition);

                // On completing a world move, the player's local position is reset
                // to the entry point of the new local area.
                Vector2 moveDir = nextPosition - oldWorldPos;
                Vector2 newLocalPos = new Vector2(32, 32);
                if (moveDir.X > 0) newLocalPos.X = 0; else if (moveDir.X < 0) newLocalPos.X = 63;
                if (moveDir.Y > 0) newLocalPos.Y = 0; else if (moveDir.Y < 0) newLocalPos.Y = 63;
                if (moveDir.X != 0 && moveDir.Y == 0) newLocalPos.Y = 32;
                if (moveDir.Y != 0 && moveDir.X == 0) newLocalPos.X = 32;

                var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(entityId);
                localPosComp.LocalPosition = new Vector2(
                    MathHelper.Clamp(newLocalPos.X, 0, Global.LOCAL_GRID_SIZE - 1),
                    MathHelper.Clamp(newLocalPos.Y, 0, Global.LOCAL_GRID_SIZE - 1)
                );

                var mapData = gameState.GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                Core.CurrentTerminalRenderer.AddOutputToHistory($"[khaki]{moveType} through[gold] {mapData.TerrainType.ToLower()}[khaki].[dim] ({timeString})");
            }
        }

        private void ApplyRestActionEffects(GameState gameState, int entityId, RestAction action)
        {
            var stats = Core.ComponentStore.GetComponent<StatsComponent>(entityId);
            if (stats == null) return;

            stats.Rest(action.RestType);

            string restType = action.RestType.ToString().Replace("Rest", "");
            Core.CurrentTerminalRenderer.AddOutputToHistory($"[rest]Completed {restType.ToLower()} rest. Energy is now {stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints}.");
        }
    }
}