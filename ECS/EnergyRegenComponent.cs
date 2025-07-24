namespace ProjectVagabond
{
    /// <summary>
    /// Tracks the fractional progress an entity has made towards regenerating one energy point.
    /// When Progress >= 1.0, the entity can regenerate energy.
    /// </summary>
    public class EnergyRegenComponent : IComponent, ICloneableComponent
    {
        public float RegenerationProgress { get; set; } = 0f;

        public IComponent Clone()
        {
            // This is a runtime state component, so cloning just creates a fresh, empty one.
            return new EnergyRegenComponent();
        }
    }
}