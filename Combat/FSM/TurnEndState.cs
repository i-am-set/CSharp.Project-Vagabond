using Microsoft.Xna.Framework;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// Handles end-of-turn effects and checks for victory or defeat conditions.
    /// </summary>
    public class TurnEndState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            // 1. Check for win/loss conditions
            // TODO: Implement logic to check if all enemies or the player are defeated.
            bool isCombatOver = false; // Placeholder

            if (isCombatOver)
            {
                combatManager.FSM.ChangeState(new CombatEndState(), combatManager);
            }
            else
            {
                // 2. Clear the actions from the completed turn.
                combatManager.ClearActionsForTurn();

                // 3. Reset the UI for the next turn.
                var actionManager = ServiceLocator.Get<ActionManager>();
                var allPlayerActions = actionManager.GetAllActions().Where(a => a.Id != "action_pass");
                combatManager.ActionHandUI.SetActions(allPlayerActions);
                combatManager.ActionHandUI.EnterScene(); // Resets the hand's animation state to slide in.

                // 4. Transition back to the start of the next turn.
                combatManager.FSM.ChangeState(new TurnStartState(), combatManager);
            }
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
        }
    }
}