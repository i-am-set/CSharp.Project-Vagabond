﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Combat.FSM
{
    /// <summary>
    /// A state dedicated to rolling for turn speed for all queued actions
    /// and determining the final execution order for the round.
    /// </summary>
    public class TurnSpeedRollState : ICombatState
    {
        public void OnEnter(CombatManager combatManager)
        {
            Debug.WriteLine("--- PHASE: TURN SPEED ROLL ---");

            var actions = combatManager.GetActionsForTurn();
            var componentStore = ServiceLocator.Get<ComponentStore>();
            var random = new Random();

            // Calculate Turn Speed for each action.
            foreach (var action in actions)
            {
                var stats = componentStore.GetComponent<StatsComponent>(action.CasterEntityId);
                if (stats == null)
                {
                    action.TurnSpeed = 0;
                    continue;
                }

                int agilityMod = stats.GetStatModifier(StatType.Agility);
                int d20Roll = random.Next(1, 21);
                action.TurnSpeed = d20Roll + agilityMod;

                string entityName = EntityNamer.GetName(action.CasterEntityId);
                Debug.WriteLine($"  > {entityName} ({action.ActionData.Name}): Roll {d20Roll} + AgiMod {agilityMod} = Speed {action.TurnSpeed}");
            }

            // Resolve the final order and update the combat manager's list.
            var resolvedOrder = TurnResolver.ResolveTurnOrder(new List<CombatAction>(actions));
            combatManager.SetResolvedActionsForTurn(resolvedOrder);

            Debug.WriteLine("  > Final Execution Order:");
            for (int i = 0; i < resolvedOrder.Count; i++)
            {
                var action = resolvedOrder[i];
                string entityName = EntityNamer.GetName(action.CasterEntityId);
                string targetName = GetTargetString(action);
                Debug.WriteLine($"    {i + 1}. {entityName} -> {action.ActionData.Name} on {targetName} (Priority: {action.ActionData.Priority}, Speed: {action.TurnSpeed})");
            }

            Debug.WriteLine("--- END PHASE: TURN SPEED ROLL ---\n");
            combatManager.FSM.ChangeState(new ActionExecutionState(), combatManager);
        }

        private string GetTargetString(CombatAction action)
        {
            var gameState = ServiceLocator.Get<GameState>();
            switch (action.ActionData.TargetType)
            {
                case TargetType.Self:
                    return "Self";
                case TargetType.AllEnemies:
                    return "All Enemies";
                case TargetType.SingleEnemy:
                    if (action.TargetEntityIds.Any())
                    {
                        return EntityNamer.GetName(action.TargetEntityIds.First());
                    }
                    return "Unknown Target";
                default:
                    return "Target";
            }
        }

        public void OnExit(CombatManager combatManager) { }
        public void Update(GameTime gameTime, CombatManager combatManager) { }
    }
}
