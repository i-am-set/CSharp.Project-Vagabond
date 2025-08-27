using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Graphics;
using System;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Provides "pixel-perfect" drawing extension methods for SpriteBatch.
    /// These methods round the final drawing position to the nearest integer, ensuring
    /// sprites and text align perfectly with a virtual pixel grid, preventing sub-pixel artifacts.
    /// </summary>
    public static class SpriteBatchExtensions
    {
        private static Vector2 RoundVector(Vector2 vector)
        {
            return new Vector2(MathF.Round(vector.X), MathF.Round(vector.Y));
        }

        // --- DrawStringSnapped ---

        public static void DrawStringSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, RoundVector(position), color);
        }

        public static void DrawStringSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            spriteBatch.DrawString(font, text, RoundVector(position), color, rotation, origin, scale, effects, layerDepth);
        }

        // --- DrawSnapped ---

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Color color)
        {
            spriteBatch.Draw(texture, RoundVector(position), color);
        }

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Rectangle destinationRectangle, Color color)
        {
            var snappedRect = new Rectangle(
                (int)MathF.Round(destinationRectangle.X),
                (int)MathF.Round(destinationRectangle.Y),
                destinationRectangle.Width,
                destinationRectangle.Height
            );
            spriteBatch.Draw(texture, snappedRect, color);
        }

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color)
        {
            var snappedRect = new Rectangle(
                (int)MathF.Round(destinationRectangle.X),
                (int)MathF.Round(destinationRectangle.Y),
                destinationRectangle.Width,
                destinationRectangle.Height
            );
            spriteBatch.Draw(texture, snappedRect, sourceRectangle, color);
        }

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            spriteBatch.Draw(texture, RoundVector(position), sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
        }

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
        {
            spriteBatch.Draw(texture, RoundVector(position), sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
        }

        // --- DrawLineSnapped ---
        public static void DrawLineSnapped(this SpriteBatch spriteBatch, Vector2 point1, Vector2 point2, Color color, float thickness = 1f, float layerDepth = 0)
        {
            spriteBatch.DrawLine(RoundVector(point1), RoundVector(point2), color, thickness, layerDepth);
        }
    }
}