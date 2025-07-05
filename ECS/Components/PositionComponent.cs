using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Stores the entity's position in the world map grid.
    /// </summary>
    public class PositionComponent : IComponent
    {
        public Vector2 WorldPosition { get; set; }
    }
}