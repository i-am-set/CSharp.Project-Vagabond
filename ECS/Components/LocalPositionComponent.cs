using Microsoft.Xna.Framework;

namespace ProjectVagabond
{
    /// <summary>
    /// Stores the entity's position within the local area grid.
    /// </summary>
    public class LocalPositionComponent : IComponent
    {
        public Vector2 LocalPosition { get; set; }
    }
}