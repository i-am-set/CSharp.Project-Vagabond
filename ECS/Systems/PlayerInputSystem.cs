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
        private readonly ComponentStore _componentStore;

        public PlayerInputSystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        public void Update(GameTime gameTime) { }

        public void QueueNewPath(GameState gameState, List<Vector2> path, bool isRunning)
        {
            CancelPendingActions(gameState);
            AppendPath(gameState, path, isRunning);
            EventBus.Publish(new GameEvents.ActionQueueChanged());
        }

        public void AppendPath(GameState gameState, List<Vector2> path, bool isRunning)
        {
            if (path == null) return;

            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            var playerWorldPos = _componentStore.GetComponent<PositionComponent>(gameState.PlayerEntityId).WorldPosition;
            var playerEntityId = gameState.PlayerEntityId;

            bool isLocalPath = gameState.CurrentMapView == MapView.Local;

            if (!isRunning || isLocalPath)
            {
                foreach (var pos in path)
                {
                    actionQueue.Enqueue(new MoveAction(playerEntityId, pos, isRunning));
                }
                EventBus.Publish(new GameEvents.ActionQueueChanged());
                return;
            }

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
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Cannot queue path. Not enough energy even after a short rest." });
                        EventBus.Publish(new GameEvents.ActionQueueChanged());
                        return;
                    }
                }
                else
                {
                    actionQueue.Enqueue(nextAction);
                }
            }
            EventBus.Publish(new GameEvents.ActionQueueChanged());
        }

        public void CancelPendingActions(GameState gameState)
        {
            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            actionQueue.Clear();
            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Pending actions cleared." });
            EventBus.Publish(new GameEvents.ActionQueueChanged());
        }

        public void ClearPendingActions(GameState gameState)
        {
            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            actionQueue.Clear();
            EventBus.Publish(new GameEvents.ActionQueueChanged());
        }

        public void RemovePendingActionsFrom(GameState gameState, int index)
        {
            var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId);
            var actionQueue = actionQueueComp.ActionQueue;
            if (index < 0 || index >= actionQueue.Count) return;

            var tempList = actionQueue.ToList();
            tempList.RemoveRange(index, tempList.Count - index);

            actionQueue.Clear();
            foreach (var action in tempList)
            {
                actionQueue.Enqueue(action);
            }
            EventBus.Publish(new GameEvents.ActionQueueChanged());
        }

        public void QueueAction(GameState gameState, IAction action)
        {
            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            actionQueue.Enqueue(action);
            EventBus.Publish(new GameEvents.ActionQueueChanged());
        }

        public void QueueRest(GameState gameState, string[] args)
        {
            if (gameState.IsExecutingActions)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Cannot queue actions while executing a path." });
                return;
            }

            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            var playerLocalPos = _componentStore.GetComponent<LocalPositionComponent>(gameState.PlayerEntityId).LocalPosition;
            var playerWorldPos = _componentStore.GetComponent<PositionComponent>(gameState.PlayerEntityId).WorldPosition;
            var playerEntityId = gameState.PlayerEntityId;

            RestType restType = RestType.ShortRest;
            if (args.Length > 1)
            {
                if (args[1].ToLower() == "short") restType = RestType.ShortRest;
                else if (args[1].ToLower() == "long") restType = RestType.LongRest;
                else if (args[1].ToLower() == "full") restType = RestType.FullRest;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Queued a {args[1].ToLower()} rest." });
            }
            else
            {
                restType = RestType.ShortRest;
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Queued a short rest." });
            }

            Vector2 restPosition;
            var lastAction = actionQueue.LastOrDefault();
            if (lastAction is MoveAction lastMove) restPosition = lastMove.Destination;
            else if (lastAction is RestAction lastRest) restPosition = lastRest.Position;
            else restPosition = (gameState.CurrentMapView == MapView.Local ? playerLocalPos : playerWorldPos);

            actionQueue.Enqueue(new RestAction(playerEntityId, restType, restPosition));
            EventBus.Publish(new GameEvents.ActionQueueChanged());
        }

        private void QueueMovementInternal(GameState gameState, Vector2 direction, string[] args, bool isRunning)
        {
            if (gameState.IsExecutingActions)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "Cannot queue movements while executing a path." });
                return;
            }

            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(gameState.PlayerEntityId).ActionQueue;
            var playerStats = _componentStore.GetComponent<StatsComponent>(gameState.PlayerEntityId);
            var playerLocalPos = _componentStore.GetComponent<LocalPositionComponent>(gameState.PlayerEntityId).LocalPosition;
            var playerWorldPos = _componentStore.GetComponent<PositionComponent>(gameState.PlayerEntityId).WorldPosition;
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
                        EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Not enough energy to start a local run! <Requires 1 EP>" });
                        return;
                    }
                }
            }

            Vector2 oppositeDirection = -direction;
            int removedSteps = 0;

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
                    if (!gameState.IsPositionPassable(nextPos, gameState.CurrentMapView, out var mapData))
                    {
                        if (isLocalMove) EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Cannot move here... edge of the area." });
                        else EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Cannot move here... terrain is impassable! <{gameState.GetTerrainDescription(mapData).ToLower()}>" });
                        break;
                    }

                    var nextAction = new MoveAction(playerEntityId, nextPos, isRunning);
                    var tempQueue = new List<IAction>(actionQueue) { nextAction };
                    var simulationResult = gameState.SimulateActionQueueEnergy(tempQueue);

                    if (!simulationResult.possible)
                    {
                        if (gameState.IsFreeMoveMode && !isLocalMove)
                        {
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[warning]Not enough energy. Auto-queuing a short rest." });
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
                                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = "[error]Cannot move here... Not enough energy even after a rest!" });
                                break;
                            }
                        }
                        else
                        {
                            int stepCost = gameState.GetMovementEnergyCost(nextAction, isLocalMove);
                            if (isLocalMove && isRunning) stepCost = 1;
                            EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[error]Cannot move here... Not enough energy! <Requires {stepCost} EP>" });
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
                    if (removedSteps > 0) EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[undo]Backtracked {removedSteps} time(s), added {validSteps} {moveType}(s)" });
                    else EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"Queued {moveType} {validSteps} {args[0].ToLower()}" });
                }
                else if (removedSteps > 0)
                {
                    EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[undo]Backtracked {removedSteps} time(s)" });
                }
            }
            else if (removedSteps > 0)
            {
                EventBus.Publish(new GameEvents.TerminalMessagePublished { Message = $"[undo]Backtracked {removedSteps} time(s)" });
            }
            EventBus.Publish(new GameEvents.ActionQueueChanged());
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