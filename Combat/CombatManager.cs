using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Defines the distinct phases of the player's turn.
    /// </summary>
    public enum PlayerTurnState
    {
        Selecting,
        Resolving
    }

    /// <summary>
    /// Manages the state of a combat encounter, including player action selection,
    /// and turn phase progression.
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
        /// The player's chosen action for the current turn.
        /// </summary>
        public CombatAction PlayerAction { get; private set; }

        /// <summary>
        /// Initializes a new instance of the CombatManager class.
        /// </summary>
        public CombatManager()
        {
            _actionManager = ServiceLocator.Get<ActionManager>();
            _gameState = ServiceLocator.Get<GameState>();
            Combatants = new List<int>();
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
        /// Sets the player's action for the turn and moves to the resolution phase.
        /// </summary>
        /// <param name="actionId">The ID of the action being played.</param>
        /// <param name="targetIds">A list of entity IDs being targeted.</param>
        public void PlayAction(string actionId, List<int> targetIds)
        {
            if (CurrentState != PlayerTurnState.Selecting) return;

            var actionData = _actionManager.GetAction(actionId);
            if (actionData == null) return;

            // In a full game, player speed would come from a stats component.
            const float playerSpeed = 10f;

            PlayerAction = new CombatAction(_gameState.PlayerEntityId, actionData, playerSpeed, targetIds);

            EventBus.Publish(new GameEvents.CardPlayed { CardActionData = actionData, TargetEntityIds = targetIds });

            CurrentState = PlayerTurnState.Resolving;
        }

        /// <summary>
        /// Resets the turn to its initial state, clearing all selections.
        /// </summary>
        public void ResetTurn()
        {
            PlayerAction = null;
            CurrentState = PlayerTurnState.Selecting;
        }

        /// <summary>
        /// Generates the list of CombatAction objects based on the player's current selections.
        /// </summary>
        /// <returns>A list of CombatAction objects for the player's turn.</returns>
        public List<CombatAction> GeneratePlayerActions()
        {
            var actions = new List<CombatAction>();
            if (PlayerAction != null)
            {
                actions.Add(PlayerAction);
            }
            return actions;
        }
    }
}