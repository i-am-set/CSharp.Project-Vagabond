﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Resolves the turn order for combat actions based on priority and the rolled turn speed.
    /// </summary>
    public static class TurnResolver
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// Sorts a list of CombatAction objects to determine the execution order for a turn.
        /// The sorting is based on a three-tiered system:
        /// 1. Primary: ActionData.Priority (descending - higher priority executes first).
        /// 2. Secondary: Rolled TurnSpeed (descending - higher speed executes first).
        /// 3. Tertiary: A random roll to break any remaining ties.
        /// </summary>
        /// <param name="actions">A list of CombatAction objects representing all actions to be resolved this turn.</param>
        /// <returns>A new list of CombatAction objects sorted in execution order.</returns>
        public static List<CombatAction> ResolveTurnOrder(List<CombatAction> actions)
        {
            // Using LINQ's OrderByDescending and ThenBy for a clean, efficient sort.
            return actions
                .OrderByDescending(action => action.ActionData.Priority)
                .ThenByDescending(action => action.TurnSpeed)
                .ThenBy(action => _random.Next()) // Randomly sort any ties.
                .ToList();
        }
    }
}
