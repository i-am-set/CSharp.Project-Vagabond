using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ProjectVagabond
{
    /// <summary>
    /// A component that gives an entity the necessary data to be drawn on the screen.
    /// </summary>
    public class RenderableComponent : IComponent, ICloneableComponent
    {
        /// <summary>
        /// The texture to draw for the entity.
        /// </summary>
        public Texture2D Texture { get; set; }

        /// <summary>
        /// The color to tint the texture with.
        /// </summary>
        public Color Color { get; set; }

        public RenderableComponent(Texture2D texture, Color color)
        {
            Texture = texture;
            Color = color;
        }

        /// <summary>
        /// Parameterless constructor for the Spawner.
        /// </summary>
        public RenderableComponent() { }

        public IComponent Clone()
        {
            return (IComponent)this.MemberwiseClone();
        }
    }
}