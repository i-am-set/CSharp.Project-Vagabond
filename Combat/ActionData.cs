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

        [JsonPropertyName("sprites")]
        public ActionSpriteData Sprites { get; set; }
    }

    /// <summary>
    /// Contains the file paths for the different animation states of a hand performing an action.
    /// </summary>
    public class ActionSpriteData
    {
        [JsonPropertyName("hold")]
        public string Hold { get; set; }

        [JsonPropertyName("cast")]
        public string Cast { get; set; }

        [JsonPropertyName("release")]
        public string Release { get; set; }
    }
}