using System.Collections.Generic;
using ProjectVagabond.Battle;

namespace ProjectVagabond
{
    public class PartyMember
    {
        public string Name { get; set; }

        // Stats
        public int Level { get; set; }
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int MaxMana { get; set; }
        public int CurrentMana { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public List<int> DefensiveElementIDs { get; set; } = new List<int>();
        public string DefaultStrikeMoveID { get; set; }
        public int PortraitIndex { get; set; } = 0;

        // Equipment
        public string? EquippedWeaponId { get; set; }
        public string? EquippedArmorId { get; set; }
        public string?[] EquippedRelics { get; set; } = new string?[3];
        public MoveEntry?[] EquippedSpells { get; set; } = new MoveEntry?[4];

        // Learned Moves (Infinite storage)
        public List<MoveEntry> Spells { get; set; } = new List<MoveEntry>();
        public List<MoveEntry> Actions { get; set; } = new List<MoveEntry>();

        public PartyMember() { }

        public PartyMember Clone()
        {
            // Shallow copy is mostly fine for strings/ints, but lists need care if modified
            var clone = (PartyMember)this.MemberwiseClone();
            clone.DefensiveElementIDs = new List<int>(this.DefensiveElementIDs);
            clone.Spells = new List<MoveEntry>();
            foreach (var s in this.Spells) clone.Spells.Add(s.Clone());
            clone.Actions = new List<MoveEntry>();
            foreach (var a in this.Actions) clone.Actions.Add(a.Clone());

            clone.EquippedRelics = (string?[])this.EquippedRelics.Clone();
            clone.EquippedSpells = (MoveEntry?[])this.EquippedSpells.Clone();

            return clone;
        }
    }
}