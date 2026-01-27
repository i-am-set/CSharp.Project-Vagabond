using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

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

        public string DefaultStrikeMoveID { get; set; }

        public List<string> Slot1MovePool { get; set; } = new List<string>();
        public List<string> Slot2MovePool { get; set; } = new List<string>();
        public List<string> Slot3MovePool { get; set; } = new List<string>();
        public List<string> Slot4MovePool { get; set; } = new List<string>();

        public List<Dictionary<string, string>> PassiveAbilityPool { get; set; } = new List<Dictionary<string, string>>();
    }
}