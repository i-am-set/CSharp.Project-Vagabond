using Microsoft.Xna.Framework.Input;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
    /// Defines the targeting behavior of a move in a 2v2 style
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
        Bleeding,

        // Temps
        Stun,
        Regen,
        Dodging,
        Silence,
        Protected,
        Empowered,
        TargetMe,
        Provoked,
        WideProtected
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