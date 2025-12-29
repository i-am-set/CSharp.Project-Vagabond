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

    /// <summary>
    /// Modifies the base variables of the damage formula (like Move Power) before defense is applied.
    /// Useful for moves that scale based on HP, Speed, or Weight.
    /// </summary>
    public interface ICalculationModifier : IAbility
    {
        float ModifyBasePower(float basePower, CombatContext ctx);
    }

    /// <summary>
    /// Allows an ability to completely override the damage calculation with a fixed value.
    /// This bypasses defense, stats, and random variance, but respects type immunity.
    /// </summary>
    public interface IFixedDamageModifier : IAbility
    {
        int GetFixedDamage(CombatContext ctx);
    }

    public interface IOutgoingDamageModifier : IAbility
    {
        float ModifyOutgoingDamage(float currentDamage, CombatContext ctx);
    }

    public interface IIncomingDamageModifier : IAbility
    {
        float ModifyIncomingDamage(float currentDamage, CombatContext ctx);
    }

    public interface IDefensePenetrationModifier : IAbility
    {
        float GetDefensePenetration(CombatContext ctx);
    }

    // --- ELEMENTAL ---
    public interface IElementalAffinityModifier : IAbility
    {
        void ModifyElementalAffinities(System.Collections.Generic.List<int> weaknesses, System.Collections.Generic.List<int> resistances, BattleCombatant owner);
    }

    // --- STATUS EFFECTS ---
    public interface IIncomingStatusModifier : IAbility
    {
        bool ShouldBlockStatus(StatusEffectType type, BattleCombatant owner);
    }

    public interface IOutgoingStatusModifier : IAbility
    {
        int ModifyStatusDuration(StatusEffectType type, int duration, BattleCombatant owner);
    }

    // --- ACTION FLOW ---
    public interface IActionModifier : IAbility
    {
        void ModifyAction(QueuedAction action, BattleCombatant owner);
    }

    public interface IOnActionComplete : IAbility
    {
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

    // --- SPECIAL MECHANICS ---
    public interface IShieldBreaker : IAbility
    {
        float BreakDamageMultiplier { get; }
        bool FailsIfNoProtect { get; }
    }
}