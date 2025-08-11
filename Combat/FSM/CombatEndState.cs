using Microsoft.Xna.Framework;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The final state of combat. Handles rewards, cleanup, and transitioning out of the scene.
    /// </summary>
    public class CombatEndState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
        }
    }
}