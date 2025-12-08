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
        /// <summary>
        /// Targets any single combatant on the field EXCEPT the user (Enemy 1, Enemy 2, or Ally).
        /// </summary>
        Single,

        /// <summary>
        /// Targets ANY single combatant on the field, INCLUDING the user.
        /// </summary>
        SingleAll,

        /// <summary>
        /// Targets BOTH enemies simultaneously. Cannot hit own party.
        /// </summary>
        Both,

        /// <summary>
        /// Targets BOTH enemies AND the Ally. Does not hit the user.
        /// </summary>
        Every,

        /// <summary>
        /// Targets EVERY combatant on the field (Enemies, Ally, and Self).
        /// </summary>
        All,

        /// <summary>
        /// Targets ONLY the user.
        /// </summary>
        Self,

        /// <summary>
        /// Targets the user AND the Ally.
        /// </summary>
        Team,

        /// <summary>
        /// Targets ONLY the Ally.
        /// </summary>
        Ally,

        /// <summary>
        /// Targets either the user OR the Ally (requires selection).
        /// </summary>
        SingleTeam,

        /// <summary>
        /// Randomly targets ONE of the two enemies.
        /// </summary>
        RandomBoth,

        /// <summary>
        /// Randomly targets ONE of: Enemy 1, Enemy 2, or Ally.
        /// </summary>
        RandomEvery,

        /// <summary>
        /// Randomly targets ONE of: Enemy 1, Enemy 2, Ally, or Self.
        /// </summary>
        RandomAll,

        /// <summary>
        /// No target (e.g. field effects).
        /// </summary>
        None
    }

    /// <summary>
    /// Defines the specific types of status effects that can be applied to a combatant.
    /// </summary>
    public enum StatusEffectType
    {
        Poison,
        Stun,
        Regen,
        Dodging,
        Burn,
        Freeze,
        Blind,
        Confuse,
        Silence,
        Fear,
        Root
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
