using ProjectVagabond.Combat.Effects;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Combat
{
    /// <summary>
    /// Defines the targeting behavior of a combat action.
    /// </summary>
    public enum TargetType
    {
        /// <summary>
        /// Targets a single enemy.
        /// </summary>
        SingleEnemy,
        /// <summary>
        /// Targets the caster.
        /// </summary>
        Self,
        /// <summary>
        /// Targets all enemies.
        /// </summary>
        AllEnemies
    }

    /// <summary>
    /// Represents a single spell or action that can be performed in combat.
    /// This class is designed to be deserialized from a JSON file.
    /// </summary>
    public class ActionData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 0;

        [JsonPropertyName("targetType")]
        public TargetType TargetType { get; set; } = TargetType.SingleEnemy;

        [JsonPropertyName("effects")]
        public List<EffectDefinition> Effects { get; set; } = new List<EffectDefinition>();

        [JsonPropertyName("timeline")]
        public AnimationTimeline Timeline { get; set; }
    }
}