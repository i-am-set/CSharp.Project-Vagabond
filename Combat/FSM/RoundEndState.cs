﻿using Microsoft.Xna.Framework;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// Handles end-of-round effects, checks for victory or defeat, and loops back to action selection.
    /// </summary>
    public class RoundEndState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("\n--- PHASE: ACTION EXECUTION ROUND END ---");

            var componentStore = ServiceLocator.Get<ComponentStore>();
            var gameState = ServiceLocator.Get<GameState>();

            // --- Discard Phase for ALL combatants ---
            foreach (var entityId in combatManager.Combatants)
            {
                var deckComp = componentStore.GetComponent<CombatDeckComponent>(entityId);
                if (deckComp != null)
                {
                    deckComp.DiscardPile.AddRange(deckComp.Hand);
                    deckComp.Hand.Clear();
                }
            }
            Debug.WriteLine("  > All hands discarded.");

            combatManager.ClearTemporaryActions();

            // 1. Check for win/loss conditions
            if (CheckForDefeat(componentStore, gameState))
            {
                Debug.WriteLine("--- END PHASE: ACTION EXECUTION ROUND END ---\n");
                combatManager.FSM.ChangeState(new CombatDefeatState(), combatManager);
                return;
            }

            if (CheckForVictory(combatManager, componentStore, gameState))
            {
                Debug.WriteLine("--- END PHASE: ACTION EXECUTION ROUND END ---\n");
                combatManager.FSM.ChangeState(new CombatEndState(), combatManager);
                return;
            }

            // 2. Clear the actions from the completed round.
            combatManager.ClearActionsForTurn();
            Debug.WriteLine("  > Action list cleared for next round.");

            // 3. Transition back to the start of the next action selection phase.
            Debug.WriteLine("--- END PHASE: ACTION EXECUTION ROUND END ---");
            Debug.WriteLine("\n\n\n>>> Starting New Round <<<\n");
            // MODIFIED: Changed TurnStartState to the new ActionSelectionState.
            combatManager.FSM.ChangeState(new ActionSelectionState(), combatManager);
        }

        private bool CheckForDefeat(ComponentStore componentStore, GameState gameState)
        {
            var playerHealth = componentStore.GetComponent<HealthComponent>(gameState.PlayerEntityId);
            if (playerHealth != null && playerHealth.CurrentHealth <= 0)
            {
                Debug.WriteLine("  > Condition Met: Player Defeat");
                return true;
            }
            return false;
        }

        private bool CheckForVictory(CombatManager combatManager, ComponentStore componentStore, GameState gameState)
        {
            var enemies = combatManager.Combatants.Where(id => id != gameState.PlayerEntityId);
            bool allEnemiesDefeated = enemies.All(id =>
            {
                var health = componentStore.GetComponent<HealthComponent>(id);
                return health != null && health.CurrentHealth <= 0;
            });

            if (allEnemiesDefeated)
            {
                Debug.WriteLine("  > Condition Met: Victory");
                return true;
            }
            return false;
        }

        public void OnExit(CombatManager combatManager) { }
        public void Update(GameTime gameTime, CombatManager combatManager) { }
    }
}
