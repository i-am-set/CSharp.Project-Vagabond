namespace ProjectVagabond
{
    /// <summary>
    /// Defines the behavioral archetypes for AI entities.
    /// </summary>
    public enum AIPersonalityType
    {
        /// <summary>
        /// Attacks the player on sight if within aggro range.
        /// </summary>
        Aggressive,
        /// <summary>
        /// Ignores the player until attacked, then becomes Aggressive.
        /// </summary>
        Neutral,
        /// <summary>
        /// Ignores the player until attacked, then becomes Fearful (flees).
        /// </summary>
        Passive,
        /// <summary>
        /// Flees from the player on sight if within aggro range.
        /// </summary>
        Fearful
    }

    /// <summary>
    /// A component that defines the behavior and hostility state of an AI entity.
    /// </summary>
    public class AIPersonalityComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// The fundamental behavior of the AI.
        /// </summary>
        public AIPersonalityType Personality { get; set; } = AIPersonalityType.Neutral;

        /// <summary>
        /// A flag indicating if the AI has been provoked and is now hostile towards the player.
        /// Used by Neutral and Passive personalities.
        /// </summary>
        public bool IsProvoked { get; set; } = false;

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}