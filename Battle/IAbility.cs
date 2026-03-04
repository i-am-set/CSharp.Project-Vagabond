using ProjectVagabond.Battle;

namespace ProjectVagabond.Battle.Abilities
{
    public static class AbilityPriority
    {
        public const int BaseOverride = 100;
        public const int FlatAddition = 50;
        public const int Multiplier = 10;
        public const int StatusEffect = 0;
    }

    public interface IAbility
    {
        string Name { get; }
        string Description { get; }
        int Priority { get; }
        void OnEvent(GameEvent e, BattleContext context);
    }

    public interface IStatusInflictingAbility : IAbility
    {
        StatusEffectType EffectType { get; }
        int Chance { get; }
        int Duration { get; }
    }

    public interface IHealingAbility : IAbility
    {
        int HealAmount { get; }
        float HealPercentage { get; }
    }

    public interface IStatModifyingAbility : IAbility
    {
        OffensiveStatType Stat { get; }
        int Amount { get; }
    }
}