using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Processes the action queues of entities, executing one action at a time
    /// and applying its effects to the game state.
    /// </summary>
    public class ActionExecutionSystem : ISystem
    {
        private PendingAction _actionAwaitingExecution = null;
        private int _currentPathIndex = 0;

        public PendingAction CurrentActionAwaitingExecution => _actionAwaitingExecution;
        public int CurrentPathIndex => _currentPathIndex;

        public void StartExecution()
        {
            _currentPathIndex = 0;
            _actionAwaitingExecution = null;
        }

        public void StopExecution()
        {
            _currentPathIndex = 0;
            _actionAwaitingExecution = null;
        }

        public void Update(GameTime gameTime)
        {
            var gameState = Core.CurrentGameState;
            if (gameState.IsPaused) return;
            if (Core.CurrentWorldClockManager.IsInterpolatingTime) return;

            var actionQueueComp = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId);
            if (actionQueueComp == null) return;
            var actionQueue = actionQueueComp.ActionQueue;

            if (_actionAwaitingExecution != null)
            {
                bool success = ApplyActionEffects(gameState, _actionAwaitingExecution);
                _actionAwaitingExecution = null;

                if (success)
                {
                    _currentPathIndex++;
                    if (_currentPathIndex >= actionQueue.Count)
                    {
                        gameState.ToggleExecutingPath(false);
                        actionQueue.Clear();
                        Core.CurrentTerminalRenderer.AddOutputToHistory("Action queue completed.");
                    }
                }
                else
                {
                    gameState.CancelPathExecution(true);
                }
                return;
            }

            if (gameState.IsExecutingPath && _currentPathIndex < actionQueue.Count)
            {
                PendingAction nextAction = actionQueue[_currentPathIndex];
                _actionAwaitingExecution = nextAction;
                int secondsPassed = CalculateSecondsForAction(gameState, nextAction);
                Core.CurrentWorldClockManager.PassTime(seconds: secondsPassed);
            }
        }

        private int CalculateSecondsForAction(GameState gameState, PendingAction action)
        {
            bool isLocalMove = gameState.PathExecutionMapView == MapView.Local;
            switch (action.Type)
            {
                case ActionType.WalkMove:
                case ActionType.RunMove:
                    Vector2 previousPosition;
                    MapData mapData;
                    if (isLocalMove)
                    {
                        previousPosition = (_currentPathIndex > 0) ? gameState.PendingActions[_currentPathIndex - 1].Position : gameState.PlayerLocalPos;
                        mapData = default;
                    }
                    else
                    {
                        previousPosition = (_currentPathIndex > 0) ? gameState.PendingActions[_currentPathIndex - 1].Position : gameState.PlayerWorldPos;
                        mapData = gameState.GetMapDataAt((int)action.Position.X, (int)action.Position.Y);
                    }
                    Vector2 moveDirection = action.Position - previousPosition;
                    int fullDuration = gameState.GetSecondsPassedDuringMovement(action.Type, mapData, moveDirection, isLocalMove);

                    if (_currentPathIndex == 0 && !isLocalMove)
                    {
                        float timeScaleFactor = gameState.GetFirstMoveTimeScaleFactor(moveDirection);
                        return (int)Math.Ceiling(fullDuration * timeScaleFactor);
                    }

                    return fullDuration;
                case ActionType.ShortRest:
                    return gameState.PlayerStats.ShortRestDuration * 60;
                case ActionType.LongRest:
                    return gameState.PlayerStats.LongRestDuration * 60;
                case ActionType.FullRest:
                    return gameState.PlayerStats.FullRestDuration * 60;
                default:
                    return 0;
            }
        }

        private bool ApplyActionEffects(GameState gameState, PendingAction action)
        {
            bool isLocalMove = gameState.PathExecutionMapView == MapView.Local;
            var playerStats = gameState.PlayerStats;
            var playerEntityId = gameState.PlayerEntityId;

            switch (action.Type)
            {
                case ActionType.WalkMove:
                case ActionType.RunMove:
                    Vector2 nextPosition = action.Position;
                    if (!gameState.IsPositionPassable(nextPosition, gameState.PathExecutionMapView))
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Movement blocked at {nextPosition}.");
                        return false;
                    }

                    string moveType = action.Type == ActionType.RunMove ? "Ran" : "Walked";
                    int secondsPassedForAction = CalculateSecondsForAction(gameState, action);
                    string timeString = Core.CurrentWorldClockManager.GetCommaFormattedTimeFromSeconds(secondsPassedForAction);

                    if (isLocalMove)
                    {
                        if (action.Type == ActionType.RunMove)
                        {
                            int firstRunIndex = gameState.PendingActions.FindIndex(a => a.Type == ActionType.RunMove);
                            if (_currentPathIndex == firstRunIndex)
                            {
                                playerStats.ExertEnergy(1);
                            }
                        }
                        var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(playerEntityId);
                        localPosComp.LocalPosition = new Vector2(
                            MathHelper.Clamp(nextPosition.X, 0, Global.LOCAL_GRID_SIZE - 1),
                            MathHelper.Clamp(nextPosition.Y, 0, Global.LOCAL_GRID_SIZE - 1)
                        );
                    }
                    else
                    {
                        int energyCost = gameState.GetMovementEnergyCost(action, false);
                        if (!playerStats.CanExertEnergy(energyCost))
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Not enough energy to continue! Need {energyCost}, have {playerStats.CurrentEnergyPoints}");
                            return false;
                        }
                        playerStats.ExertEnergy(energyCost);

                        var posComp = Core.ComponentStore.GetComponent<PositionComponent>(playerEntityId);
                        Vector2 oldWorldPos = posComp.WorldPosition;
                        posComp.WorldPosition = nextPosition;

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
                    break;

                case ActionType.ShortRest:
                case ActionType.LongRest:
                case ActionType.FullRest:
                    playerStats.Rest(action.ActionRestType.Value);
                    string restType = action.ActionRestType.Value.ToString().Replace("Rest", "");
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[rest]Completed {restType.ToLower()} rest. Energy is now {playerStats.CurrentEnergyPoints}/{playerStats.MaxEnergyPoints}.");
                    break;
            }
            return true;
        }
    }
}