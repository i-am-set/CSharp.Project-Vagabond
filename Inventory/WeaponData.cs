using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents the static data for a Weapon.
    /// Weapons provide a specific MoveID for the "Strike" command, 
    /// and can also provide passive effects and stat modifiers like Relics.
    /// </summary>
    public class WeaponData
    {
        public string WeaponID { get; set; }
        public string WeaponName { get; set; }
        public string Description { get; set; }
        public int Rarity { get; set; } = 0;
        public int LevelRequirement { get; set; } = 0;

        /// <summary>
        /// The ID of the Move (Action) this weapon performs when the player uses "Strike".
        /// </summary>
        public string MoveID { get; set; }

        /// <summary>
        /// Passive effects granted while equipped (e.g., "Lifesteal: 10").
        /// </summary>
        public Dictionary<string, string> Effects { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Stat modifiers applied while equipped (e.g., "Strength": 5).
        /// </summary>
        public Dictionary<string, int> StatModifiers { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public List<string> Tags { get; set; } = new List<string>();
    }
}