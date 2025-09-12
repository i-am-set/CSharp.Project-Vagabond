namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Specifies the type of action a move performs.
    /// </summary>
    public enum ActionType
    {
        Physical,
        Magical,
        Other
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