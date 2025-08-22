﻿using Microsoft.Xna.Framework;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// Handles end-of-turn effects and checks for victory or defeat conditions.
    /// </summary>
    public class TurnEndState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("  --- Round End ---");

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
            Debug.WriteLine("    ... All hands discarded.");

            // Clear any temporary actions created for this turn.
            combatManager.ClearTemporaryActions();

            // 1. Check for win/loss conditions
            if (CheckForDefeat(combatManager, componentStore, gameState))
            {
                combatManager.FSM.ChangeState(new CombatDefeatState(), combatManager);
                return;
            }

            if (CheckForVictory(combatManager, componentStore, gameState))
            {
                combatManager.FSM.ChangeState(new CombatEndState(), combatManager);
                return;
            }

            // 2. Clear the actions from the completed turn.
            combatManager.ClearActionsForTurn();

            // 3. Transition back to the start of the next turn selection phase.
            Debug.WriteLine("  --- Starting New Round ---");
            combatManager.FSM.ChangeState(new TurnStartState(), combatManager);
        }

        private bool CheckForDefeat(CombatManager combatManager, ComponentStore componentStore, GameState gameState)
        {
            var playerHealth = componentStore.GetComponent<HealthComponent>(gameState.PlayerEntityId);
            if (playerHealth != null && playerHealth.CurrentHealth <= 0)
            {
                Debug.WriteLine("  !!! PLAYER DEFEATED !!!");
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
                Debug.WriteLine("  !!! VICTORY !!!");
                return true;
            }
            return false;
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
        }
    }
}
