using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond
{
    /// <summary>
    /// Manages the behavior of AI-controlled entities based on a time budget
    /// granted by the player's actions.
    /// </summary>
    public class AISystem : ISystem
    {
        private GameState _gameState;
        private readonly ComponentStore _componentStore;
        private CombatTurnSystem _combatTurnSystem;

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

        // This system is no longer updated by the SystemManager in real-time.
        // Its logic is triggered by the OnTimePassed event from the WorldClockManager.
        public void Update(GameTime gameTime) { }

        /// <summary>
        /// Processes all active AI entities, giving them a time budget to think and act.
        /// </summary>
        /// <param name="timeBudget">The amount of in-game seconds that just passed.</param>
        public void ProcessEntities(int timeBudget)
        {
            _gameState ??= ServiceLocator.Get<GameState>();

            // Don't process AI movement/decisions if in combat
            if (_gameState.IsInCombat) return;

            foreach (var entityId in _gameState.ActiveEntities)
            {
                var aiComp = _componentStore.GetComponent<AIComponent>(entityId);
                if (aiComp == null || !_componentStore.HasComponent<NPCTagComponent>(entityId))
                {
                    continue; // Not a relevant AI entity
                }

                // Grant the time budget from the player's action
                aiComp.ActionTimeBudget += timeBudget;

                // Process this entity's decisions and actions as long as it has budget
                while (aiComp.ActionTimeBudget > 0)
                {
                    var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(entityId);
                    if (actionQueueComp == null) break;

                    // If the queue is empty, the AI needs to decide what to do next.
                    if (actionQueueComp.ActionQueue.Count == 0)
                    {
                        DecideNextAction(entityId, aiComp);
                    }

                    // If there's still nothing in the queue, the AI is idle or has initiated combat.
                    if (actionQueueComp.ActionQueue.Count == 0)
                    {
                        // Spend the remaining budget being idle.
                        aiComp.ActionTimeBudget = 0;
                        break;
                    }

                    // Process the next action in the queue if there's enough budget.
                    IAction nextAction = actionQueueComp.ActionQueue.Peek();
                    int actionCost = CalculateAIActionCost(nextAction);

                    if (aiComp.ActionTimeBudget >= actionCost)
                    {
                        // Execute the action
                        actionQueueComp.ActionQueue.Dequeue();
                        aiComp.ActionTimeBudget -= actionCost;
                        ExecuteAIAction(entityId, nextAction);
                    }
                    else
                    {
                        // Not enough budget to complete the next action, wait for more time.
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Processes a single turn for an AI entity during combat.
        /// The AI will decide on an action and then immediately end its turn.
        /// </summary>
        /// <param name="entityId">The ID of the AI entity to process.</param>
        public void ProcessCombatTurn(int entityId)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            _combatTurnSystem ??= ServiceLocator.Get<CombatTurnSystem>();

            if (!_gameState.IsInCombat) return;

            // Get AI components
            var aiPos = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            var availableAttacks = _componentStore.GetComponent<AvailableAttacksComponent>(entityId);
            var combatant = _componentStore.GetComponent<CombatantComponent>(entityId);
            var aiName = EntityNamer.GetName(entityId);

            // Get Player components
            var playerPos = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);

            if (aiPos == null || availableAttacks == null || combatant == null || playerPos == null)
            {
                EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"[error]{aiName} cannot act in combat (missing critical components)." });
                return;
            }

            float distanceToPlayer = Vector2.Distance(aiPos.LocalPosition, playerPos.LocalPosition);

            // --- DECISION TREE ---

            // 1. Attack or Move?
            if (distanceToPlayer <= combatant.AttackRange)
            {
                // In range, try to attack.
                var bestAttack = availableAttacks.Attacks
                    .OrderByDescending(a => a.DamageMultiplier)
                    .FirstOrDefault();

                if (bestAttack != null)
                {
                    var chosenAttack = new ChosenAttackComponent
                    {
                        TargetId = _gameState.PlayerEntityId,
                        AttackName = bestAttack.Name
                    };
                    _componentStore.AddComponent(entityId, chosenAttack);
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} decides to use {bestAttack.Name}." });
                }
                else
                {
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} is in range but has no attacks." });
                }
            }
            else
            {
                // Not in range, try to move closer.
                var path = _gameState.GetAffordablePath(entityId, aiPos.LocalPosition, playerPos.LocalPosition, false, GameState.COMBAT_TURN_DURATION_SECONDS, out _);

                if (path != null && path.Any())
                {
                    var actionQueue = _componentStore.GetComponent<ActionQueueComponent>(entityId);
                    foreach (var step in path)
                    {
                        actionQueue.ActionQueue.Enqueue(new MoveAction(entityId, step, false));
                    }
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} decides to move towards the player." });
                }
                else
                {
                    EventBus.Publish(new GameEvents.CombatLogMessagePublished { Message = $"{aiName} cannot find a path to the player." });
                }
            }
            // The AI's decision is made. The CombatProcessingSystem will execute it and then the turn will end.
        }

        /// <summary>
        /// The AI's "brain" FSM. It decides what to do and enqueues a single action.
        /// </summary>
        private void DecideNextAction(int entityId, AIComponent aiComp)
        {
            _gameState ??= ServiceLocator.Get<GameState>();
            if (_gameState.IsInCombat) return;

            var actionQueueComp = _componentStore.GetComponent<ActionQueueComponent>(entityId);
            var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
            var combatantComp = _componentStore.GetComponent<CombatantComponent>(entityId);

            if (actionQueueComp == null || localPosComp == null || combatantComp == null) return;

            // Check for combat initiation
            var playerLocalPosComp = _componentStore.GetComponent<LocalPositionComponent>(_gameState.PlayerEntityId);
            if (playerLocalPosComp != null)
            {
                float distanceToPlayer = Vector2.Distance(localPosComp.LocalPosition, playerLocalPosComp.LocalPosition);
                if (distanceToPlayer <= combatantComp.AggroRange)
                {
                    _gameState.InitiateCombat(new List<int> { entityId, _gameState.PlayerEntityId });
                    return; // Combat initiated, no further action decided this tick.
                }
            }

            // If not initiating combat, wander randomly.
            var shuffledOffsets = _neighborOffsets.OrderBy(v => _random.Next()).ToList();
            foreach (var offset in shuffledOffsets)
            {
                var targetPos = localPosComp.LocalPosition + offset;
                if (targetPos.X >= 0 && targetPos.X < Global.LOCAL_GRID_SIZE &&
                    targetPos.Y >= 0 && targetPos.Y < Global.LOCAL_GRID_SIZE)
                {
                    actionQueueComp.ActionQueue.Enqueue(new MoveAction(entityId, targetPos, false));
                    return; // Queued one action, decision is made for now.
                }
            }
        }

        /// <summary>
        /// Calculates the time cost (in seconds) for an AI's action.
        /// </summary>
        private int CalculateAIActionCost(IAction action)
        {
            if (action is MoveAction)
            {
                // Simple cost for now. Could be based on stats later.
                return 4;
            }
            return 1; // Default cost for thinking or other simple actions.
        }

        /// <summary>
        /// Immediately applies the effects of an AI's action.
        /// </summary>
        private void ExecuteAIAction(int entityId, IAction action)
        {
            if (action is MoveAction moveAction)
            {
                var localPosComp = _componentStore.GetComponent<LocalPositionComponent>(entityId);
                if (localPosComp != null)
                {
                    localPosComp.LocalPosition = moveAction.Destination;
                    // Check if the NPC moved to the edge of the local map
                    if (localPosComp.LocalPosition.X == 0 || localPosComp.LocalPosition.X == Global.LOCAL_GRID_SIZE - 1 ||
                        localPosComp.LocalPosition.Y == 0 || localPosComp.LocalPosition.Y == Global.LOCAL_GRID_SIZE - 1)
                    {
                        // TODO: Add logic for chunk transition here in the future.
                    }
                }
            }
            // Other action types (RestAction, etc.) could be handled here.
        }
    }
}