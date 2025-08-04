using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Resolves the turn order for combat actions based on priority and effective cast speed.
    /// </summary>
    public static class TurnResolver
    {
        /// <summary>
        /// Sorts a list of CombatAction objects to determine the execution order for a turn.
        /// The sorting is based on a two-tiered system:
        /// 1. Primary: ActionData.Priority (descending - higher priority executes first).
        /// 2. Secondary: EffectiveCastSpeed (descending - faster actions break ties).
        /// </summary>
        /// <param name="actions">A list of CombatAction objects representing all actions to be resolved this turn.</param>
        /// <returns>A new list of CombatAction objects sorted in execution order.</returns>
        public static List<CombatAction> ResolveTurnOrder(List<CombatAction> actions)
        {
            // Using LINQ's OrderByDescending and ThenByDescending for a clean, efficient sort.
            return actions
                .OrderByDescending(action => action.ActionData.Priority)
                .ThenByDescending(action => action.EffectiveCastSpeed)
                .ToList();
        }
    }
}