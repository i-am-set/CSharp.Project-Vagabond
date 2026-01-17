using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProjectVagabond.Battle.Abilities
{
    public class CombatContext
    {
        public BattleCombatant Actor { get; set; }
        public BattleCombatant Target { get; set; }
        public MoveData Move { get; set; }

        // Context Data
        public float BaseDamage { get; set; }
        public bool IsCritical { get; set; }
        public bool IsGraze { get; set; }
        public float MultiTargetModifier { get; set; } = 1.0f;
        public bool IsLastAction { get; set; } = false;

        /// <summary>
        /// Accumulates lifesteal percentages from multiple sources (Relics, Moves) 
        /// to be applied as a single healing event.
        /// </summary>
        public float AccumulatedLifestealPercent { get; set; } = 0f;

        /// <summary>
        /// If true, this calculation is for UI display or AI evaluation, not actual execution.
        /// Abilities should NOT trigger side effects (events, animations, logs) when this is true.
        /// </summary>
        public bool IsSimulation { get; set; } = false;

        public bool MoveHasTag(string tag)
        {
            return Move != null && Move.Tags.Contains(tag);
        }

        public bool MoveHasElement(int elementId)
        {
            return Move != null && Move.OffensiveElementIDs.Contains(elementId);
        }
    }
}
