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
                       EffectType == StatusEffectType.Frostbite;
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
                _ => EffectType.ToString(),
            };
        }

        /// <summary>
        /// Gets the formatted text for the tooltip, including name and duration.
        /// </summary>
        /// <returns>A formatted string for the tooltip.</returns>
        public string GetTooltipText()
        {
            string name = GetDisplayName().ToUpper();
            if (IsPermanent)
            {
                return $"{name} (PERM)";
            }
            return $"{name} ({DurationInTurns})";
        }
    }
}