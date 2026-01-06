using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents the static data for an Armor item.
    /// Armor always provides stat modifiers and may optionally provide passive effects.
    /// </summary>
    public class ArmorData
    {
        public string ArmorID { get; set; }
        public string ArmorName { get; set; }
        /// <summary>
        /// The name of the passive ability granted by this armor. Can be null if no effects are present.
        /// </summary>
        public string? AbilityName { get; set; }

        public string Description { get; set; }
        public int Rarity { get; set; } = 0;

        /// <summary>
        /// Passive effects granted while equipped (e.g., "DamageReductionPhysical: 10").
        /// </summary>
        public Dictionary<string, string> Effects { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Stat modifiers applied while equipped. Armor MUST have at least one modifier.
        /// </summary>
        public Dictionary<string, int> StatModifiers { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public List<string> Tags { get; set; } = new List<string>();
    }
}