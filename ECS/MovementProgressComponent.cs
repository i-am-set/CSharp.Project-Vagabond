namespace ProjectVagabond
{
    /// <summary>
    /// Tracks the fractional progress an entity has made towards moving to the next tile.
    /// When Progress >= 1.0, the entity can move one tile.
    /// </summary>
    public class MovementProgressComponent : IComponent, ICloneableComponent
    {
        public float Progress { get; set; } = 0f;

        public IComponent Clone()
        {
            // This is a runtime state component, so cloning just creates a fresh, empty one.
            return new MovementProgressComponent();
        }
    }
}
