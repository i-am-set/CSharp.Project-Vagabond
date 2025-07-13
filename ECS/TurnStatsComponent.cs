namespace ProjectVagabond
{
    /// <summary>
    /// A component that holds the dynamic resources an entity has for a single combat turn,
    /// such as actions and movement budget.
    /// </summary>
    public class TurnStatsComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// Gets or sets a value indicating whether the entity has its primary action available.
        /// Used for major actions like attacking.
        /// </summary>
        public bool HasPrimaryAction { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the entity has its secondary action available.
        /// Used for minor actions like using certain skills or items.
        /// </summary>
        public bool HasSecondaryAction { get; set; } = true;

        /// <summary>
        /// Gets or sets the total time (in seconds) of movement used during the current turn.
        /// This is compared against GameState.COMBAT_TURN_DURATION_SECONDS.
        /// </summary>
        public float MovementTimeUsedThisTurn { get; set; } = 0f;

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}