using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond.Battle.Abilities
{
    public class CombatContext
    {
        public BattleCombatant Actor { get; set; }
        public BattleCombatant Target { get; set; }
        public MoveData Move { get; set; }
        public ConsumableItemData Item { get; set; }

        // Context Data
        public float BaseDamage { get; set; }
        public bool IsCritical { get; set; }
        public bool IsGraze { get; set; } 
        public float MultiTargetModifier { get; set; } = 1.0f;
        public bool IsLastAction { get; set; } = false;

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