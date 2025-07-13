using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the behavior of AI-controlled entities.
    /// </summary>
    public class AISystem : ISystem
    {
        private GameState _gameState;
        private readonly ComponentStore _componentStore;
        private readonly Random _random = new();
        private static readonly Vector2[] _neighborOffsets = new Vector2[]
        {
            new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0),
            new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1)
        };

        public AISystem()
        {
            _componentStore = ServiceLocator.Get<ComponentStore>();
        }

        // This system is not updated by the SystemManager in real-time.
        public void Update(GameTime gameTime) { }

        /// <summary>
        /// Plans and queues a full turn's worth of actions for an AI entity in combat.
        /// This function runs ONCE at the start of the AI's turn.
        /// </summary>
        /// <param name="entityId">The ID of the AI entity to process.</param>
        public void ProcessCombatTurn(int entityId)
        {
            _gameState ??= ServiceLocator.Get<GameState>();

            var aiPos = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            var combatant = _componentStore.GetComponent<CombatantComponent>(entityId);
            var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(entityId);
            var stats = _componentStore.GetComponent<StatsComponent>(entityId);
            var playerPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            var aiName = EntityNamer.GetName(entityId);

            if (aiPos == null || combatant == null || actionQueue == null || stats == null || playerPos == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]{aiName} cannot act (missing components)." });
                actionQueue?.ActionQueue.Enqueue(new EndTurnAction(entityId)); // Ensure turn ends even on error
                return;
            }

            // --- AI TURN PLANNING ---
            var simulatedPosition = aiPos.LocalPosition;

            // ** GOAL: Move into attack range, then attack. **
            // Pathfind to the player's position. The pathfinder is allowed to target an occupied tile.
            var path = _gameState.GetAffordablePath(entityId, simulatedPosition, playerPos.LocalPosition, true, out float pathTimeCost);

            var pathToMove = new List<Vector2>();
            if (path != null && path.Any())
            {
                // The actual path to travel is all steps except the last one (which is the target's tile).
                // If the path is only one step, it means we are already adjacent, so we don't move.
                if (path.Count > 1)
                {
                    pathToMove = path.Take(path.Count - 1).ToList();
                }
            }

            if (pathToMove.Any())
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} moves towards the player." });
                foreach (var step in pathToMove)
                {
                    actionQueue.ActionQueue.Enqueue(new MoveAction(entityId, step, true));
                }
            }

            // The position the AI will be in after moving. If no move, it's the starting position.
            var finalPositionAfterMove = pathToMove.Any() ? pathToMove.Last() : simulatedPosition;

            // Check if the AI's new position is in attack range of the player.
            if (Vector2.Distance(finalPositionAfterMove, playerPos.LocalPosition) <= combatant.AttackRange)
            {
                var attack = _componentStore.GetComponent<AvailableAttacksComponent>(entityId)?.Attacks.FirstOrDefault();
                if (attack != null)
                {
                    string message = pathToMove.Any() ? $"{aiName} will attack after moving." : $"{aiName} is in range and attacks!";
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = message });
                    actionQueue.ActionQueue.Enqueue(new AttackAction(entityId, _gameState.PlayerEntityId, attack.Name));
                }
            }

            // ** Final Step: Always queue an EndTurnAction to guarantee the turn ends. **
            actionQueue.ActionQueue.Enqueue(new EndTurnAction(entityId));
        }

        // --- Out of Combat Logic (Unchanged) ---
        public void ProcessEntities(int timeBudget)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.IsInCombat) return;

            foreach (var entityId in _gameState.ActiveEntities)
            {
                var aiComp = _componentStore.GetComponent<AIComponent>(entityId);
                if (aiComp == null || !_componentStore.HasComponent<NPCTagComponent>(entityId)) continue;

                aiComp.ActionTimeBudget += timeBudget;
                while (aiComp.ActionTimeBudget > 0)
                {
                    var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(entityId);
                    if (actionQueueComp == null) break;
                    if (actionQueueComp.ActionQueue.Count == 0) DecideNextAction(entityId, aiComp);
                    if (actionQueueComp.ActionQueue.Count == 0) { aiComp.ActionTimeBudget = 0; break; }

                    IAction nextAction = actionQueueComp.ActionQueue.Peek();
                    int actionCost = CalculateAIActionCost(nextAction);
                    if (aiComp.ActionTimeBudget >= actionCost)
                    {
                        actionQueueComp.ActionQueue.Dequeue();
                        aiComp.ActionTimeBudget -= actionCost;
                        ExecuteAIAction(entityId, nextAction);
                    }
                    else { break; }
                }
            }
        }

        private void DecideNextAction(int entityId, AIComponent aiComp)
        {
            var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(entityId);
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            var combatantComp = _componentStore.GetComponent<CombatantComponent>(entityId);
            if (actionQueueComp == null || localPosComp == null || combatantComp == null) return;

            var playerLocalPosComp = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            if (playerLocalPosComp != null)
            {
                if (Vector2.Distance(localPosComp.LocalPosition, playerLocalPosComp.LocalPosition) <= combatantComp.AggroRange)
                {
                    _gameState.InitiateCombat(new List<int> { entityId, _gameState.PlayerEntityId });
                    return;
                }
            }

            var shuffledOffsets = _neighborOffsets.OrderBy(v => _random.Next()).ToList();
            foreach (var offset in shuffledOffsets)
            {
                var targetPos = localPosComp.LocalPosition + offset;
                if (targetPos.X >= 0 && targetPos.X < Global.LOCAL_GRID_SIZE && targetPos.Y >= 0 && targetPos.Y < Global.LOCAL_GRID_SIZE)
                {
                    actionQueueComp.ActionQueue.Enqueue(new MoveAction(entityId, targetPos, false));
                    return;
                }
            }
        }

        private int CalculateAIActionCost(IAction action) => action is MoveAction ? 4 : 1;

        private void ExecuteAIAction(int entityId, IAction action)
        {
            if (action is MoveAction moveAction)
            {
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                if (localPosComp != null) localPosComp.LocalPosition = moveAction.Destination;
            }
        }
    }
}