using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class PartyMember
    {
        public string Name { get; set; }
        public int Level { get; set; } = 1;
        public int CurrentEXP { get; set; }
        public int MaxEXP { get; set; } = 100;
        public int MaxHP { get; set; }
        public int CurrentHP { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Tenacity { get; set; }
        public int Agility { get; set; }

        public int PortraitIndex { get; set; } = 0;

        public Dictionary<string, string> IntrinsicAbilities { get; set; } = new Dictionary<string, string>();

        public MoveEntry? BasicMove { get; set; }
        public MoveEntry? Spell1 { get; set; }
        public MoveEntry? Spell2 { get; set; }
        public MoveEntry? Spell3 { get; set; }

        public HashSet<string> KnownMovesHistory { get; set; } = new HashSet<string>();
        public List<TemporaryBuff> ActiveBuffs { get; set; } = new List<TemporaryBuff>();

        public PartyMember() { }

        public PartyMember Clone()
        {
            var clone = (PartyMember)this.MemberwiseClone();
            clone.IntrinsicAbilities = new Dictionary<string, string>(this.IntrinsicAbilities);

            if (this.BasicMove != null) clone.BasicMove = this.BasicMove.Clone();
            if (this.Spell1 != null) clone.Spell1 = this.Spell1.Clone();
            if (this.Spell2 != null) clone.Spell2 = this.Spell2.Clone();
            if (this.Spell3 != null) clone.Spell3 = this.Spell3.Clone();

            clone.KnownMovesHistory = new HashSet<string>(this.KnownMovesHistory);

            clone.ActiveBuffs = new List<TemporaryBuff>();
            foreach (var buff in this.ActiveBuffs)
            {
                clone.ActiveBuffs.Add(new TemporaryBuff { EffectType = buff.EffectType, RemainingBattles = buff.RemainingBattles });
            }

            return clone;
        }
    }
}