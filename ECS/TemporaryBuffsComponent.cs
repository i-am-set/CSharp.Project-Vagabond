using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class TemporaryBuff
    {
        public StatusEffectType EffectType { get; set; }
        public int RemainingBattles { get; set; }
    }

    public class TemporaryBuffsComponent : IComponent
    {
        public List<TemporaryBuff> Buffs { get; set; } = new List<TemporaryBuff>();
    }
}