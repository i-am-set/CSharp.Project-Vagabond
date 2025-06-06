using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond
{
    public struct GridElement
    {
        public Texture2D Texture;
        public Color Color;
        public Vector2 Position;

        public GridElement(Texture2D texture, Color color, Vector2 position)
        {
            Texture = texture;
            Color = color;
            Position = position;
        }
    }
}
