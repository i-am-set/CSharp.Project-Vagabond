using ProjectVagabond.Battle;
using System.Collections.Generic;

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

        public int PortraitIndex { get; set; } = 0;

        public Dictionary<string, string> IntrinsicAbilities { get; set; } = new Dictionary<string, string>();

        public MoveEntry? AttackMove { get; set; }
        public MoveEntry? SpecialMove { get; set; }

        public List<TemporaryBuff> ActiveBuffs { get; set; } = new List<TemporaryBuff>();

        public PartyMember() { }

        public PartyMember Clone()
        {
            var clone = (PartyMember)this.MemberwiseClone();
            clone.IntrinsicAbilities = new Dictionary<string, string>(this.IntrinsicAbilities);

            if (this.AttackMove != null) clone.AttackMove = this.AttackMove.Clone();
            if (this.SpecialMove != null) clone.SpecialMove = this.SpecialMove.Clone();

            clone.ActiveBuffs = new List<TemporaryBuff>();
            foreach (var buff in this.ActiveBuffs)
            {
                clone.ActiveBuffs.Add(new TemporaryBuff { EffectType = buff.EffectType, RemainingBattles = buff.RemainingBattles });
            }

            return clone;
        }
    }
}