using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using ProjectVagabond.Combat.UI;
using System.Diagnostics;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// Handles start-of-turn effects and determines the next actor.
    /// </summary>
    public class TurnStartState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            var gameState = ServiceLocator.Get<GameState>();
            var currentEntityId = combatManager.CurrentTurnEntityId;
            string entityName = (currentEntityId == gameState.PlayerEntityId) ? "Player" : currentEntityId.ToString();
            Debug.WriteLine($"  --- Turn Start: Entity {entityName} ---");

            var componentStore = ServiceLocator.Get<ComponentStore>();
            var actionManager = ServiceLocator.Get<ActionManager>();
            var itemManager = ServiceLocator.Get<ItemManager>();
            var random = new System.Random();

            var deckComp = componentStore.GetComponent<CombatDeckComponent>(currentEntityId);
            if (deckComp == null)
            {
                Debug.WriteLine($"    ... [ERROR] Entity {currentEntityId} has no CombatDeckComponent. Ending turn.");
                combatManager.FSM.ChangeState(new TurnEndState(), combatManager);
                return;
            }

            // --- Draw Phase ---
            const int cardsToDraw = 4;
            for (int i = 0; i < cardsToDraw; i++)
            {
                if (deckComp.DrawPile.Count == 0)
                {
                    if (deckComp.DiscardPile.Count == 0) break; // No cards left to draw
                    Debug.WriteLine($"    ... Reshuffling discard pile into draw pile for Entity {currentEntityId}.");
                    deckComp.DrawPile.AddRange(deckComp.DiscardPile);
                    deckComp.DiscardPile.Clear();
                    deckComp.DrawPile = deckComp.DrawPile.OrderBy(x => random.Next()).ToList();
                }

                string drawnCardId = deckComp.DrawPile[0];
                deckComp.DrawPile.RemoveAt(0);
                deckComp.Hand.Add(drawnCardId);
            }
            Debug.WriteLine($"    ... Drew {deckComp.Hand.Count} cards for Entity {currentEntityId}.");

            // --- Temporary Card Generation ---
            var combatantComp = componentStore.GetComponent<CombatantComponent>(currentEntityId);
            var equipmentComp = componentStore.GetComponent<EquipmentComponent>(currentEntityId);
            string weaponId = equipmentComp?.EquippedWeaponId ?? combatantComp?.DefaultWeaponId;
            string temporaryWeaponActionId = null;
            if (!string.IsNullOrEmpty(weaponId))
            {
                var weapon = itemManager.GetWeapon(weaponId);
                temporaryWeaponActionId = weapon?.GrantedActionIds?.FirstOrDefault();
                Debug.WriteLine($"    ... Generated temporary weapon action '{temporaryWeaponActionId}' for Entity {currentEntityId}.");
            }

            // --- Transition ---
            if (currentEntityId == gameState.PlayerEntityId)
            {
                // Build the visual hand for the player
                var handCards = new List<CombatCard>();
                foreach (var actionId in deckComp.Hand)
                {
                    var actionData = actionManager.GetAction(actionId);
                    if (actionData != null)
                    {
                        handCards.Add(new CombatCard(actionData));
                    }
                }
                // Add the temporary weapon card
                if (!string.IsNullOrEmpty(temporaryWeaponActionId))
                {
                    var actionData = actionManager.GetAction(temporaryWeaponActionId);
                    if (actionData != null)
                    {
                        handCards.Add(new CombatCard(actionData) { IsTemporary = true });
                    }
                }

                combatManager.ActionHandUI.SetHand(handCards);
                combatManager.FSM.ChangeState(new PlayerActionSelectionState(), combatManager);
            }
            else // AI's turn
            {
                // Add temporary weapon action to AI's logical hand
                if (!string.IsNullOrEmpty(temporaryWeaponActionId))
                {
                    deckComp.Hand.Add(temporaryWeaponActionId);
                }
                combatManager.FSM.ChangeState(new AIActionSelectionState(), combatManager);
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