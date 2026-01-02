using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public class PartyMemberData
    {
        public string MemberID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        // Narration Data
        public Gender Gender { get; set; } = Gender.Neutral;
        public bool IsProperNoun { get; set; } = true; // Default to true for named characters

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
        /// <summary>
        /// A pool of possible moves (Spells OR Actions) this character can start with.
        /// </summary>
        public List<string> StartingMoves { get; set; } = new List<string>();

        /// <summary>
        /// How many moves from the StartingMoves pool to randomly assign to the 4 combat slots. Max 4.
        /// </summary>
        public int NumberOfStartingMoves { get; set; } = 4;

        // Split equipment to avoid ID collisions (since Weapon "0" and Armor "0" are different items)
        public Dictionary<string, int> StartingWeapons { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> StartingArmor { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> StartingRelics { get; set; } = new Dictionary<string, int>();
    }
}
