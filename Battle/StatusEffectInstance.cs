using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle
{
    /// <summary>
    /// Represents an active buff or debuff on a combatant.
    /// </summary>
    public class StatusEffectInstance
    {
        /// <summary>
        /// The type of the status effect.
        /// </summary>
        public StatusEffectType EffectType { get; set; }

        /// <summary>
        /// The remaining duration of the effect in turns.
        /// For permanent effects, this value is ignored (usually set to -1 or 999).
        /// </summary>
        public int DurationInTurns { get; set; }

        /// <summary>
        /// Tracks how many turns this specific instance of Poison has been active.
        /// Used to calculate the doubling damage.
        /// </summary>
        public int PoisonTurnCount { get; set; } = 0;

        public bool IsPermanent
        {
            get
            {
                return EffectType == StatusEffectType.Poison ||
                       EffectType == StatusEffectType.Burn ||
                       EffectType == StatusEffectType.Frostbite ||
                       EffectType == StatusEffectType.Bleeding;
            }
        }

        /// <summary>
        /// Initializes a new instance of the StatusEffectInstance class.
        /// </summary>
        /// <param name="effectType">The type of the status effect.</param>
        /// <param name="durationInTurns">The duration of the effect in turns. Ignored for Perm effects.</param>
        public StatusEffectInstance(StatusEffectType effectType, int durationInTurns)
        {
            EffectType = effectType;
            DurationInTurns = durationInTurns;
        }

        /// <summary>
        /// Gets a user-friendly display name for the status effect.
        /// </summary>
        public string GetDisplayName()
        {
            return EffectType switch
            {
                StatusEffectType.Poison => "Poisoned",
                StatusEffectType.Stun => "Stunned",
                StatusEffectType.Regen => "Regeneration",
                StatusEffectType.Dodging => "Dodging",
                StatusEffectType.Burn => "Burn",
                StatusEffectType.Frostbite => "Frostbite",
                StatusEffectType.Silence => "Silenced",
                StatusEffectType.TargetMe => "Draw Fire",
                StatusEffectType.Provoked => "Provoked",
                StatusEffectType.Bleeding => "Bleeding", 
                _ => EffectType.ToString(),
            };
        }

        /// <summary>
        /// Gets the formatted text for the tooltip title, including name and duration.
        /// </summary>
        public string GetTooltipText()
        {
            string name = GetDisplayName().ToUpper();
            if (IsPermanent)
            {
                return name;
            }
            return $"{name} ({DurationInTurns})";
        }

        /// <summary>
        /// Gets the description text for the tooltip, explaining the effect.
        /// </summary>
        public string GetDescription()
        {
            var global = ServiceLocator.Get<Global>();
            switch (EffectType)
            {
                case StatusEffectType.Burn:
                    return $"{global.BurnDamageMultiplier}x damage received";
                case StatusEffectType.Poison:
                    // Calculate next turn damage
                    int safeTurnCount = Math.Min(PoisonTurnCount, 30);
                    long dmg = (long)global.PoisonBaseDamage * (long)Math.Pow(2, safeTurnCount);
                    return $"Does {dmg} damage at end of turn";
                case StatusEffectType.Regen:
                    return $"Restores {global.RegenPercent * 100}% HP at end of turn";
                case StatusEffectType.Dodging:
                    return $"{global.DodgingAccuracyMultiplier}x chance to be hit";
                case StatusEffectType.Silence:
                    return "Can't cast spells";
                case StatusEffectType.Stun:
                    return "Can't move this turn";
                case StatusEffectType.Frostbite:
                    return $"{global.FrostbiteAgilityMultiplier}x agility";
                case StatusEffectType.TargetMe:
                    return "Enemies must target this unit";
                case StatusEffectType.Provoked:
                    return "Can't use status moves";
                case StatusEffectType.Bleeding:
                    return "Takes 10% Max HP damage at end of turn";
                default:
                    return "";
            }
        }
    }
}
