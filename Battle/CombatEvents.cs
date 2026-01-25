namespace ProjectVagabond.Battle
{
    public enum CombatEventType
    {
        // Stats
        CalculateStat,

        // Damage Calculation
        CalculateBasePower,
        CalculateOutgoingDamage,
        CalculateIncomingDamage,
        CalculateAllyDamage,
        CalculateDefensePenetration,
        CalculateFixedDamage,

        // Mechanics
        CheckCritChance,
        CheckCritDamage,
        CheckAccuracy,
        CheckEvasion,
        CheckStatusImmunity,
        CheckDazeImmunity,
        CheckStatChangeBlock,
        ModifyElementalAffinity,
        QueryMoveLock, // For Stubborn/Choice Band

        // Action Flow
        ActionDeclared,
        ActionComplete,

        // Triggers
        OnHit,
        OnDamaged,
        OnKill,
        OnCritReceived,
        OnStatusApplied,
        OnLifesteal,

        // Lifecycle
        TurnStart,
        TurnEnd,
        BattleStart,
        CombatantEnter
    }
}