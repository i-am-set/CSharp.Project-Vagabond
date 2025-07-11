namespace ProjectVagabond
{
    public interface ICloneableComponent : IComponent
    {
        IComponent Clone();
    }
}