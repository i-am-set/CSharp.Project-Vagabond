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

        // Shifted to strict 2-move system (Strike and Alt)
        public MoveEntry? StrikeMove { get; set; }
        public MoveEntry? AltMove { get; set; }

        public HashSet<string> KnownMovesHistory { get; set; } = new HashSet<string>();
        public List<TemporaryBuff> ActiveBuffs { get; set; } = new List<TemporaryBuff>();

        public PartyMember() { }

        public PartyMember Clone()
        {
            var clone = (PartyMember)this.MemberwiseClone();
            clone.IntrinsicAbilities = new Dictionary<string, string>(this.IntrinsicAbilities);

            if (this.StrikeMove != null) clone.StrikeMove = this.StrikeMove.Clone();
            if (this.AltMove != null) clone.AltMove = this.AltMove.Clone();

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