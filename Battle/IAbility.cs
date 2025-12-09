using ProjectVagabond.Battle;

namespace ProjectVagabond.Battle.Abilities
{
    public interface IAbility
    {
        string Name { get; }
        string Description { get; }
    }

    // --- STATS & ATTRIBUTES ---
    public interface IStatModifier : IAbility
    {
        int ModifyStat(OffensiveStatType statType, int currentValue, BattleCombatant owner);
        int ModifyMaxStat(string statName, int currentValue);
    }

    public interface ICritModifier : IAbility
    {
        float ModifyCritChance(float currentChance, CombatContext ctx);
        float ModifyCritDamage(float currentMultiplier, CombatContext ctx);
    }

    public interface IAccuracyModifier : IAbility
    {
        int ModifyAccuracy(int currentAccuracy, CombatContext ctx);
        bool ShouldIgnoreEvasion(CombatContext ctx);
    }

    // --- DAMAGE CALCULATION ---
    public interface IOutgoingDamageModifier : IAbility
    {
        float ModifyOutgoingDamage(float currentDamage, CombatContext ctx);
    }

    public interface IIncomingDamageModifier : IAbility
    {
        float ModifyIncomingDamage(float currentDamage, CombatContext ctx);
    }

    // --- ACTION FLOW ---
    public interface IActionModifier : IAbility
    {
        // Modifies an action before it is queued (e.g. Priority, Power)
        void ModifyAction(QueuedAction action, BattleCombatant owner);
    }

    public interface IOnActionComplete : IAbility
    {
        // Triggers after an action finishes executing
        void OnActionComplete(QueuedAction action, BattleCombatant owner);
    }

    // --- TRIGGERS ---
    public interface IOnHitEffect : IAbility
    {
        void OnHit(CombatContext ctx, int damageDealt);
    }

    public interface IOnDamagedEffect : IAbility
    {
        void OnDamaged(CombatContext ctx, int damageTaken);
    }

    public interface IOnKill : IAbility
    {
        void OnKill(CombatContext ctx);
    }

    public interface IOnCritReceived : IAbility
    {
        void OnCritReceived(CombatContext ctx);
    }

    public interface IOnStatusApplied : IAbility
    {
        void OnStatusApplied(CombatContext ctx, StatusEffectInstance status);
    }

    public interface ILifestealReaction : IAbility
    {
        // Returns true if the ability "consumed" the lifesteal event (preventing normal healing)
        bool OnLifestealReceived(BattleCombatant source, int amount, BattleCombatant owner);
    }

    // --- LIFECYCLE ---
    public interface ITurnLifecycle : IAbility
    {
        void OnTurnStart(BattleCombatant owner);
        void OnTurnEnd(BattleCombatant owner);
    }

    public interface IBattleLifecycle : IAbility
    {
        void OnBattleStart(BattleCombatant owner);
        void OnCombatantEnter(BattleCombatant owner);
    }
}