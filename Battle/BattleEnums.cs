namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Specifies the type of damage a move inflicts.
    /// </summary>
    public enum DamageType
    {
        Physical,
        Magical
    }

    /// <summary>
    /// Defines the specific types of status effects that can be applied to a combatant.
    /// </summary>
    public enum StatusEffectType
    {
        StrengthUp,
        IntelligenceDown,
        TenacityUp,
        AgilityDown,
        Poison,
        Stun,
        Regen
    }
}