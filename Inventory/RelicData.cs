using ProjectVagabond.Battle;
using ProjectVagabond.Items;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents the static data for a single Relic item which grants a passive ability.
    /// </summary>
    public class RelicData
    {
        public string RelicID { get; set; }
        public string RelicName { get; set; }
        public string AbilityName { get; set; }

        /// <summary>
        /// Practical information about the item's effect.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Lore or visual description of the item.
        /// </summary>
        public string Flavor { get; set; }

        public int Rarity { get; set; } = 0;
        public Dictionary<string, string> Effects { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Simple integer modifiers for stats (e.g., "Strength": 2, "Agility": -1).
        /// Keys should match the stat names (Strength, Intelligence, Tenacity, Agility, MaxHP, MaxMana).
        /// </summary>
        public Dictionary<string, int> StatModifiers { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public List<string> Tags { get; set; } = new List<string>();
    }
}
