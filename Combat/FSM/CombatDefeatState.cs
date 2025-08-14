using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// A terminal state reached when the player is defeated.
    /// It displays a message and halts further combat progression.
    /// </summary>
    public class CombatDefeatState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("--- PLAYER DEFEAT ---");
            Debug.WriteLine("  ... Game Over. Halting combat.");
            // In a full game, this would likely trigger a UI screen and options to load/quit.
            // For now, it just stops the FSM from updating.
        }

        public void OnExit(CombatManager combatManager)
        {
            // This state should not be exited under normal circumstances.
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            // Do nothing. This effectively freezes the combat in a defeat state.
        }
    }
}