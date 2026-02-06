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
        public List<List<string>> PossibleMajorBattles { get; set; } = new List<List<string>>();
    }
}