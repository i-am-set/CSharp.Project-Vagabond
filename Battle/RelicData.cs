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
        public string Description { get; set; }
        public int Rarity { get; set; } = 0;
        public int LevelRequirement { get; set; } = 0;
        public Dictionary<string, string> Effects { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> Tags { get; set; } = new List<string>();
    }
}
