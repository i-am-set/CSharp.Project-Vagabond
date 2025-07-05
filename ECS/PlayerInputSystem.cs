using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Handles player-specific input commands and manages the player's action queue.
    /// This system is primarily driven by calls from the CommandProcessor and MapInputHandler.
    /// </summary>
    public class PlayerInputSystem : ISystem
    {
        /// <summary>
        /// The Update method is not used by this system as its logic is triggered by external events (e.g., commands).
        /// </summary>
        public void Update(GameTime gameTime) { }

        public void QueueNewPath(GameState gameState, List<Vector2> path, bool isRunning)
        {
            CancelPendingActions(gameState);
            AppendPath(gameState, path, isRunning);
        }

        public void AppendPath(GameState gameState, List<Vector2> path, bool isRunning)
        {
            if (path == null) return;

            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            var playerStats = Core.ComponentStore.GetComponent<StatsComponent>(gameState.PlayerEntityId);
            var playerWorldPos = Core.ComponentStore.GetComponent<PositionComponent>(gameState.PlayerEntityId).WorldPosition;

            bool isLocalPath = gameState.CurrentMapView == MapView.Local;

            if (!isRunning || isLocalPath) // For walking or any local path, just add. No energy cost.
            {
                foreach (var pos in path)
                {
                    actionQueue.Add(new PendingAction(pos, isRunning: isRunning));
                }
                return;
            }

            // For running on the world map, check energy for each step.
            foreach (var nextPos in path)
            {
                var nextAction = new PendingAction(nextPos, isRunning: true);
                var tempQueue = new List<PendingAction>(actionQueue) { nextAction };
                var simulationResult = gameState.SimulateActionQueueEnergy(tempQueue);

                if (!simulationResult.possible)
                {
                    Vector2 restPosition = actionQueue.Any() ? actionQueue.Last().Position : playerWorldPos;
                    var restAction = new PendingAction(RestType.ShortRest, restPosition);
                    var tempQueueWithRest = new List<PendingAction>(actionQueue) { restAction, nextAction };

                    if (gameState.SimulateActionQueueEnergy(tempQueueWithRest).possible)
                    {
                        actionQueue.Add(restAction);
                        actionQueue.Add(nextAction);
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot queue path. Not enough energy even after a short rest.");
                        return; // Stop adding the rest of the path
                    }
                }
                else
                {
                    actionQueue.Add(nextAction);// Enough energy, just add the action
                }
            }
        }

        public void CancelPendingActions(GameState gameState)
        {
            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            actionQueue.Clear();
            Core.CurrentTerminalRenderer.AddOutputToHistory("Pending actions cleared.");
        }

        public void ClearPendingActions(GameState gameState)
        {
            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            actionQueue.Clear();
        }

        public void RemovePendingActionsFrom(GameState gameState, int index)
        {
            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            if (index < 0 || index >= actionQueue.Count) return;
            actionQueue.RemoveRange(index, actionQueue.Count - index);
        }

        public void QueueAction(GameState gameState, PendingAction action)
        {
            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            actionQueue.Add(action);
        }

        public void QueueRest(GameState gameState, string[] args)
        {
            if (gameState.IsExecutingPath)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("Cannot queue actions while executing a path.");
                return;
            }

            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            var playerLocalPos = Core.ComponentStore.GetComponent<LocalPositionComponent>(gameState.PlayerEntityId).LocalPosition;
            var playerWorldPos = Core.ComponentStore.GetComponent<PositionComponent>(gameState.PlayerEntityId).WorldPosition;

            RestType restType = RestType.ShortRest;
            if (args.Length > 1)
            {
                if (args[1].ToLower() == "short") restType = RestType.ShortRest;
                else if (args[1].ToLower() == "long") restType = RestType.LongRest;
                else if (args[1].ToLower() == "full") restType = RestType.FullRest;

                Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued a {args[1].ToLower()} rest.");
            }
            else
            {
                restType = RestType.ShortRest;
                Core.CurrentTerminalRenderer.AddOutputToHistory("Queued a short rest.");
            }

            Vector2 restPosition = actionQueue.Any() ? actionQueue.Last().Position : (gameState.CurrentMapView == MapView.Local ? playerLocalPos : playerWorldPos);
            actionQueue.Add(new PendingAction(restType, restPosition));
        }

        private void QueueMovementInternal(GameState gameState, Vector2 direction, string[] args, bool isRunning)
        {
            if (gameState.IsExecutingPath)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("Cannot queue movements while executing a path.");
                return;
            }

            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            var playerStats = Core.ComponentStore.GetComponent<StatsComponent>(gameState.PlayerEntityId);
            var playerLocalPos = Core.ComponentStore.GetComponent<LocalPositionComponent>(gameState.PlayerEntityId).LocalPosition;
            var playerWorldPos = Core.ComponentStore.GetComponent<PositionComponent>(gameState.PlayerEntityId).WorldPosition;

            bool isLocalMove = gameState.CurrentMapView == MapView.Local;
            int count = 1;
            if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
            {
                count = Math.Max(1, Math.Min(Global.MAX_SINGLE_MOVE_LIMIT, parsedCount));
            }

            if (isLocalMove && isRunning)
            {
                if (!actionQueue.Any(a => a.Type == ActionType.RunMove))
                {
                    if (!playerStats.CanExertEnergy(1))
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Not enough energy to start a local run! <Requires 1 EP>");
                        return;
                    }
                }
            }

            Vector2 oppositeDirection = -direction;
            int removedSteps = 0;

            while (actionQueue.Count > 0 && removedSteps < count)
            {
                int lastMoveIndex = actionQueue.FindLastIndex(a => a.Type == ActionType.WalkMove || a.Type == ActionType.RunMove);
                if (lastMoveIndex == -1) break;

                PendingAction lastMoveAction = actionQueue[lastMoveIndex];
                Vector2 prevPos = (lastMoveIndex > 0) ? actionQueue[lastMoveIndex - 1].Position : (isLocalMove ? playerLocalPos : playerWorldPos);
                Vector2 lastDirection = lastMoveAction.Position - prevPos;

                if (lastDirection == oppositeDirection)
                {
                    actionQueue.RemoveAt(lastMoveIndex);
                    removedSteps++;
                }
                else
                {
                    break;
                }
            }

            int remainingSteps = count - removedSteps;
            if (remainingSteps > 0)
            {
                Vector2 currentPos = actionQueue.Any() ? actionQueue.Last().Position : (isLocalMove ? playerLocalPos : playerWorldPos);
                int validSteps = 0;

                for (int i = 0; i < remainingSteps; i++)
                {
                    Vector2 nextPos = currentPos + direction;

                    if (!gameState.IsPositionPassable(nextPos, gameState.CurrentMapView))
                    {
                        if (isLocalMove)
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... edge of the area.");
                        }
                        else
                        {
                            var mapData = gameState.GetMapDataAt((int)nextPos.X, (int)nextPos.Y);
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... terrain is impassable! <{mapData.TerrainType.ToLower()}>");
                        }
                        break;
                    }

                    var nextAction = new PendingAction(nextPos, isRunning);
                    var tempQueue = new List<PendingAction>(actionQueue) { nextAction };
                    var simulationResult = gameState.SimulateActionQueueEnergy(tempQueue);

                    if (!simulationResult.possible)
                    {
                        if (gameState.IsFreeMoveMode && !isLocalMove)
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory("[warning]Not enough energy. Auto-queuing a short rest.");
                            Vector2 restPosition = actionQueue.Any() ? actionQueue.Last().Position : playerWorldPos;

                            var tempQueueWithRest = new List<PendingAction>(actionQueue);
                            tempQueueWithRest.Add(new PendingAction(RestType.ShortRest, restPosition));
                            tempQueueWithRest.Add(nextAction);

                            if (gameState.SimulateActionQueueEnergy(tempQueueWithRest).possible)
                            {
                                actionQueue.Add(new PendingAction(RestType.ShortRest, restPosition));
                            }
                            else
                            {
                                Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... Not enough energy even after a rest!");
                                break;
                            }
                        }
                        else
                        {
                            int stepCost = gameState.GetMovementEnergyCost(nextAction, isLocalMove);
                            if (isLocalMove && isRunning) stepCost = 1;
                            Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot move here... Not enough energy! <Requires {stepCost} EP>");
                            break;
                        }
                    }

                    currentPos = nextPos;
                    actionQueue.Add(nextAction);
                    validSteps++;
                }

                if (validSteps > 0)
                {
                    string moveType = isRunning ? "run" : "walk";
                    if (removedSteps > 0)
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Backtracked {removedSteps} time(s), added {validSteps} {moveType}(s)");
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"Queued {moveType} {validSteps} {args[0].ToLower()}");
                    }
                }
                else if (removedSteps > 0)
                {
                    Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Backtracked {removedSteps} time(s)");
                }
            }
            else if (removedSteps > 0)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory($"[undo]Backtracked {removedSteps} time(s)");
            }
        }

        public void QueueRunMovement(GameState gameState, Vector2 direction, string[] args)
        {
            QueueMovementInternal(gameState, direction, args, true);
        }

        public void QueueWalkMovement(GameState gameState, Vector2 direction, string[] args)
        {
            QueueMovementInternal(gameState, direction, args, false);
        }
    }
}