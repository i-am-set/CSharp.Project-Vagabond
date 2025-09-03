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
        /// The texture to draw for the entity. This is populated at runtime.
        /// </summary>
        public Texture2D Texture { get; set; }

        /// <summary>
        /// The color to tint the texture with.
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// The content path for the entity's sprite, relative to the Content root.
        /// e.g., "Sprites/Enemies/wanderer"
        /// </summary>
        public string SpritePath { get; set; }

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
            // Texture is a runtime property, so it's not part of the clone.
            var clone = (RenderableComponent)this.MemberwiseClone();
            clone.Texture = null;
            return clone;
        }
    }
}