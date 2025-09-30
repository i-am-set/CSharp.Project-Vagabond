using System.Collections.Generic;
using System.Text.Json.Serialization;
using ProjectVagabond.Battle;

namespace ProjectVagabond.Utils
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SplitTheme
    {
        Forest,
        City,
        Cave
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RewardType
    {
        Spell,
        Ability,
        Item
    }

    /// <summary>
    /// Represents the data for a single themed gauntlet ("Split").
    /// Defines the structure and content pools for a randomized progression sequence.
    /// </summary>
    public class SplitData
    {
        public SplitTheme Theme { get; set; }
        public List<string> Structure { get; set; } = new List<string>();
        public List<List<string>> PossibleBattles { get; set; } = new List<List<string>>();
        public List<NarrativeEventData> PossibleNarratives { get; set; } = new List<NarrativeEventData>();
        public List<List<string>> PossibleMajorBattles { get; set; } = new List<List<string>>();
    }

    /// <summary>
    /// Represents the content for a single narrative event.
    /// </summary>
    public class NarrativeEventData
    {
        public string Prompt { get; set; }
        public List<NarrativeChoice> Choices { get; set; } = new List<NarrativeChoice>();
    }

    /// <summary>
    /// A single choice within a narrative event.
    /// </summary>
    public class NarrativeChoice
    {
        public string Text { get; set; }
        public ChoiceOutcome Outcome { get; set; }
    }

    /// <summary>
    /// The result of making a narrative choice.
    /// </summary>
    public class ChoiceOutcome
    {
        public string OutcomeType { get; set; } // e.g., "GiveItem", "AddBuff"
        public string Value { get; set; }       // e.g., "HealthPotion", "StrengthUp"
        public int Duration { get; set; } = 1;  // e.g., Duration in battles for a buff
    }
}