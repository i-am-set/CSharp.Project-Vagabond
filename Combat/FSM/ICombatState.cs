using Microsoft.Xna.Framework;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// Defines the contract for a state in the combat Finite State Machine.
    /// </summary>
    public interface ICombatState
    {
        /// <summary>
        /// Called when the FSM enters this state.
        /// </summary>
        void OnEnter(CombatManager combatManager);

        /// <summary>
        /// Called when the FSM exits this state.
        /// </summary>
        void OnExit(CombatManager combatManager);

        /// <summary>
        /// Called every frame while this state is active.
        /// </summary>
        void Update(GameTime gameTime, CombatManager combatManager);
    }
}