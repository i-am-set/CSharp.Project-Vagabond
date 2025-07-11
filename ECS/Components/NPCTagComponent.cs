namespace ProjectVagabond
{
    /// <summary>
    /// A marker component used to identify non-player character entities.
    /// It contains no data.
    /// </summary>
    public class NPCTagComponent : IComponent, ICloneableComponent
    {
        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}