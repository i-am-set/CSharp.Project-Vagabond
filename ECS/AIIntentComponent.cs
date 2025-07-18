namespace ProjectVagabond
{
    /// <summary>
    /// Defines the current high-level intention of an AI entity.
    /// </summary>
    public enum AIIntent
    {
        /// <summary>
        /// The AI is wandering or idle, with no specific target.
        /// </summary>
        None,
        /// <summary>
        /// The AI is actively moving towards the player with hostile intent.
        /// </summary>
        Pursuing,
        /// <summary>
        /// The AI is actively moving away from the player.
        /// </summary>
        Fleeing
    }

    /// <summary>
    /// A component that stores the current runtime intent of an AI.
    /// This is used by rendering systems to display visual cues.
    /// </summary>
    public class AIIntentComponent : IComponent, ICloneableComponent
    {
        public AIIntent CurrentIntent { get; set; } = AIIntent.None;

        public IComponent Clone()
        {
            // This is a runtime state component, so cloning just resets it.
            var clone = (AIIntentComponent)this.MemberwiseClone();
            clone.CurrentIntent = AIIntent.None;
            return clone;
        }
    }
}