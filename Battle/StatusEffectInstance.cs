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
        /// </summary>
        public int DurationInTurns { get; set; }

        /// <summary>
        /// Initializes a new instance of the StatusEffectInstance class.
        /// </summary>
        /// <param name="effectType">The type of the status effect.</param>
        /// <param name="durationInTurns">The duration of the effect in turns.</param>
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
                StatusEffectType.StrengthUp => "Strength Up",
                StatusEffectType.IntelligenceDown => "Intelligence Down",
                StatusEffectType.TenacityUp => "Tenacity Up",
                StatusEffectType.AgilityDown => "Agility Down",
                StatusEffectType.Poison => "Poisoned",
                StatusEffectType.Stun => "Stunned",
                StatusEffectType.Regen => "Regeneration",
                StatusEffectType.Dodging => "Dodging",
                StatusEffectType.Burn => "Burn",
                StatusEffectType.Freeze => "Frozen",
                StatusEffectType.Blind => "Blind",
                StatusEffectType.Confuse => "Confused",
                StatusEffectType.Silence => "Silenced",
                StatusEffectType.Fear => "Feared",
                StatusEffectType.Root => "Rooted",
                _ => EffectType.ToString(),
            };
        }
    }
}