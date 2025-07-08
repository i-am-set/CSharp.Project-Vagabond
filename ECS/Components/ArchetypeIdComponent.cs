namespace ProjectVagabond
{
    /// <summary>
    /// A component that stores the archetype ID of an entity, providing a reliable
    /// way to look up its original template.
    /// </summary>
    public class ArchetypeIdComponent : IComponent
    {
        public string ArchetypeId { get; set; }
    }
}