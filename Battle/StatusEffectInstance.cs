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
    }
}