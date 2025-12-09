using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond.Battle.Abilities
{
    /// <summary>
    /// A container for all data relevant to a specific combat interaction.
    /// Passed through ability hooks so they can make decisions.
    /// </summary>
    public class CombatContext
    {
        public BattleCombatant Actor { get; set; }
        public BattleCombatant Target { get; set; }
        public MoveData Move { get; set; }
        public ConsumableItemData Item { get; set; }

        // For damage calculation context
        public float BaseDamage { get; set; }
        public bool IsCritical { get; set; }
        public float MultiTargetModifier { get; set; } = 1.0f;

        // Helper to check tags easily
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