﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Combat.Effects;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// A phase where each combatant, in a fixed order, selects their action for the round.
    /// This state manages the sub-turn for each combatant's selection.
    /// </summary>
    public class ActionSelectionState : ICombatState
    {
        private bool _isWaitingForPlayerInput;
        private bool _isWaitingForAnimation;
        private float _failsafeTimer;
        private const float FAILSAFE_DURATION = 10f;

        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("--- PHASE: ACTION SELECTION ---");
            _isWaitingForPlayerInput = false;
            _isWaitingForAnimation = false;
            EventBus.Subscribe<GameEvents.ActionAnimationComplete>(OnPlayerCardAnimationCompleted);

            ProcessNextCombatant(combatManager);
        }

        private void ProcessNextCombatant(CombatManager combatManager)
        {
            var gameState = ServiceLocator.Get<GameState>();
            var currentEntityId = combatManager.CurrentTurnEntityId;
            string entityName = EntityNamer.GetName(currentEntityId);
            Debug.WriteLine($"  > Selecting action for: {entityName}");

            // --- Step 1: Draw cards and generate temporary actions (formerly TurnStartState) ---
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var deckComp = componentStore.GetComponent<CombatDeckComponent>(currentEntityId);
            if (deckComp == null)
            {
                Debug.WriteLine($"    [CRITICAL] Entity {currentEntityId} has no CombatDeckComponent. Skipping selection.");
                AdvanceToNext(combatManager);
                return;
            }

            DrawCards(deckComp, currentEntityId);
            var temporaryWeaponAction = GenerateTemporaryAction(combatManager, currentEntityId);

            if (deckComp.Hand.Count == 0 && temporaryWeaponAction == null)
            {
                Debug.WriteLine($"    [WARNING] Entity {currentEntityId} has no cards to play. Skipping selection.");
                AdvanceToNext(combatManager);
                return;
            }

            // --- Step 2: Choose action based on Player or AI ---
            if (currentEntityId == gameState.PlayerEntityId)
            {
                // Player's turn to select
                PopulatePlayerUI(combatManager, deckComp, temporaryWeaponAction);
                _isWaitingForPlayerInput = true;
                Debug.WriteLine("    ... Waiting for player input...");
            }
            else
            {
                // AI's turn to select
                PrepareAIHand(deckComp, temporaryWeaponAction);
                ChooseAIAction(combatManager);
                AdvanceToNext(combatManager);
            }
        }

        public void OnExit(CombatManager combatManager)
        {
            EventBus.Unsubscribe<GameEvents.ActionAnimationComplete>(OnPlayerCardAnimationCompleted);
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            if (_isWaitingForPlayerInput)
            {
                // Delegate updates to the input handler and UI
                combatManager.InputHandler?.Update(gameTime);
                combatManager.ActionHandUI?.Update(gameTime, combatManager.InputHandler, combatManager);
            }
            else if (_isWaitingForAnimation)
            {
                _failsafeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_failsafeTimer >= FAILSAFE_DURATION)
                {
                    Debug.WriteLine($"    [WARNING] Player action confirmation animation timed out. Forcing next state.");
                    OnPlayerCardAnimationCompleted(new GameEvents.ActionAnimationComplete());
                }
            }
        }

        // This is now called by CombatManager when the player confirms an action.
        // It transitions this state from waiting for input to waiting for animation.
        public void OnPlayerActionConfirmed()
        {
            _isWaitingForPlayerInput = false;
            _isWaitingForAnimation = true;
            _failsafeTimer = 0f;
            Debug.WriteLine("    ... Waiting for player card animation...");
        }

        // This is called when the card-play animation finishes.
        private void OnPlayerCardAnimationCompleted(GameEvents.ActionAnimationComplete e)
        {
            if (!_isWaitingForAnimation) return;
            _isWaitingForAnimation = false;
            Debug.WriteLine("    ... Player card animation complete.");
            AdvanceToNext(ServiceLocator.Get<CombatManager>());
        }

        private void AdvanceToNext(CombatManager combatManager)
        {
            combatManager.AdvanceTurn();
            if (combatManager.IsNewRound())
            {
                Debug.WriteLine("--- END PHASE: ACTION SELECTION ---\n");
                combatManager.FSM.ChangeState(new TurnSpeedRollState(), combatManager);
            }
            else
            {
                ProcessNextCombatant(combatManager);
            }
        }

        #region AI Action Selection Logic
        private void ChooseAIAction(CombatManager combatManager)
        {
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var actionManager = ServiceLocator.Get<ActionManager>();
            var gameState = ServiceLocator.Get<GameState>();
            var random = new Random();

            var aiId = combatManager.CurrentTurnEntityId;
            var aiComp = componentStore.GetComponent<AIComponent>(aiId);
            var deckComp = componentStore.GetComponent<CombatDeckComponent>(aiId);
            string chosenActionId = null;

            Debug.WriteLine($"    ... AI is choosing from hand: [{string.Join(", ", deckComp.Hand)}]");
            switch (aiComp.Intellect)
            {
                case AIIntellect.Dumb:
                    chosenActionId = deckComp.Hand[random.Next(deckComp.Hand.Count)];
                    break;
                case AIIntellect.Normal:
                    if (random.Next(0, 4) == 0) { chosenActionId = deckComp.Hand[random.Next(deckComp.Hand.Count)]; }
                    else { chosenActionId = deckComp.Hand.FirstOrDefault(id => actionManager.GetAction(id)?.Effects.Any(e => e.Type.Equals("DealDamage", StringComparison.OrdinalIgnoreCase)) ?? false) ?? deckComp.Hand[0]; }
                    break;
                case AIIntellect.Optimal:
                    chosenActionId = deckComp.Hand.FirstOrDefault(id => actionManager.GetAction(id)?.Effects.Any(e => e.Type.Equals("DealDamage", StringComparison.OrdinalIgnoreCase)) ?? false) ?? deckComp.Hand[0];
                    break;
            }
            Debug.WriteLine($"    ... AI chose action: {chosenActionId}");

            var actionData = actionManager.GetAction(chosenActionId);
            if (actionData != null)
            {
                var targetIds = new List<int>();
                if (actionData.TargetType == TargetType.SingleEnemy) { targetIds.Add(gameState.PlayerEntityId); }
                var aiAction = new CombatAction(aiId, actionData, targetIds);
                combatManager.AddActionForTurn(aiAction);
            }
        }
        #endregion

        #region Card Management Logic
        private void DrawCards(CombatDeckComponent deckComp, int entityId)
        {
            const int cardsToDraw = 4;
            var random = new System.Random();
            int cardsDrawn = 0;
            for (int i = 0; i < cardsToDraw; i++)
            {
                if (deckComp.DrawPile.Count == 0)
                {
                    if (deckComp.DiscardPile.Count == 0) break;
                    Debug.WriteLine($"    ... Reshuffling discard pile into draw pile for Entity {entityId}.");
                    deckComp.DrawPile.AddRange(deckComp.DiscardPile);
                    deckComp.DiscardPile.Clear();
                    deckComp.DrawPile = deckComp.DrawPile.OrderBy(x => random.Next()).ToList();
                }
                if (deckComp.DrawPile.Any())
                {
                    string drawnCardId = deckComp.DrawPile[0];
                    deckComp.DrawPile.RemoveAt(0);
                    deckComp.Hand.Add(drawnCardId);
                    cardsDrawn++;
                }
            }
            Debug.WriteLine($"    ... Drew {cardsDrawn} cards.");
        }

        private ActionData GenerateTemporaryAction(CombatManager combatManager, int entityId)
        {
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var itemManager = ServiceLocator.Get<ItemManager>();
            var combatantComp = componentStore.GetComponent<CombatantComponent>(entityId);
            var equipmentComp = componentStore.GetComponent<EquipmentComponent>(entityId);
            string weaponId = equipmentComp?.EquippedWeaponId ?? combatantComp?.DefaultWeaponId;
            if (string.IsNullOrEmpty(weaponId)) return null;
            var weapon = itemManager.GetWeapon(weaponId);
            if (weapon?.PrimaryAttack == null) return null;
            var temporaryWeaponAction = weapon.PrimaryAttack;
            temporaryWeaponAction.Id = $"temp_{weapon.Id}";
            combatManager.AddTemporaryAction(temporaryWeaponAction);
            Debug.WriteLine($"    ... Generated temporary weapon action '{temporaryWeaponAction.Name}'.");
            return temporaryWeaponAction;
        }

        private void PopulatePlayerUI(CombatManager combatManager, CombatDeckComponent deckComp, ActionData tempAction)
        {
            var actionManager = ServiceLocator.Get<ActionManager>();
            var handCards = new List<CombatCard>();
            foreach (var actionId in deckComp.Hand)
            {
                var actionData = actionManager.GetAction(actionId);
                if (actionData != null) { handCards.Add(new CombatCard(actionData)); }
            }
            if (tempAction != null) { handCards.Add(new CombatCard(tempAction) { IsTemporary = true }); }
            combatManager.ActionHandUI.SetHand(handCards);
        }

        private void PrepareAIHand(CombatDeckComponent deckComp, ActionData tempAction)
        {
            if (tempAction != null) { deckComp.Hand.Add(tempAction.Id); }
        }
        #endregion
    }
}
