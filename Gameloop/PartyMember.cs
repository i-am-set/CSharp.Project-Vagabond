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
        public MoveEntry? CoreMove { get; set; }
        public MoveEntry? AltMove { get; set; }

        public List<TemporaryBuff> ActiveBuffs { get; set; } = new List<TemporaryBuff>();

        public PartyMember() { }

        public PartyMember Clone()
        {
            var clone = (PartyMember)this.MemberwiseClone();
            clone.IntrinsicAbilities = new Dictionary<string, string>(this.IntrinsicAbilities);

            if (this.BasicMove != null) clone.BasicMove = this.BasicMove.Clone();
            if (this.CoreMove != null) clone.CoreMove = this.CoreMove.Clone();
            if (this.AltMove != null) clone.AltMove = this.AltMove.Clone();

            clone.ActiveBuffs = new List<TemporaryBuff>();
            foreach (var buff in this.ActiveBuffs)
            {
                clone.ActiveBuffs.Add(new TemporaryBuff { EffectType = buff.EffectType, RemainingBattles = buff.RemainingBattles });
            }

            return clone;
        }
    }
}