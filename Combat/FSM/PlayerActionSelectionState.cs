using Microsoft.Xna.Framework;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The state where the player selects their actions for the turn.
    /// </summary>
    public class PlayerActionSelectionState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            // The ActionHandUI will automatically become interactive during Update
            // because the FSM is in this state.
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            // Delegate updates to the input handler and UI, which are only active in this state.
            combatManager.InputHandler?.Update(gameTime);
            combatManager.ActionHandUI?.Update(gameTime, combatManager.InputHandler, combatManager);
        }
    }
}