using ProjectVagabond.Battle;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// An ECS component that stores the base stats and combat-related data for an entity.
    /// </summary>
    public class CombatantStatsComponent : IComponent, ICloneableComponent
    {
        public int Level { get; set; }
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int MaxMana { get; set; }
        public int CurrentMana { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }
        public List<int> DefensiveElementIDs { get; set; } = new List<int>();
        public List<string> AvailableMoveIDs { get; set; } = new List<string>();

        public IComponent Clone()
        {
            var clone = (CombatantStatsComponent)this.MemberwiseClone();
            clone.DefensiveElementIDs = new List<int>(this.DefensiveElementIDs);
            clone.AvailableMoveIDs = new List<string>(this.AvailableMoveIDs);
            return clone;
        }
    }
}
