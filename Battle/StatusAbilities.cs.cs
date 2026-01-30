using ProjectVagabond.Battle;
using System;

namespace ProjectVagabond.Battle.Abilities
{
    public class InflictStatusBurnAbility : InflictStatusAbility
    {
        public InflictStatusBurnAbility(int chance, int duration = -1)
            : base("Burn", StatusEffectType.Burn, chance, duration) { }
    }

    public class InflictStatusPoisonAbility : InflictStatusAbility
    {
        public InflictStatusPoisonAbility(int chance, int duration = -1)
            : base("Poison", StatusEffectType.Poison, chance, duration) { }
    }

    public class InflictStatusFrostbiteAbility : InflictStatusAbility
    {
        public InflictStatusFrostbiteAbility(int chance, int duration = -1)
            : base("Frostbite", StatusEffectType.Frostbite, chance, duration) { }
    }

    public class InflictStatusBleedingAbility : InflictStatusAbility
    {
        public InflictStatusBleedingAbility(int chance, int duration = -1)
            : base("Bleeding", StatusEffectType.Bleeding, chance, duration) { }
    }

    public class InflictStatusStunAbility : InflictStatusAbility
    {
        // Stun is usually temporary, default to 1 turn if not specified
        public InflictStatusStunAbility(int chance, int duration = 1)
            : base("Stun", StatusEffectType.Stun, chance, duration) { }
    }

    public class InflictStatusSilenceAbility : InflictStatusAbility
    {
        public InflictStatusSilenceAbility(int chance, int duration = 3)
            : base("Silence", StatusEffectType.Silence, chance, duration) { }
    }

    public class InflictStatusProvokedAbility : InflictStatusAbility
    {
        public InflictStatusProvokedAbility(int chance, int duration = 3)
            : base("Provoke", StatusEffectType.Provoked, chance, duration) { }
    }
}