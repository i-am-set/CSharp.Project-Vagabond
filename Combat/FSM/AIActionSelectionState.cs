using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The state where the AI determines its action for the turn.
    /// </summary>
    public class AIActionSelectionState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var actionManager = ServiceLocator.Get<ActionManager>();
            var gameState = ServiceLocator.Get<GameState>();
            var random = new Random();

            var aiId = combatManager.CurrentTurnEntityId;
            var aiComp = componentStore.GetComponent<AIComponent>(aiId);
            var deckComp = componentStore.GetComponent<CombatDeckComponent>(aiId);

            if (deckComp == null || !deckComp.Hand.Any() || aiComp == null)
            {
                Debug.WriteLine($"    ... Entity {aiId} has no actions. Ending its turn immediately.");
                combatManager.FSM.ChangeState(new ActionExecutionState(), combatManager);
                return;
            }

            string chosenActionId = null;

            // --- Decision Phase ---
            Debug.WriteLine($"    ... Entity {aiId} is choosing an action from hand: [{string.Join(", ", deckComp.Hand)}]");
            switch (aiComp.Intellect)
            {
                case AIIntellect.Dumb:
                    chosenActionId = deckComp.Hand[random.Next(deckComp.Hand.Count)];
                    break;

                case AIIntellect.Normal:
                    // Prioritize damage, but with a chance to do something random.
                    if (random.Next(0, 4) == 0) // 25% chance of random action
                    {
                        chosenActionId = deckComp.Hand[random.Next(deckComp.Hand.Count)];
                    }
                    else
                    {
                        // Find the first action that deals damage. A simple heuristic.
                        chosenActionId = deckComp.Hand.FirstOrDefault(id =>
                            actionManager.GetAction(id)?.Effects.Any(e => e.Type.Equals("DealDamage", StringComparison.OrdinalIgnoreCase)) ?? false
                        ) ?? deckComp.Hand[0]; // Fallback to first card
                    }
                    break;

                case AIIntellect.Optimal:
                    // TODO: Implement resource-aware and player-weakness logic.
                    // For now, it behaves like Normal.
                    chosenActionId = deckComp.Hand.FirstOrDefault(id =>
                        actionManager.GetAction(id)?.Effects.Any(e => e.Type.Equals("DealDamage", StringComparison.OrdinalIgnoreCase)) ?? false
                    ) ?? deckComp.Hand[0];
                    break;
            }
            Debug.WriteLine($"    ... Entity {aiId} chose action: {chosenActionId}");

            // --- Action Creation ---
            var actionData = actionManager.GetAction(chosenActionId);
            if (actionData != null)
            {
                var targetIds = new List<int>();
                if (actionData.TargetType == TargetType.SingleEnemy)
                {
                    targetIds.Add(gameState.PlayerEntityId); // Always target player for now
                }
                // Handle other target types if necessary

                const float aiSpeed = 5f; // Placeholder
                var aiAction = new CombatAction(aiId, actionData, aiSpeed, targetIds);
                combatManager.AddActionForTurn(aiAction);
            }
            else
            {
                Debug.WriteLine($"    ... [ERROR] AI tried to use non-existent action: {chosenActionId}");
            }

            // --- Transition ---
            Debug.WriteLine("    ... AI action selection complete.");
            combatManager.FSM.ChangeState(new ActionExecutionState(), combatManager);
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
        }
    }
}