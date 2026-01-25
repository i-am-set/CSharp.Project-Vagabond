using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Progression
{
    public class SplitData
    {
        public string Theme { get; set; } = "";
        public int SplitLengthMin { get; set; }
        public int SplitLengthMax { get; set; }
        public List<List<string>> PossibleBattles { get; set; } = new List<List<string>>();
        public List<string> PossibleNarrativeEventIDs { get; set; } = new List<string>();
        public List<List<string>> PossibleMajorBattles { get; set; } = new List<List<string>>();
    }

    public class NarrativeEvent
    {
        public string EventID { get; set; } = "";
        public string Prompt { get; set; } = "";
        public List<NarrativeChoice> Choices { get; set; } = new List<NarrativeChoice>();
    }

    public class NarrativeChoice
    {
        public string Text { get; set; } = "";
        public string? Requirement { get; set; } // e.g. "Gold:50" or "HP:20"
        public List<WeightedOutcome> Outcomes { get; set; } = new List<WeightedOutcome>();
    }

    public class WeightedOutcome
    {
        public int Weight { get; set; } = 1;
        public List<int>? DifficultyClass { get; set; } // e.g. [4, 5, 6] for success
        public List<NarrativeOutcome> Outcomes { get; set; } = new List<NarrativeOutcome>();
        public string ResultText { get; set; } = "";
    }

    public class NarrativeOutcome
    {
        public string OutcomeType { get; set; } = ""; // "GiveItem", "ModifyStat", "Damage", "Heal", "Gold"
        public string Value { get; set; } = "";
        public int Amount { get; set; }
    }
}