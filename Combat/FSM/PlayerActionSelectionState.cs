using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The state where the player selects their actions for the turn.
    /// </summary>
    public class PlayerActionSelectionState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("    ... Waiting for player input...");
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