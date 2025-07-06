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
            gameState.IsActionQueueDirty = true;
        }

        public void AppendPath(GameState gameState, List<Vector2> path, bool isRunning)
        {
            if (path == null) return;

            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            var playerStats = Core.ComponentStore.GetComponent<StatsComponent>(gameState.PlayerEntityId);
            var playerWorldPos = Core.ComponentStore.GetComponent<PositionComponent>(gameState.PlayerEntityId).WorldPosition;
            var playerEntityId = gameState.PlayerEntityId;

            bool isLocalPath = gameState.CurrentMapView == MapView.Local;

            if (!isRunning || isLocalPath) // For walking or any local path, just add. No energy cost.
            {
                foreach (var pos in path)
                {
                    actionQueue.Enqueue(new MoveAction(playerEntityId, pos, isRunning));
                }
                gameState.IsActionQueueDirty = true;
                return;
            }

            // For running on the world map, check energy for each step.
            foreach (var nextPos in path)
            {
                var nextAction = new MoveAction(playerEntityId, nextPos, true);
                var tempQueue = new List<IAction>(actionQueue) { nextAction };
                var simulationResult = gameState.SimulateActionQueueEnergy(tempQueue);

                if (!simulationResult.possible)
                {
                    Vector2 restPosition = actionQueue.Any() ? ((actionQueue.Last() as MoveAction)?.Destination ?? playerWorldPos) : playerWorldPos;
                    var restAction = new RestAction(playerEntityId, RestType.ShortRest, restPosition);
                    var tempQueueWithRest = new List<IAction>(actionQueue) { restAction, nextAction };

                    if (gameState.SimulateActionQueueEnergy(tempQueueWithRest).possible)
                    {
                        actionQueue.Enqueue(restAction);
                        actionQueue.Enqueue(nextAction);
                    }
                    else
                    {
                        Core.CurrentTerminalRenderer.AddOutputToHistory($"[error]Cannot queue path. Not enough energy even after a short rest.");
                        gameState.IsActionQueueDirty = true;
                        return; // Stop adding the rest of the path
                    }
                }
                else
                {
                    actionQueue.Enqueue(nextAction);// Enough energy, just add the action
                }
            }
            gameState.IsActionQueueDirty = true;
        }

        public void CancelPendingActions(GameState gameState)
        {
            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            actionQueue.Clear();
            Core.CurrentTerminalRenderer.AddOutputToHistory("Pending actions cleared.");
            gameState.IsActionQueueDirty = true;
        }

        public void ClearPendingActions(GameState gameState)
        {
            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            actionQueue.Clear();
            gameState.IsActionQueueDirty = true;
        }

        public void RemovePendingActionsFrom(GameState gameState, int index)
        {
            var actionQueueComp = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId);
            var actionQueue = actionQueueComp.ActionQueue;
            if (index < 0 || index >= actionQueue.Count) return;

            // Inefficient, but necessary to support indexed removal on a Queue as per instructions.
            var tempList = actionQueue.ToList();
            tempList.RemoveRange(index, tempList.Count - index);

            actionQueue.Clear();
            foreach (var action in tempList)
            {
                actionQueue.Enqueue(action);
            }
            gameState.IsActionQueueDirty = true;
        }

        public void QueueAction(GameState gameState, IAction action)
        {
            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            actionQueue.Enqueue(action);
            gameState.IsActionQueueDirty = true;
        }

        public void QueueRest(GameState gameState, string[] args)
        {
            if (gameState.IsExecutingActions)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("Cannot queue actions while executing a path.");
                return;
            }

            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            var playerLocalPos = Core.ComponentStore.GetComponent<LocalPositionComponent>(gameState.PlayerEntityId).LocalPosition;
            var playerWorldPos = Core.ComponentStore.GetComponent<PositionComponent>(gameState.PlayerEntityId).WorldPosition;
            var playerEntityId = gameState.PlayerEntityId;

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

            Vector2 restPosition;
            var lastAction = actionQueue.LastOrDefault();
            if (lastAction is MoveAction lastMove)
            {
                restPosition = lastMove.Destination;
            }
            else if (lastAction is RestAction lastRest)
            {
                restPosition = lastRest.Position;
            }
            else
            {
                restPosition = (gameState.CurrentMapView == MapView.Local ? playerLocalPos : playerWorldPos);
            }

            actionQueue.Enqueue(new RestAction(playerEntityId, restType, restPosition));
            gameState.IsActionQueueDirty = true;
        }

        private void QueueMovementInternal(GameState gameState, Vector2 direction, string[] args, bool isRunning)
        {
            if (gameState.IsExecutingActions)
            {
                Core.CurrentTerminalRenderer.AddOutputToHistory("Cannot queue movements while executing a path.");
                return;
            }

            var actionQueue = Core.ComponentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            var playerStats = Core.ComponentStore.GetComponent<StatsComponent>(gameState.PlayerEntityId);
            var playerLocalPos = Core.ComponentStore.GetComponent<LocalPositionComponent>(gameState.PlayerEntityId).LocalPosition;
            var playerWorldPos = Core.ComponentStore.GetComponent<PositionComponent>(gameState.PlayerEntityId).WorldPosition;
            var playerEntityId = gameState.PlayerEntityId;

            bool isLocalMove = gameState.CurrentMapView == MapView.Local;
            int count = 1;
            if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
            {
                count = Math.Max(1, Math.Min(Global.MAX_SINGLE_MOVE_LIMIT, parsedCount));
            }

            if (isLocalMove && isRunning)
            {
                if (!actionQueue.Any(a => a is MoveAction ma && ma.IsRunning))
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

            // This backtracking logic is inefficient with a Queue but maintained to follow original functionality.
            var actionList = actionQueue.ToList();
            while (actionList.Count > 0 && removedSteps < count)
            {
                int lastMoveIndex = actionList.FindLastIndex(a => a is MoveAction);
                if (lastMoveIndex == -1) break;

                MoveAction lastMoveAction = actionList[lastMoveIndex] as MoveAction;
                Vector2 prevPos;
                if (lastMoveIndex > 0)
                {
                    var prevAction = actionList[lastMoveIndex - 1];
                    prevPos = (prevAction is MoveAction prevMove) ? prevMove.Destination : (prevAction as RestAction)?.Position ?? (isLocalMove ? playerLocalPos : playerWorldPos);
                }
                else
                {
                    prevPos = isLocalMove ? playerLocalPos : playerWorldPos;
                }

                Vector2 lastDirection = lastMoveAction.Destination - prevPos;

                if (lastDirection == oppositeDirection)
                {
                    actionList.RemoveAt(lastMoveIndex);
                    removedSteps++;
                }
                else
                {
                    break;
                }
            }
            // Rebuild queue after potential removals
            actionQueue.Clear();
            foreach (var action in actionList) actionQueue.Enqueue(action);


            int remainingSteps = count - removedSteps;
            if (remainingSteps > 0)
            {
                Vector2 currentPos;
                var lastAction = actionQueue.LastOrDefault();
                if (lastAction is MoveAction lastMove) currentPos = lastMove.Destination;
                else if (lastAction is RestAction lastRest) currentPos = lastRest.Position;
                else currentPos = isLocalMove ? playerLocalPos : playerWorldPos;

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

                    var nextAction = new MoveAction(playerEntityId, nextPos, isRunning);
                    var tempQueue = new List<IAction>(actionQueue) { nextAction };
                    var simulationResult = gameState.SimulateActionQueueEnergy(tempQueue);

                    if (!simulationResult.possible)
                    {
                        if (gameState.IsFreeMoveMode && !isLocalMove)
                        {
                            Core.CurrentTerminalRenderer.AddOutputToHistory("[warning]Not enough energy. Auto-queuing a short rest.");
                            Vector2 restPosition = actionQueue.Any() ? ((actionQueue.Last() as MoveAction)?.Destination ?? playerWorldPos) : playerWorldPos;

                            var tempQueueWithRest = new List<IAction>(actionQueue);
                            tempQueueWithRest.Add(new RestAction(playerEntityId, RestType.ShortRest, restPosition));
                            tempQueueWithRest.Add(nextAction);

                            if (gameState.SimulateActionQueueEnergy(tempQueueWithRest).possible)
                            {
                                actionQueue.Enqueue(new RestAction(playerEntityId, RestType.ShortRest, restPosition));
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
                    actionQueue.Enqueue(nextAction);
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
            gameState.IsActionQueueDirty = true;
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