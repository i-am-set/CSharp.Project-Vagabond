using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Encounters
{
    /// <summary>
    /// Represents a condition that must be met for a random encounter to be eligible to trigger.
    /// </summary>
    public class EncounterTriggerCondition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } // e.g., "TerrainHeight"

        [JsonPropertyName("comparison")]
        public string Comparison { get; set; } // e.g., "GreaterThan", "LessThan", "EqualTo"

        [JsonPropertyName("value")]
        public float Value { get; set; }
    }

    /// <summary>
    /// Represents the root data structure for an encounter, loaded from JSON.
    /// </summary>
    public class EncounterData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("image")]
        public string Image { get; set; }

        [JsonPropertyName("descriptionText")]
        public string DescriptionText { get; set; }

        [JsonPropertyName("choices")]
        public List<EncounterChoiceData> Choices { get; set; } = new List<EncounterChoiceData>();

        [JsonPropertyName("isRandom")]
        public bool IsRandom { get; set; } = false;

        [JsonPropertyName("triggerConditions")]
        public List<EncounterTriggerCondition> TriggerConditions { get; set; } = new List<EncounterTriggerCondition>();
    }

    /// <summary>
    /// Represents a single choice a player can make within an encounter.
    /// </summary>
    public class EncounterChoiceData
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("requirements")]
        public List<EncounterRequirementData> Requirements { get; set; } = new List<EncounterRequirementData>();

        [JsonPropertyName("outcomes")]
        public List<EncounterOutcomeData> Outcomes { get; set; } = new List<EncounterOutcomeData>();

        [JsonPropertyName("successOutcomes")]
        public List<EncounterOutcomeData> SuccessOutcomes { get; set; } = new List<EncounterOutcomeData>();

        [JsonPropertyName("failureOutcomes")]
        public List<EncounterOutcomeData> FailureOutcomes { get; set; } = new List<EncounterOutcomeData>();
    }

    /// <summary>
    /// Defines a condition that must be met for a choice to be available.
    /// </summary>
    public class EncounterRequirementData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    /// <summary>
    /// Defines a single consequence of selecting a choice.
    /// </summary>
    public class EncounterOutcomeData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }
}