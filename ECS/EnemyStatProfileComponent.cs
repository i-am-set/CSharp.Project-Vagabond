using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// An ECS component that defines the statistical profile for an AI combatant archetype.
    /// This data is used by the Spawner to randomly generate a CombatantStatsComponent at runtime.
    /// </summary>
    public class EnemyStatProfileComponent : IComponent, ICloneableComponent
    {
        public int Level { get; set; }

        // Narration Data
        public Gender Gender { get; set; } = Gender.Thing; // Default to "It" for monsters
        public bool IsProperNoun { get; set; } = false; // Default to false for generic enemies (e.g. "The Spider")

        public int MinHP { get; set; }
        public int MaxHP { get; set; }
        public int MaxMana { get; set; } = 100;
        public int MinStrength { get; set; }
        public int MaxStrength { get; set; }
        public int MinIntelligence { get; set; }
        public int MaxIntelligence { get; set; }
        public int MinTenacity { get; set; }
        public int MaxTenacity { get; set; }
        public int MinAgility { get; set; }
        public int MaxAgility { get; set; }

        public List<int> WeaknessElementIDs { get; set; } = new List<int>();
        public List<int> ResistanceElementIDs { get; set; } = new List<int>();

        public List<string> MoveLearnset { get; set; } = new List<string>();
        public int MinNumberOfMoves { get; set; }
        public int MaxNumberOfMoves { get; set; }

        public IComponent Clone()
        {
            var clone = (EnemyStatProfileComponent)this.MemberwiseClone();
            clone.WeaknessElementIDs = new List<int>(this.WeaknessElementIDs);
            clone.ResistanceElementIDs = new List<int>(this.ResistanceElementIDs);
            clone.MoveLearnset = new List<string>(this.MoveLearnset);
            return clone;
        }
    }
}
