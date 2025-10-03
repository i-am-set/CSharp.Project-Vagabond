using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Progression
{
    public class SplitData
    {
        public string Theme { get; set; } = "";
        public int SplitLengthMin { get; set; }
        public int SplitLengthMax { get; set; }
        public int NumberOfRewardFloors { get; set; }
        public List<List<string>> PossibleBattles { get; set; } = new List<List<string>>();
        public List<NarrativeEvent> PossibleNarratives { get; set; } = new List<NarrativeEvent>();
        public List<List<string>> PossibleMajorBattles { get; set; } = new List<List<string>>();
    }

    public class NarrativeEvent
    {
        public string Prompt { get; set; } = "";
        public List<NarrativeChoice> Choices { get; set; } = new List<NarrativeChoice>();
    }

    public class NarrativeChoice
    {
        public string Text { get; set; } = "";
        public NarrativeOutcome? Outcome { get; set; }
        public string ResultText { get; set; } = "";
    }

    public class NarrativeOutcome
    {
        public string OutcomeType { get; set; } = "";
        public string Value { get; set; } = "";
        public int Duration { get; set; }
    }
}