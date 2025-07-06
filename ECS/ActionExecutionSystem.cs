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
        private IAction _currentAction = null;
        private bool _isFirstActionInQueue = false;
        private bool _localRunCostApplied = false;

        public void StartExecution()
        {
            _currentAction = null;
            _isFirstActionInQueue = true;
            _localRunCostApplied = false;
        }

        public void StopExecution()
        {
            _currentAction = null;
            _isFirstActionInQueue = false;
            _localRunCostApplied = false;
        }

        public void Update(GameTime gameTime)
        {
            var gameState = Core.CurrentGameState;
            if (gameState.IsPaused) return;

            var actionQueueComp = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId);
            if (actionQueueComp == null) return;
            var actionQueue = actionQueueComp.ActionQueue;

            // If we are not executing a path, or the queue is empty, we might need to stop.
            if (!gameState.IsExecutingPath || actionQueue.Count == 0)
            {
                if (gameState.IsExecutingPath && actionQueue.Count == 0)
                {
                    gameState.ToggleExecutingPath(false);
                    Core.CurrentTerminalRenderer.AddOutputToHistory("Action queue completed.");
                }
                return;
            }

            // Don't process a new action while time is interpolating.
            if (Core.CurrentWorldClockManager.IsInterpolatingTime) return;

            // Time is not interpolating, so we can process the action at the front of the queue.
            // This is the action for which time has just finished passing.
            IAction actionToExecute = actionQueue.Peek();

            bool success = ApplyActionEffects(gameState, actionToExecute);

            if (success)
            {
                // Mark as complete and remove from queue.
                actionToExecute.IsComplete = true;
                actionQueue.Dequeue();
                _isFirstActionInQueue = false; // The first action is now done.
            }
            else
            {
                // Action failed, cancel the entire path.
                gameState.CancelPathExecution(true);
                return;
            }

            // If there's another action in the queue, start passing time for it.
            if (actionQueue.Count > 0)
            {
                IAction nextAction = actionQueue.Peek();
                int secondsPassed = CalculateSecondsForAction(gameState, nextAction);
                Core.CurrentWorldClockManager.PassTime(seconds: secondsPassed);
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

                if (_isFirstActionInQueue && !isLocalMove)
                {
                    float timeScaleFactor = gameState.GetFirstMoveTimeScaleFactor(moveDirection);
                    return (int)Math.Ceiling(fullDuration * timeScaleFactor);
                }

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

        private bool ApplyActionEffects(GameState gameState, IAction action)
        {
            bool isLocalMove = gameState.PathExecutionMapView == MapView.Local;
            var playerStats = gameState.PlayerStats;
            var playerEntityId = gameState.PlayerEntityId;

            if (action is MoveAction moveAction)
            {
                Vector2 nextPosition = moveAction.Destination;
                if (!gameState.IsPositionPassable(nextPosition, gameState.PathExecutionMapView))
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Movement blocked at {nextPosition}.");
                    return false;
                }

                string moveType = moveAction.IsRunning ? "Ran" : "Walked";
                int secondsPassedForAction = CalculateSecondsForAction(gameState, moveAction);
                string timeString = Core.CurrentWorldClockManager.GetCommaFormattedTimeFromSeconds(secondsPassedForAction);

                if (isLocalMove)
                {
                    if (moveAction.IsRunning && !_localRunCostApplied)
                    {
                        playerStats.ExertEnergy(1);
                        _localRunCostApplied = true;
                    }

                    var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(playerEntityId);
                    localPosComp.LocalPosition = new Vector2(
                        MathHelper.Clamp(nextPosition.X, 0, Global.LOCAL_GRID_SIZE - 1),
                        MathHelper.Clamp(nextPosition.Y, 0, Global.LOCAL_GRID_SIZE - 1)
                    );
                }
                else
                {
                    int energyCost = gameState.GetMovementEnergyCost(moveAction, false);
                    if (!playerStats.CanExertEnergy(energyCost))
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Not enough energy to continue! Need {energyCost}, have {playerStats.CurrentEnergyPoints}");
                        return false;
                    }
                    playerStats.ExertEnergy(energyCost);

                    var posComp = Core.ComponentStore.GetComponent<PositionComponent>(playerEntityId);
                    Vector2 oldWorldPos = posComp.WorldPosition;
                    posComp.WorldPosition = nextPosition;

                    // Update the entity's chunk registration after it moves.
                    Core.ChunkManager.UpdateEntityChunk(playerEntityId, oldWorldPos, nextPosition);

                    Vector2 moveDir = nextPosition - oldWorldPos;
                    Vector2 newLocalPos = new Vector2(32, 32);
                    if (moveDir.X > 0) newLocalPos.X = 0; else if (moveDir.X < 0) newLocalPos.X = 63;
                    if (moveDir.Y > 0) newLocalPos.Y = 0; else if (moveDir.Y < 0) newLocalPos.Y = 63;
                    if (moveDir.X != 0 && moveDir.Y == 0) newLocalPos.Y = 32;
                    if (moveDir.Y != 0 && moveDir.X == 0) newLocalPos.X = 32;

                    var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(playerEntityId);
                    localPosComp.LocalPosition = new Vector2(
                        MathHelper.Clamp(newLocalPos.X, 0, Global.LOCAL_GRID_SIZE - 1),
                        MathHelper.Clamp(newLocalPos.Y, 0, Global.LOCAL_GRID_SIZE - 1)
                    );

                    var mapData = gameState.GetMapDataAt((int)nextPosition.X, (int)nextPosition.Y);
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[khaki]{moveType} through[gold] {mapData.TerrainType.ToLower()}[khaki].[dim] ({timeString})");
                }
            }
            else if (action is RestAction restAction)
            {
                playerStats.Rest(restAction.RestType);
                string restType = restAction.RestType.ToString().Replace("Rest", "");
                Core.CurrentTerminalRenderer.AddOutputToHistory($"[rest]Completed {restType.ToLower()} rest. Energy is now {playerStats.CurrentEnergyPoints}/{playerStats.MaxEnergyPoints}.");
            }

            return true;
        }
    }
}