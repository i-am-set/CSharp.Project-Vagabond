using System;
using System.Collections.Generic;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Defines the distinct phases of the player's turn.
    /// </summary>
    public enum PlayerTurnState
    {
        Selecting,
        Confirming,
        Resolving
    }

    /// <summary>
    /// Manages the state of a combat encounter, including player action selection,
    /// synergy resolution, and turn phase progression.
    /// </summary>
    public class CombatManager
    {
        private readonly ActionManager _actionManager;
        private readonly GameState _gameState;

        /// <summary>
        /// The current phase of the player's turn.
        /// </summary>
        public PlayerTurnState CurrentState { get; private set; }

        /// <summary>
        /// A list of all entity IDs participating in the combat.
        /// </summary>
        public List<int> Combatants { get; private set; }

        /// <summary>
        /// The player's left hand.
        /// </summary>
        public PlayerHand LeftHand { get; }

        /// <summary>
        /// The player's right hand.
        /// </summary>
        public PlayerHand RightHand { get; }

        /// <summary>
        /// The ID of the synergy action that will be created if the current selections are confirmed.
        /// Null if the current combination does not result in a synergy.
        /// </summary>
        public string PotentialSynergyActionId { get; private set; }

        /// <summary>
        /// Initializes a new instance of the CombatManager class.
        /// </summary>
        /// <param name="leftHandSpeed">The base casting speed for the left hand.</param>
        /// <param name="rightHandSpeed">The base casting speed for the right hand.</param>
        public CombatManager(float leftHandSpeed, float rightHandSpeed)
        {
            _actionManager = ServiceLocator.Get<ActionManager>();
            _gameState = ServiceLocator.Get<GameState>();
            Combatants = new List<int>();
            LeftHand = new PlayerHand(HandType.Left, leftHandSpeed);
            RightHand = new PlayerHand(HandType.Right, rightHandSpeed);
            CurrentState = PlayerTurnState.Selecting;
        }

        /// <summary>
        /// Starts a new combat encounter with a given set of participants.
        /// </summary>
        /// <param name="combatants">A list of entity IDs for all combatants.</param>
        public void StartCombat(List<int> combatants)
        {
            Combatants.Clear();
            Combatants.AddRange(combatants);
            ResetTurn();
        }

        /// <summary>
        /// Selects an action for a specific hand.
        /// </summary>
        /// <param name="hand">The hand to select the action for.</param>
        /// <param name="actionId">The ID of the action to select.</param>
        public void SelectAction(HandType hand, string actionId)
        {
            if (CurrentState != PlayerTurnState.Selecting) return;

            if (hand == HandType.Left)
            {
                LeftHand.SelectAction(actionId);
            }
            else if (hand == HandType.Right)
            {
                RightHand.SelectAction(actionId);
            }

            var actionData = _actionManager.GetAction(actionId);
            if (actionData != null)
            {
                EventBus.Publish(new GameEvents.CardPlayed { CardActionData = actionData, TargetHand = hand });
            }

            UpdatePotentialSynergy();
            UpdateState();
        }

        /// <summary>
        /// Cancels the selected action for a single hand, returning to the selection phase.
        /// </summary>
        /// <param name="hand">The hand whose action should be canceled.</param>
        public void CancelAction(HandType hand)
        {
            if (CurrentState == PlayerTurnState.Resolving) return;

            string actionIdToReturn = null;
            if (hand == HandType.Left)
            {
                actionIdToReturn = LeftHand.SelectedActionId;
                LeftHand.ClearSelection();
            }
            else if (hand == HandType.Right)
            {
                actionIdToReturn = RightHand.SelectedActionId;
                RightHand.ClearSelection();
            }

            if (!string.IsNullOrEmpty(actionIdToReturn))
            {
                var actionData = _actionManager.GetAction(actionIdToReturn);
                if (actionData != null)
                {
                    EventBus.Publish(new GameEvents.CardReturnedToHand { CardActionData = actionData, SourceHand = hand });
                }
            }

            UpdatePotentialSynergy();
            UpdateState();
        }

        /// <summary>
        /// Cancels both selected actions when in the confirmation phase, returning to the start of the turn.
        /// </summary>
        public void CancelTurn()
        {
            if (CurrentState != PlayerTurnState.Confirming) return;
            ResetTurn();
        }

        /// <summary>
        /// Confirms the selected actions and moves to the resolution phase.
        /// </summary>
        public void ConfirmTurn()
        {
            if (CurrentState != PlayerTurnState.Confirming) return;
            CurrentState = PlayerTurnState.Resolving;
        }

        /// <summary>
        /// Resets the turn to its initial state, clearing all selections.
        /// </summary>
        public void ResetTurn()
        {
            string leftActionId = LeftHand.SelectedActionId;
            string rightActionId = RightHand.SelectedActionId;

            LeftHand.ClearSelection();
            RightHand.ClearSelection();
            PotentialSynergyActionId = null;
            CurrentState = PlayerTurnState.Selecting;

            if (!string.IsNullOrEmpty(leftActionId))
            {
                var actionData = _actionManager.GetAction(leftActionId);
                if (actionData != null) EventBus.Publish(new GameEvents.CardReturnedToHand { CardActionData = actionData, SourceHand = HandType.Left });
            }
            if (!string.IsNullOrEmpty(rightActionId))
            {
                var actionData = _actionManager.GetAction(rightActionId);
                if (actionData != null) EventBus.Publish(new GameEvents.CardReturnedToHand { CardActionData = actionData, SourceHand = HandType.Right });
            }
        }

        /// <summary>
        /// Updates the current turn state based on whether both hands have selected an action.
        /// </summary>
        private void UpdateState()
        {
            if (LeftHand.SelectedActionId != null && RightHand.SelectedActionId != null)
            {
                CurrentState = PlayerTurnState.Confirming;
            }
            else
            {
                CurrentState = PlayerTurnState.Selecting;
            }
        }

        /// <summary>
        /// Checks the currently selected actions and determines if they form a synergy.
        /// Updates the PotentialSynergyActionId property accordingly.
        /// </summary>
        private void UpdatePotentialSynergy()
        {
            PotentialSynergyActionId = null;
            if (string.IsNullOrEmpty(LeftHand.SelectedActionId) || string.IsNullOrEmpty(RightHand.SelectedActionId))
            {
                return;
            }

            var leftAction = _actionManager.GetAction(LeftHand.SelectedActionId);
            if (leftAction?.Combinations == null) return;

            // Check if the left action's synergy list contains the right action.
            foreach (var synergy in leftAction.Combinations)
            {
                if (synergy.PairedWith.Equals(RightHand.SelectedActionId, StringComparison.OrdinalIgnoreCase))
                {
                    PotentialSynergyActionId = synergy.CombinesToBecome;
                    return;
                }
            }

            // If not found, check the other way around.
            var rightAction = _actionManager.GetAction(RightHand.SelectedActionId);
            if (rightAction?.Combinations == null) return;

            foreach (var synergy in rightAction.Combinations)
            {
                if (synergy.PairedWith.Equals(LeftHand.SelectedActionId, StringComparison.OrdinalIgnoreCase))
                {
                    PotentialSynergyActionId = synergy.CombinesToBecome;
                    return;
                }
            }
        }

        /// <summary>
        /// Generates the list of CombatAction objects based on the player's current selections.
        /// This correctly handles synergies.
        /// </summary>
        /// <returns>A list of CombatAction objects for the player's turn.</returns>
        public List<CombatAction> GeneratePlayerActions()
        {
            var actions = new List<CombatAction>();
            int playerId = _gameState.PlayerEntityId;

            if (!string.IsNullOrEmpty(PotentialSynergyActionId))
            {
                // Create a single synergy action
                var synergyActionData = _actionManager.GetAction(PotentialSynergyActionId);
                if (synergyActionData != null)
                {
                    float effectiveSpeed = Math.Min(LeftHand.CastSpeed, RightHand.CastSpeed);
                    actions.Add(new CombatAction(playerId, synergyActionData, effectiveSpeed, LeftHand.SelectedActionId, RightHand.SelectedActionId));
                }
            }
            else
            {
                // Create two separate actions
                var leftActionData = _actionManager.GetAction(LeftHand.SelectedActionId);
                if (leftActionData != null)
                {
                    actions.Add(new CombatAction(playerId, leftActionData, LeftHand.CastSpeed, HandType.Left));
                }

                var rightActionData = _actionManager.GetAction(RightHand.SelectedActionId);
                if (rightActionData != null)
                {
                    actions.Add(new CombatAction(playerId, rightActionData, RightHand.CastSpeed, HandType.Right));
                }
            }

            return actions;
        }
    }
}