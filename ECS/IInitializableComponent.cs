namespace ProjectVagabond
{
    /// <summary>
    /// An interface for components that require an initialization step
    /// after their properties have been set by the Spawner.
    /// </summary>
    public interface IInitializableComponent : IComponent
    {
        /// <summary>
        /// Called by the Spawner after the component is created and its
        /// properties are populated from the archetype definition.
        /// </summary>
        void Initialize();
    }
}