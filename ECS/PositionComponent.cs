using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Stores the entity's position in the world map grid.
    /// </summary>
    public class PositionComponent : IComponent, ICloneableComponent
    {
        public Vector2 WorldPosition { get; set; }
        public Point CurrentChunk { get; set; }

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}