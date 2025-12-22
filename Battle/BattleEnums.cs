using ProjectVagabond.Battle;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Specifies the type of damage a move inflicts upon impact.
    /// </summary>
    public enum ImpactType
    {
        Physical,
        Magical,
        Status
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
    /// Defines the targeting behavior of a move in a 2v2 VGC style context.
    /// </summary>
    public enum TargetType
    {
        Single,
        SingleAll,
        Both,
        Every,
        All,
        Self,
        Team,
        Ally,
        SingleTeam,
        RandomBoth,
        RandomEvery,
        RandomAll,
        None
    }

    /// <summary>
    /// Defines the specific types of status effects that can be applied to a combatant.
    /// </summary>
    public enum StatusEffectType
    {
        // Perms
        Poison,
        Burn,
        Frostbite,

        // Temps
        Stun,
        Regen,
        Dodging,
        Silence
    }

    /// <summary>
    /// Defines the primary function of a consumable item.
    /// </summary>
    public enum ConsumableType
    {
        Heal,
        Buff,
        Cleanse,
        Attack
    }

    /// <summary>
    /// Defines the combat stat used for a move's damage calculation.
    /// </summary>
    public enum OffensiveStatType
    {
        Strength,
        Intelligence,
        Tenacity,
        Agility
    }
}