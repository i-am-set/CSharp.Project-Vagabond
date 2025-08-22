﻿using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The initial state of combat. Handles setup and transitions to the first turn.
    /// </summary>
    public class CombatStartState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("\n--- PHASE: COMBAT START ---");
            BuildDecksForAllCombatants(combatManager);
            Debug.WriteLine("  > Decks built for all combatants.");

            // The turn order is now fixed and simple, not based on initiative rolls.
            combatManager.SetInitiativeOrder(new List<int>(combatManager.Combatants));
            Debug.WriteLine("  > Fixed action selection order established.");
            Debug.WriteLine("--- END PHASE: COMBAT START ---\n");
            Debug.WriteLine(">>> Starting New Round <<<\n");

            // Immediately begin the first turn's action selection.
            combatManager.FSM.ChangeState(new ActionSelectionState(), combatManager);
        }

        private void BuildDecksForAllCombatants(CombatManager combatManager)
        {
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var itemManager = ServiceLocator.Get<ItemManager>();
            var gameState = ServiceLocator.Get<GameState>();
            var random = new System.Random();

            foreach (var entityId in combatManager.Combatants)
            {
                var combatantComp = componentStore.GetComponent<CombatantComponent>(entityId);
                var equipmentComp = componentStore.GetComponent<EquipmentComponent>(entityId);
                var deckComp = componentStore.GetComponent<CombatDeckComponent>(entityId);
                if (deckComp == null)
                {
                    deckComp = new CombatDeckComponent();
                    componentStore.AddComponent(entityId, deckComp);
                }

                // MODIFIED: Explicitly clear all piles to ensure a clean slate for every combat.
                deckComp.DrawPile.Clear();
                deckComp.Hand.Clear();
                deckComp.DiscardPile.Clear();

                var permanentActionIds = new List<string>();

                // If this is the player, add cards from their persistent master deck.
                if (entityId == gameState.PlayerEntityId)
                {
                    var playerDeckComp = componentStore.GetComponent<PlayerDeckComponent>(entityId);
                    if (playerDeckComp?.MasterDeck != null)
                    {
                        permanentActionIds.AddRange(playerDeckComp.MasterDeck);
                    }
                }

                // Add innate actions
                if (combatantComp?.InnateActionIds != null)
                {
                    permanentActionIds.AddRange(combatantComp.InnateActionIds);
                }

                // Add special moves from the equipped weapon
                string weaponId = equipmentComp?.EquippedWeaponId;
                if (!string.IsNullOrEmpty(weaponId))
                {
                    var weapon = itemManager.GetWeapon(weaponId);
                    if (weapon?.GrantedActionIds != null)
                    {
                        permanentActionIds.AddRange(weapon.GrantedActionIds);
                    }
                }

                // Populate and shuffle the draw pile
                deckComp.DrawPile = permanentActionIds.Distinct().ToList();
                deckComp.DrawPile = deckComp.DrawPile.OrderBy(x => random.Next()).ToList();
            }
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
        }
    }
}
