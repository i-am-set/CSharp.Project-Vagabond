using Microsoft.Xna.Framework;
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

        private readonly List<CombatAction> _actionsForTurn = new List<CombatAction>();

        /// <summary>
        /// A list of all entity IDs participating in the combat.
        /// </summary>
        public List<int> Combatants { get; private set; }

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
            _fsm.ChangeState(new CombatStartState(), this);
        }

        /// <summary>
        /// Creates a CombatAction from player input, adds it to the list for the current turn,
        /// triggers the card play animation, and transitions the FSM to wait for that animation.
        /// </summary>
        /// <param name="actionId">The ID of the action being played.</param>
        /// <param name="targetIds">A list of entity IDs being targeted.</param>
        public void AddPlayerAction(string actionId, List<int> targetIds)
        {
            var actionData = _actionManager.GetAction(actionId);
            if (actionData == null) return;

            // In a full game, player speed would come from a stats component.
            const float playerSpeed = 10f;

            var playerAction = new CombatAction(_gameState.PlayerEntityId, actionData, playerSpeed, targetIds);
            AddActionForTurn(playerAction);

            // Publish the event that tells the UI to animate the card being played.
            EventBus.Publish(new GameEvents.PlayerActionConfirmed { CardActionData = actionData, TargetEntityIds = targetIds });

            // Transition to a state that waits for the card animation to finish.
            _fsm.ChangeState(new PlayerActionConfirmedState(), this);
        }

        /// <summary>
        /// Adds a pre-constructed action to the list for the current turn.
        /// </summary>
        public void AddActionForTurn(CombatAction action)
        {
            _actionsForTurn.Add(action);
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
        /// Updates the combat state machine.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            _fsm?.Update(gameTime, this);
        }
    }
}