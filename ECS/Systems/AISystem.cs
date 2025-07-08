using Microsoft.Xna.Framework;
using System;
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
        private readonly Random _random = new();
        private static readonly Vector2[] _neighborOffsets = new Vector2[]
        {
            new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0),
            new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1)
        };

        // This system is no longer updated by the SystemManager in real-time.
        // Its logic is triggered by the OnTimePassed event from the WorldClockManager.
        public void Update(GameTime gameTime) { }

        /// <summary>
        /// Processes all active AI entities, giving them a time budget to think and act.
        /// </summary>
        /// <param name="timeBudget">The amount of in-game seconds that just passed.</param>
        public void ProcessEntities(int timeBudget)
        {
            var gameState = Core.CurrentGameState;

            // Don't process AI movement/decisions if in combat
            if (gameState.IsInCombat) return;

            foreach (var entityId in gameState.ActiveEntities)
            {
                var aiComp = Core.ComponentStore.GetComponent<AIComponent>(entityId);
                if (aiComp == null || !Core.ComponentStore.HasComponent<NPCTagComponent>(entityId))
                {
                    continue; // Not a relevant AI entity
                }

                // Grant the time budget from the player's action
                aiComp.ActionTimeBudget += timeBudget;

                // Process this entity's decisions and actions as long as it has budget
                while (aiComp.ActionTimeBudget > 0)
                {
                    var actionQueueComp = Core.ComponentStore.GetComponent<ActionQueueComponent>(entityId);
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
            var gameState = Core.CurrentGameState;
            if (!gameState.IsInCombat) return;

            var combatStats = Core.ComponentStore.GetComponent<CombatStatsComponent>(entityId);
            var availableAttacks = Core.ComponentStore.GetComponent<AvailableAttacksComponent>(entityId);
            var aiName = EntityNamer.GetName(entityId);

            if (combatStats == null || availableAttacks == null)
            {
                Core.CurrentTerminalRenderer.AddCombatLog($"[error]{aiName} cannot act in combat (missing stats/attacks).");
                Core.CombatTurnSystem.EndCurrentTurn(); // End turn anyway to not stall combat.
                return;
            }

            // Simple AI: Find the first affordable attack and use it on the player.
            var affordableAttack = availableAttacks.Attacks.FirstOrDefault(a => combatStats.ActionPoints >= a.ActionPointCost);

            if (affordableAttack != null)
            {
                // Found an attack, let's use it.
                var chosenAttack = new ChosenAttackComponent
                {
                    TargetId = gameState.PlayerEntityId,
                    AttackName = affordableAttack.Name
                };
                
                // Spend the action points.
                combatStats.ActionPoints -= affordableAttack.ActionPointCost;

                // Resolve the action immediately
                Core.CombatResolutionSystem.ResolveAction(entityId, chosenAttack);
            }
            else
            {
                // Can't afford any attacks, just log it.
                Core.CurrentTerminalRenderer.AddCombatLog($"{aiName} has no affordable attacks and does nothing.");
            }

            // The AI has made its decision (or decided to do nothing), so it ends its turn.
            Core.CombatTurnSystem.EndCurrentTurn();
        }

        /// <summary>
        /// The AI's "brain" FSM. It decides what to do and enqueues a single action.
        /// </summary>
        private void DecideNextAction(int entityId, AIComponent aiComp)
        {
            var gameState = Core.CurrentGameState;
            if (gameState.IsInCombat) return;

            var actionQueueComp = Core.ComponentStore.GetComponent<ActionQueueComponent>(entityId);
            var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(entityId);
            var combatantComp = Core.ComponentStore.GetComponent<CombatantComponent>(entityId);

            if (actionQueueComp == null || localPosComp == null || combatantComp == null) return;

            // Check for combat initiation
            var playerLocalPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(gameState.PlayerEntityId);
            if (playerLocalPosComp != null)
            {
                float distanceToPlayer = Vector2.Distance(localPosComp.LocalPosition, playerLocalPosComp.LocalPosition);
                if (distanceToPlayer <= combatantComp.AggroRange)
                {
                    gameState.InitiateCombat(new List<int> { entityId, gameState.PlayerEntityId });
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
                var localPosComp = Core.ComponentStore.GetComponent<LocalPositionComponent>(entityId);
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