using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond
{
    public struct GridElement
    {
        public Texture2D Texture;
        public Color Color;
        public Vector2 ScreenPosition;
        public Vector2 WorldPosition;

        public GridElement(Texture2D texture, Color color, Vector2 screenPosition, Vector2 worldPosition)
        {
            Texture = texture;
            Color = color;
            ScreenPosition = screenPosition;
            WorldPosition = worldPosition;
        }
    }
}