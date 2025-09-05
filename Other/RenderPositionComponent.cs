using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Stores the entity's visual position, which can be interpolated for smooth movement,
    /// independent of its logical grid position.
    /// </summary>
    public class RenderPositionComponent : IComponent, ICloneableComponent
    {
        public Vector2 WorldPosition { get; set; }

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}