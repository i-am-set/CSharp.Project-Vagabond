using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public class EnemyData
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public Gender Gender { get; set; } = Gender.Thing;
        public bool IsProperNoun { get; set; } = false;

        public int MinHP { get; set; }
        public int MaxHP { get; set; }

        public int MinStrength { get; set; }
        public int MaxStrength { get; set; }

        public int MinIntelligence { get; set; }
        public int MaxIntelligence { get; set; }

        public int MinTenacity { get; set; }
        public int MaxTenacity { get; set; }

        public int MinAgility { get; set; }
        public int MaxAgility { get; set; }

        public int? MaxGuard { get; set; }

        public List<string> BasicMoves { get; set; } = new List<string>();
        public List<string> CoreMoves { get; set; } = new List<string>();
        public List<string> AltMoves { get; set; } = new List<string>();
    }
}