namespace ProjectVagabond
{
    /// <summary>
    /// A data structure that defines a status effect to be applied by an attack or weapon.
    /// </summary>
    public class StatusEffectApplication
    {
        /// <summary>
        /// The name of the status effect, which corresponds to a StatusEffect class (e.g., "Poison").
        /// </summary>
        public string EffectName { get; set; }

        /// <summary>
        /// The potency or duration of the effect, expressed in dice notation (e.g., "1d6", "5", "2d6+1").
        /// </summary>
        public string Amount { get; set; }
    }
}