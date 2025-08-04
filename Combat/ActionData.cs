using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Combat
{
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

        [JsonPropertyName("sprites")]
        public ActionSpriteData Sprites { get; set; }

        [JsonPropertyName("combinations")]
        public List<SynergyData> Combinations { get; set; } = new List<SynergyData>();
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

    /// <summary>
    /// Defines a synergy rule, specifying what happens when this action is paired with another.
    /// </summary>
    public class SynergyData
    {
        [JsonPropertyName("pairedWith")]
        public string PairedWith { get; set; }

        [JsonPropertyName("combinesToBecome")]
        public string CombinesToBecome { get; set; }
    }
}