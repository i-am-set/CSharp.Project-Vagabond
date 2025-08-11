using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// The state where the AI determines its action for the turn.
    /// </summary>
    public class AIActionSelectionState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            // This state's logic is immediate. It gathers all AI actions and transitions.
            var actionManager = ServiceLocator.Get<ActionManager>();
            var gameState = ServiceLocator.Get<GameState>();

            var allCombatants = combatManager.Combatants;
            var aiCombatants = allCombatants.Where(id => id != gameState.PlayerEntityId).ToList();

            foreach (var aiId in aiCombatants)
            {
                // 1. Choose an action (simple logic for now)
                // For this prototype, let's assume the AI always tries to cast Fireball.
                var actionData = actionManager.GetAction("spell_fireball");
                if (actionData == null)
                {
                    // Fallback if fireball doesn't exist
                    actionData = actionManager.GetAllActions().FirstOrDefault(a => a.TargetType == TargetType.SingleEnemy);
                    if (actionData == null) continue; // No suitable actions found for this AI
                }

                // 2. Choose a target (simple logic for now)
                var targetIds = new List<int>();
                if (actionData.TargetType == TargetType.SingleEnemy)
                {
                    // For now, AI always targets the player.
                    targetIds.Add(gameState.PlayerEntityId);
                }
                else if (actionData.TargetType == TargetType.Self)
                {
                    // No target needed.
                }
                // Other target types can be added later.

                // 3. Create the CombatAction
                // In a full game, AI speed would come from a stats component.
                const float aiSpeed = 5f;
                var aiAction = new CombatAction(aiId, actionData, aiSpeed, targetIds);

                // 4. Add the action to the manager's list
                combatManager.AddActionForTurn(aiAction);
            }

            // 5. After all AI have decided, transition to the execution state.
            combatManager.FSM.ChangeState(new ActionExecutionState(), combatManager);
        }

        public void OnExit(CombatManager combatManager)
        {
        }

        public void Update(GameTime gameTime, CombatManager combatManager)
        {
            // All logic is handled in OnEnter for this state.
        }
    }
}