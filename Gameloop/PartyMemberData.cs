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

        public List<int> WeaknessElementIDs { get; set; } = new List<int>();
        public List<int> ResistanceElementIDs { get; set; } = new List<int>();

        public string DefaultStrikeMoveID { get; set; }

        // Starting Loadout
        public List<string> StartingSpells { get; set; } = new List<string>();
        public List<string> StartingActions { get; set; } = new List<string>();

        // Split equipment to avoid ID collisions (since Weapon "0" and Armor "0" are different items)
        public Dictionary<string, int> StartingWeapons { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> StartingArmor { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> StartingRelics { get; set; } = new Dictionary<string, int>();
    }
}