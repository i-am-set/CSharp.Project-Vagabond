using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes the action queues of entities, executing one action at a time
    /// and applying its effects to the game state.
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
        /// Updates the action system, processing actions for all active entities
        /// in a three-phase cycle: Initiation, Processing, and Cleanup.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            var gameState = Core.CurrentGameState;
            if (gameState.IsPaused) return;

            // --- Phase 1: INITIATION ---
            // This phase starts new actions for any idle entities that have a queue.
            foreach (var entityId in gameState.ActiveEntities)
            {
                var actionQueueComp = Core.ComponentStore.GetComponent<ActionQueueComponent>(entityId);
                if (actionQueueComp != null && actionQueueComp.ActionQueue.Count > 0)
                {
                    bool hasAction = Core.ComponentStore.HasComponent<MoveAction>(entityId) ||
                                     Core.ComponentStore.HasComponent<RestAction>(entityId);

                    if (!hasAction)
                    {
                        // Only initiate player actions if the game is in execution mode.
                        if (entityId == gameState.PlayerEntityId && gameState.IsExecutingPath)
                        {
                            IAction nextAction = actionQueueComp.ActionQueue.Peek();

                            // Pre-start checks
                            if (nextAction is MoveAction moveAction)
                            {
                                if (!gameState.IsPositionPassable(moveAction.Destination, gameState.PathExecutionMapView))
                                {
                                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Movement blocked at {moveAction.Destination}.");
                                    gameState.CancelPathExecution(true);
                                    continue;
                                }
                                int energyCost = gameState.GetMovementEnergyCost(moveAction, gameState.PathExecutionMapView == MapView.Local);
                                if (gameState.PathExecutionMapView == MapView.Local && moveAction.IsRunning && !_localRunCostApplied)
                                {
                                    energyCost = 1; // Initial cost to start running locally
                                }
                                if (!gameState.PlayerStats.CanExertEnergy(energyCost))
                                {
                                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Not enough energy to move! Need {energyCost}, have {gameState.PlayerStats.CurrentEnergyPoints}");
                                    gameState.CancelPathExecution(true);
                                    continue;
                                }
                            }

                            // If checks pass, calculate time, dequeue, add component, and pass time.
                            int secondsToPass = CalculateSecondsForAction(gameState, nextAction);
                            actionQueueComp.ActionQueue.Dequeue();
                            if (nextAction is MoveAction ma) Core.ComponentStore.AddComponent(entityId, ma);
                            else if (nextAction is RestAction ra) Core.ComponentStore.AddComponent(entityId, ra);

                            Core.CurrentWorldClockManager.PassTime(seconds: secondsToPass);
                        }
                        else if (entityId != gameState.PlayerEntityId)
                        {
                            // Logic for NPCs would go here. For now, they just process instantly.
                            IAction nextAction = actionQueueComp.ActionQueue.Dequeue();
                            if (nextAction is MoveAction ma) Core.ComponentStore.AddComponent(entityId, ma);
                            else if (nextAction is RestAction ra) Core.ComponentStore.AddComponent(entityId, ra);
                        }
                    }
                }
            }

            // If time is passing for the player's action, halt all further processing for this frame.
            if (Core.CurrentWorldClockManager.IsInterpolatingTime) return;

            // --- Phase 2: REAL PROCESSING ---
            // This phase applies the effects of actions that have just finished (i.e., time has passed).

            // Process MoveActions
            var entitiesWithMoveAction = Core.ComponentStore.GetAllEntitiesWithComponent<MoveAction>().ToList();
            foreach (var entityId in entitiesWithMoveAction)
            {
                var moveAction = Core.ComponentStore.GetComponent<MoveAction>(entityId);
                if (moveAction != null && !moveAction.IsComplete)
                {
                    ApplyMoveActionEffects(gameState, entityId, moveAction);
                    moveAction.IsComplete = true;
                    if (entityId == gameState.PlayerEntityId) _isFirstPlayerAction = false;
                }
            }

            // Process RestActions
            var entitiesWithRestAction = Core.ComponentStore.GetAllEntitiesWithComponent<RestAction>().ToList();
            foreach (var entityId in entitiesWithRestAction)
            {
                var restAction = Core.ComponentStore.GetComponent<RestAction>(entityId);
                if (restAction != null && !restAction.IsComplete)
                {
                    ApplyRestActionEffects(gameState, entityId, restAction);
                    restAction.IsComplete = true;
                    if (entityId == gameState.PlayerEntityId) _isFirstPlayerAction = false;
                }
            }

            // --- Phase 3: CLEANUP ---
            // This phase removes completed action components.

            var completedMoveEntities = Core.ComponentStore.GetAllEntitiesWithComponent<MoveAction>()
                .Where(id => Core.ComponentStore.GetComponent<MoveAction>(id)?.IsComplete == true)
                .ToList();
            foreach (var entityId in completedMoveEntities)
            {
                Core.ComponentStore.RemoveComponent<MoveAction>(entityId);
            }

            var completedRestEntities = Core.ComponentStore.GetAllEntitiesWithComponent<RestAction>()
                .Where(id => Core.ComponentStore.GetComponent<RestAction>(id)?.IsComplete == true)
                .ToList();
            foreach (var entityId in completedRestEntities)
            {
                Core.ComponentStore.RemoveComponent<RestAction>(entityId);
            }

            // After all phases, check if the player's queue is now empty to end the execution state.
            if (gameState.IsExecutingPath)
            {
                var playerQueueComp = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId);
                bool playerHasAction = Core.ComponentStore.HasComponent<MoveAction>(gameState.PlayerEntityId) ||
                                       Core.ComponentStore.HasComponent<RestAction>(gameState.PlayerEntityId);

                if (playerQueueComp != null && playerQueueComp.ActionQueue.Count == 0 && !playerHasAction)
                {
                    gameState.ToggleExecutingPath(false);
                    Core.CurrentTerminalRenderer.AddOutputToHistory("Action queue completed.");
                }
            }
        }

        private int CalculateSecondsForAction(GameState gameState, IAction action)
        {
            // Only calculate for player, as only they drive time
            if (action.ActorId != gameState.PlayerEntityId) return 0;

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

                return fullDuration;
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
            if (stats == null) return; // Can't move without stats

            // For now, only player movement is fully implemented
            if (entityId == gameState.PlayerEntityId)
            {
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
            // else: NPC movement logic would go here
        }

        private void ApplyRestActionEffects(GameState gameState, int entityId, RestAction action)
        {
            var stats = Core.ComponentStore.GetComponent<StatsComponent>(entityId);
            if (stats == null) return;

            stats.Rest(action.RestType);

            if (entityId == gameState.PlayerEntityId)
            {
                string restType = action.RestType.ToString().Replace("Rest", "");
                Core.CurrentTerminalRenderer.AddOutputToHistory($"[rest]Completed {restType.ToLower()} rest. Energy is now {stats.CurrentEnergyPoints}/{stats.MaxEnergyPoints}.");
            }
        }
    }
}