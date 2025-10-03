using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class TemporaryBuff
    {
        public StatusEffectType EffectType { get; set; }
        public int RemainingBattles { get; set; }
    }

    public class TemporaryBuffsComponent : IComponent, ICloneableComponent
    {
        public List<TemporaryBuff> Buffs { get; set; } = new List<TemporaryBuff>();

        public IComponent Clone()
        {
            var clone = (TemporaryBuffsComponent)this.MemberwiseClone();
            // Deep copy the list of buffs
            clone.Buffs = new List<TemporaryBuff>();
            foreach (var buff in this.Buffs)
            {
                clone.Buffs.Add(new TemporaryBuff { EffectType = buff.EffectType, RemainingBattles = buff.RemainingBattles });
            }
            return clone;
        }
    }
}