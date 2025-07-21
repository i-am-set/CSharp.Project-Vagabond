namespace ProjectVagabond
{
    /// <summary>
    /// A component that marks an entity as a corpse and stores the
    /// original ID of the entity that died.
    /// </summary>
    public class CorpseComponent : IComponent, ICloneableComponent
    {
        public int OriginalEntityId { get; set; }

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}
