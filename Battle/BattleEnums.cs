namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Specifies the type of damage a move inflicts upon impact.
    /// </summary>
    public enum ImpactType
    {
        Physical,
        Magical
    }

    /// <summary>
    /// Specifies the fundamental nature of a move (magical or non-magical).
    /// </summary>
    public enum MoveType
    {
        Action,
        Spell
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