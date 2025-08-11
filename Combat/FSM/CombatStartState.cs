using Microsoft.Xna.Framework;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The initial state of combat. Handles setup and transitions to the first turn.
    /// </summary>
    public class CombatStartState : ICombatState
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