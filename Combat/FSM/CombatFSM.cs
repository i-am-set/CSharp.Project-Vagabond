using Microsoft.Xna.Framework;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// Manages the state transitions and updates for the combat system.
    /// </summary>
    public class CombatFSM
    {
        private ICombatState _currentState;
        public ICombatState CurrentState => _currentState;

        /// <summary>
        /// Transitions to a new state, calling the appropriate exit and enter methods.
        /// </summary>
        /// <param name="newState">The new state to transition to.</param>
        /// <param name="combatManager">The combat manager instance.</param>
        public void ChangeState(ICombatState newState, CombatManager combatManager)
        {
            _currentState?.OnExit(combatManager);
            _currentState = newState;
            _currentState?.OnEnter(combatManager);
        }

        /// <summary>
        /// Updates the currently active state.
        /// </summary>
        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            _currentState?.Update(gameTime, combatManager);
        }
    }
}