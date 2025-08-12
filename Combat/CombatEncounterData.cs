using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Defines a single enemy to be spawned in a combat encounter.
    /// </summary>
    public class EnemySpawnDefinition
    {
        [JsonPropertyName("archetypeId")]
        public string ArchetypeId { get; set; }

        [JsonPropertyName("position")]
        public Point Position { get; set; }

        /// <summary>
        /// Defines stat randomization rules. Key is a single letter (s,a,t,i,c), value is the variance.
        /// E.g., { "s": 2 } means Strength will be modified by a random value between -2 and +2.
        /// </summary>
        [JsonPropertyName("statVariances")]
        public Dictionary<string, int> StatVariances { get; set; }
    }

    /// <summary>
    /// Represents the data blueprint for a complete combat encounter, loaded from JSON.
    /// </summary>
    public class CombatEncounterData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("enemies")]
        public List<EnemySpawnDefinition> Enemies { get; set; } = new List<EnemySpawnDefinition>();
    }
}