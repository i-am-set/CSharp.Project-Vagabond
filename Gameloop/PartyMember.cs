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

namespace ProjectVagabond
{
    public class PartyMember
    {
        public string Name { get; set; }
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int MaxMana { get; set; }
        public int CurrentMana { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public string DefaultStrikeMoveID { get; set; }
        public int PortraitIndex { get; set; } = 0;

        public Dictionary<string, string> IntrinsicAbilities { get; set; } = new Dictionary<string, string>();

        public MoveEntry?[] Spells { get; set; } = new MoveEntry?[4];
        public List<MoveEntry> Actions { get; set; } = new List<MoveEntry>();

        public PartyMember() { }

        public PartyMember Clone()
        {
            var clone = (PartyMember)this.MemberwiseClone();
            clone.IntrinsicAbilities = new Dictionary<string, string>(this.IntrinsicAbilities);
            clone.Spells = new MoveEntry?[4];
            for (int i = 0; i < 4; i++)
            {
                if (this.Spells[i] != null) clone.Spells[i] = this.Spells[i]!.Clone();
            }
            clone.Actions = new List<MoveEntry>();
            foreach (var a in this.Actions) clone.Actions.Add(a.Clone());
            return clone;
        }
    }
}
