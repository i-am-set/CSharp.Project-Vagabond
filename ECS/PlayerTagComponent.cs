namespace ProjectVagabond
{
    /// <summary>
    /// A marker component used to identify the player entity.
    /// It contains no data.
    /// </summary>
    public class PlayerTagComponent : IComponent, ICloneableComponent
    {
        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}