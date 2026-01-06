using Microsoft.Xna.Framework;
using ProjectVagabond;
using ProjectVagabond.Battle;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Stores the entity's position in the world map grid.
    /// </summary>
    public class PositionComponent : IComponent, ICloneableComponent
    {
        public Vector2 WorldPosition { get; set; }

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}
