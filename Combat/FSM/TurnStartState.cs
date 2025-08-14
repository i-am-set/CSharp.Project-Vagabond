using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using ProjectVagabond.Combat.UI;
using System.Diagnostics;
using ProjectVagabond.Combat.Effects;

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
            var deckComp = componentStore.GetComponent<CombatDeckComponent>(currentEntityId);
            if (deckComp == null)
            {
                Debug.WriteLine($"    ... [CRITICAL FAILURE] Entity {currentEntityId} has no CombatDeckComponent. Turn cannot proceed.");
                combatManager.FSM.ChangeState(new TurnEndState(), combatManager);
                return;
            }

            // --- Phase 1: Draw cards from the deck ---
            DrawCards(deckComp, currentEntityId);

            // --- Phase 2: Generate temporary actions for the turn (e.g., basic weapon attacks) ---
            var temporaryWeaponAction = GenerateTemporaryAction(combatManager, currentEntityId);

            // --- Failsafe for empty hand ---
            if (deckComp.Hand.Count == 0 && temporaryWeaponAction == null)
            {
                Debug.WriteLine($"    ... [WARNING] Entity {currentEntityId} has no cards to play. Skipping turn.");
                combatManager.FSM.ChangeState(new TurnEndState(), combatManager);
                return;
            }

            // --- Phase 3: Transition to the appropriate selection state ---
            if (currentEntityId == gameState.PlayerEntityId)
            {
                PopulatePlayerUI(combatManager, deckComp, temporaryWeaponAction);
                combatManager.FSM.ChangeState(new PlayerActionSelectionState(), combatManager);
            }
            else // AI's turn
            {
                PrepareAIHand(deckComp, temporaryWeaponAction);
                combatManager.FSM.ChangeState(new AIActionSelectionState(), combatManager);
            }
        }

        private void DrawCards(CombatDeckComponent deckComp, int entityId)
        {
            const int cardsToDraw = 4;
            var random = new System.Random();

            for (int i = 0; i < cardsToDraw; i++)
            {
                if (deckComp.DrawPile.Count == 0)
                {
                    if (deckComp.DiscardPile.Count == 0) break; // No cards left to draw
                    Debug.WriteLine($"    ... Reshuffling discard pile into draw pile for Entity {entityId}.");
                    deckComp.DrawPile.AddRange(deckComp.DiscardPile);
                    deckComp.DiscardPile.Clear();
                    deckComp.DrawPile = deckComp.DrawPile.OrderBy(x => random.Next()).ToList();
                }

                string drawnCardId = deckComp.DrawPile[0];
                deckComp.DrawPile.RemoveAt(0);
                deckComp.Hand.Add(drawnCardId);
            }
            Debug.WriteLine($"    ... Drew {deckComp.Hand.Count} cards for Entity {entityId}.");
        }

        private ActionData GenerateTemporaryAction(CombatManager combatManager, int entityId)
        {
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var itemManager = ServiceLocator.Get<ItemManager>();

            var combatantComp = componentStore.GetComponent<CombatantComponent>(entityId);
            var equipmentComp = componentStore.GetComponent<EquipmentComponent>(entityId);

            string weaponId = equipmentComp?.EquippedWeaponId;
            if (string.IsNullOrEmpty(weaponId))
            {
                weaponId = combatantComp?.DefaultWeaponId;
            }

            if (string.IsNullOrEmpty(weaponId))
            {
                Debug.WriteLine($"    ... [CRITICAL FAILURE] Entity {entityId} has no weapon or default weapon defined. No temporary attack generated.");
                return null;
            }

            var weapon = itemManager.GetWeapon(weaponId);
            if (weapon?.PrimaryAttack == null)
            {
                Debug.WriteLine($"    ... [CRITICAL FAILURE] Failed to generate temporary action: Weapon '{weaponId}' not found or has no PrimaryAttack defined.");
                return null;
            }

            // The ActionData is now fully defined in the weapon data. We just need to give it a unique ID for this turn.
            var temporaryWeaponAction = weapon.PrimaryAttack;
            temporaryWeaponAction.Id = $"temp_{weapon.Id}"; // Assign a temporary, unique ID for this turn.

            combatManager.AddTemporaryAction(temporaryWeaponAction);
            Debug.WriteLine($"    ... Generated temporary weapon action '{temporaryWeaponAction.Name}' for Entity {entityId}.");
            return temporaryWeaponAction;
        }

        private void PopulatePlayerUI(CombatManager combatManager, CombatDeckComponent deckComp, ActionData tempAction)
        {
            var actionManager = ServiceLocator.Get<ActionManager>();
            var handCards = new List<CombatCard>();

            foreach (var actionId in deckComp.Hand)
            {
                var actionData = actionManager.GetAction(actionId);
                if (actionData != null)
                {
                    handCards.Add(new CombatCard(actionData));
                }
            }
            if (tempAction != null)
            {
                handCards.Add(new CombatCard(tempAction) { IsTemporary = true });
            }

            combatManager.ActionHandUI.SetHand(handCards);
        }

        private void PrepareAIHand(CombatDeckComponent deckComp, ActionData tempAction)
        {
            if (tempAction != null)
            {
                deckComp.Hand.Add(tempAction.Id);
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