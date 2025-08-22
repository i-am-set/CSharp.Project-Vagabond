﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.FSM;
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
    /// Manages the state of a combat encounter, including player action selection,
    /// and turn phase progression.
    /// </summary>
    public class CombatManager
    {
        private readonly ActionManager _actionManager;
        private readonly GameState _gameState;
        private CombatFSM _fsm;
        public CombatFSM FSM => _fsm;

        private List<CombatAction> _actionsForTurn = new List<CombatAction>();
        private List<int> _initiativeOrder = new List<int>();
        private int _currentTurnIndex = -1;
        private readonly Dictionary<string, ActionData> _temporaryActions = new Dictionary<string, ActionData>();


        /// <summary>
        /// A list of all entity IDs participating in the combat.
        /// </summary>
        public List<int> Combatants { get; private set; }
        public int CurrentTurnEntityId => _currentTurnIndex >= 0 && _currentTurnIndex < _initiativeOrder.Count ? _initiativeOrder[_currentTurnIndex] : -1;


        // UI and Input references for states to access
        public ActionHandUI ActionHandUI { get; private set; }
        public CombatInputHandler InputHandler { get; private set; }
        public CombatScene Scene { get; private set; }

        /// <summary>
        /// Initializes a new instance of the CombatManager class.
        /// </summary>
        public CombatManager()
        {
            _actionManager = ServiceLocator.Get<ActionManager>();
            _gameState = ServiceLocator.Get<GameState>();
            Combatants = new List<int>();
            _fsm = new CombatFSM();
            ServiceLocator.Register<CombatManager>(this); // Register self for event handlers
        }

        /// <summary>
        /// Registers the core UI and input components with the manager so states can access them.
        /// </summary>
        public void RegisterComponents(ActionHandUI handUI, CombatInputHandler inputHandler, CombatScene scene)
        {
            ActionHandUI = handUI;
            InputHandler = inputHandler;
            Scene = scene;
        }

        /// <summary>
        /// Starts a new combat encounter with a given set of participants.
        /// </summary>
        /// <param name="combatants">A list of entity IDs for all combatants.</param>
        public void StartCombat(List<int> combatants)
        {
            Combatants.Clear();
            Combatants.AddRange(combatants);
            ClearActionsForTurn();
            _currentTurnIndex = 0;
            _fsm.ChangeState(new CombatStartState(), this);
        }

        /// <summary>
        /// Sets the fixed order for action selection.
        /// </summary>
        public void SetInitiativeOrder(List<int> order)
        {
            _initiativeOrder = order;
            _currentTurnIndex = 0;
        }

        /// <summary>
        /// Advances the turn to the next combatant in the selection order.
        /// </summary>
        public void AdvanceTurn()
        {
            _currentTurnIndex++;
        }

        /// <summary>
        /// Checks if the selection turn index has reset, indicating a new round.
        /// </summary>
        public bool IsNewRound()
        {
            if (_currentTurnIndex >= _initiativeOrder.Count)
            {
                _currentTurnIndex = 0;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Creates a CombatAction from player input, adds it to the list for the current turn,
        /// triggers the card play animation, and notifies the current state.
        /// </summary>
        public void AddPlayerAction(string actionId, List<int> targetIds)
        {
            Debug.WriteLine($"    > Player action '{actionId}' confirmed for targets [{string.Join(", ", targetIds)}].");
            var actionData = _actionManager.GetAction(actionId);
            if (actionData == null) return;

            var playerAction = new CombatAction(_gameState.PlayerEntityId, actionData, targetIds);
            AddActionForTurn(playerAction);

            EventBus.Publish(new GameEvents.PlayerActionConfirmed { CardActionData = actionData, TargetEntityIds = targetIds });

            // Notify the current state that the player has made their choice.
            if (FSM.CurrentState is ActionSelectionState actionSelectionState)
            {
                actionSelectionState.OnPlayerActionConfirmed();
            }
        }

        /// <summary>
        /// Adds a pre-constructed action to the list for the current turn.
        /// </summary>
        public void AddActionForTurn(CombatAction action)
        {
            _actionsForTurn.Add(action);
        }

        /// <summary>
        /// Overwrites the current list of actions with a new, sorted list after speed rolls.
        /// </summary>
        public void SetResolvedActionsForTurn(List<CombatAction> sortedActions)
        {
            _actionsForTurn = sortedActions;
        }

        /// <summary>
        /// Gets the list of actions queued for the current turn.
        /// </summary>
        public IReadOnlyList<CombatAction> GetActionsForTurn()
        {
            return _actionsForTurn;
        }

        /// <summary>
        /// Clears all actions queued for the current turn.
        /// </summary>
        public void ClearActionsForTurn()
        {
            _actionsForTurn.Clear();
        }

        /// <summary>
        /// Caches a dynamically generated action for the duration of the current turn.
        /// </summary>
        public void AddTemporaryAction(ActionData actionData)
        {
            if (actionData == null || string.IsNullOrEmpty(actionData.Id)) return;
            _temporaryActions[actionData.Id] = actionData;
        }

        /// <summary>
        /// Retrieves a temporary action from the cache.
        /// </summary>
        public ActionData GetTemporaryAction(string id)
        {
            _temporaryActions.TryGetValue(id, out var action);
            return action;
        }

        /// <summary>
        /// Clears all temporary actions from the cache. Should be called at the end of a turn.
        /// </summary>
        public void ClearTemporaryActions()
        {
            _temporaryActions.Clear();
        }

        /// <summary>
        /// Updates the combat state machine.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            _fsm?.Update(gameTime, this);
        }
    }
}
