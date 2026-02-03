using System.Collections.Generic;

namespace ProjectVagabond.Battle
{
    public class PartyMemberData
    {
        public string MemberID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public Gender Gender { get; set; } = Gender.Neutral;
        public bool IsProperNoun { get; set; } = true;

        public int MaxHP { get; set; }
        public int MaxMana { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public string BasicMoveId { get; set; }
        public string CoreMoveId { get; set; }
        public string AltMoveId { get; set; }

        public List<Dictionary<string, string>> PassiveAbilityPool { get; set; } = new List<Dictionary<string, string>>();
    }
}