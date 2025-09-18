using System;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents the static data for a single passive ability.
    /// </summary>
    public class AbilityData
    {
        public string AbilityID { get; set; }
        public string AbilityName { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Effects { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}