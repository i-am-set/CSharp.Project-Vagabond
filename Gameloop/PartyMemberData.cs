using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public class PartyMemberData
    {
        public string MemberID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        // Base Stats
        public int MaxHP { get; set; }
        public int MaxMana { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public List<int> DefensiveElementIDs { get; set; } = new List<int>();
        public string DefaultStrikeMoveID { get; set; }

        // Starting Loadout
        public List<string> StartingSpells { get; set; } = new List<string>();
        public List<string> StartingActions { get; set; } = new List<string>();
        public Dictionary<string, int> StartingEquipment { get; set; } = new Dictionary<string, int>(); // "WeaponID": 1
    }
}