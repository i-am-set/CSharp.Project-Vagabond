using Microsoft.Xna.Framework;
using ProjectVagabond.Combat.FSM;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// Handles start-of-turn effects and determines the next actor.
    /// </summary>
    public class TurnStartState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            // Future: Apply start-of-turn effects like poison, regeneration, etc.
            // For now, this state is just a transition point.

            // Immediately transition to the state where the player can select their action.
            combatManager.FSM.ChangeState(new PlayerActionSelectionState(), combatManager);
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
        }
    }
}
