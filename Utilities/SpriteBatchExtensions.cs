using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Graphics;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Provides drawing extension methods.
    /// </summary>
    public static class SpriteBatchExtensions
    {
        // --- DrawStringSnapped (Now Smooth) ---
        // Removed the (1,0) shadow entirely. Now just draws the text snapped to pixels.

        public static void DrawStringSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, position, color);
        }

        public static void DrawStringSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            spriteBatch.DrawString(font, text, position, color, rotation, origin, scale, effects, layerDepth);
        }

        public static void DrawStringSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
        {
            spriteBatch.DrawString(font, text, position, color, rotation, origin, scale, effects, layerDepth);
        }

        // --- DrawStringOutlinedSnapped (Circle/Cross Outline) ---

        public static void DrawStringOutlinedSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color textColor, Color outlineColor)
        {
            // 1. Draw outline in 4 directions
            spriteBatch.DrawString(font, text, position + new Vector2(-1, 0), outlineColor);
            spriteBatch.DrawString(font, text, position + new Vector2(1, 0), outlineColor);
            spriteBatch.DrawString(font, text, position + new Vector2(0, 1), outlineColor);
            spriteBatch.DrawString(font, text, position + new Vector2(0, -1), outlineColor);

            // 2. Draw main text
            spriteBatch.DrawString(font, text, position, textColor);
        }

        public static void DrawStringOutlinedSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color textColor, Color outlineColor, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            // 1. Draw outline in 4 directions
            spriteBatch.DrawString(font, text, position + new Vector2(-1, 0), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(1, 0), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(0, 1), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(0, -1), outlineColor, rotation, origin, scale, effects, layerDepth);

            // 2. Draw main text
            spriteBatch.DrawString(font, text, position, textColor, rotation, origin, scale, effects, layerDepth);
        }

        public static void DrawStringOutlinedSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color textColor, Color outlineColor, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
        {
            // 1. Draw outline in 4 directions
            spriteBatch.DrawString(font, text, position + new Vector2(-1, 0), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(1, 0), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(0, 1), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(0, -1), outlineColor, rotation, origin, scale, effects, layerDepth);

            // 2. Draw main text
            spriteBatch.DrawString(font, text, position, textColor, rotation, origin, scale, effects, layerDepth);
        }

        // --- DrawStringSquareOutlinedSnapped (Full 8-Direction Outline) ---

        public static void DrawStringSquareOutlinedSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color textColor, Color outlineColor)
        {
            // Diagonals
            spriteBatch.DrawString(font, text, position + new Vector2(-1, -1), outlineColor);
            spriteBatch.DrawString(font, text, position + new Vector2(1, -1), outlineColor);
            spriteBatch.DrawString(font, text, position + new Vector2(-1, 1), outlineColor);
            spriteBatch.DrawString(font, text, position + new Vector2(1, 1), outlineColor);

            // Cardinals
            spriteBatch.DrawString(font, text, position + new Vector2(-1, 0), outlineColor);
            spriteBatch.DrawString(font, text, position + new Vector2(1, 0), outlineColor);
            spriteBatch.DrawString(font, text, position + new Vector2(0, -1), outlineColor);
            spriteBatch.DrawString(font, text, position + new Vector2(0, 1), outlineColor);

            // Main text
            spriteBatch.DrawString(font, text, position, textColor);
        }

        public static void DrawStringSquareOutlinedSnapped(this SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color textColor, Color outlineColor, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            // Diagonals
            spriteBatch.DrawString(font, text, position + new Vector2(-1, -1), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(1, -1), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(-1, 1), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(1, 1), outlineColor, rotation, origin, scale, effects, layerDepth);

            // Cardinals
            spriteBatch.DrawString(font, text, position + new Vector2(-1, 0), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(1, 0), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(0, -1), outlineColor, rotation, origin, scale, effects, layerDepth);
            spriteBatch.DrawString(font, text, position + new Vector2(0, 1), outlineColor, rotation, origin, scale, effects, layerDepth);

            // Main text
            spriteBatch.DrawString(font, text, position, textColor, rotation, origin, scale, effects, layerDepth);
        }


        // --- DrawSnapped ---

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Color color)
        {
            spriteBatch.Draw(texture, position, color);
        }

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
        {
            spriteBatch.Draw(texture, position, sourceRectangle, color);
        }

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Rectangle destinationRectangle, Color color)
        {
            spriteBatch.Draw(texture, destinationRectangle, color);
        }

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color)
        {
            spriteBatch.Draw(texture, destinationRectangle, sourceRectangle, color);
        }

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            spriteBatch.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
        }

        public static void DrawSnapped(this SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
        {
            spriteBatch.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
        }

        // --- DrawLineSnapped ---
        public static void DrawLineSnapped(this SpriteBatch spriteBatch, Vector2 point1, Vector2 point2, Color color, float thickness = 1f, float layerDepth = 0)
        {
            spriteBatch.DrawLine(point1, point2, color, thickness, layerDepth);
        }

        // --- Bresenham Line Algorithms ---
        // These remain integer-based by definition of the algorithm.
        public static void DrawBresenhamLineSnapped(this SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color)
        {
            int x0 = (int)MathF.Round(start.X);
            int y0 = (int)MathF.Round(start.Y);
            int x1 = (int)MathF.Round(end.X);
            int y1 = (int)MathF.Round(end.Y);

            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                spriteBatch.Draw(pixel, new Vector2(x0, y0), color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        public static List<Point> GetBresenhamLinePoints(Vector2 start, Vector2 end)
        {
            var points = new List<Point>();
            int x0 = (int)MathF.Round(start.X);
            int y0 = (int)MathF.Round(start.Y);
            int x1 = (int)MathF.Round(end.X);
            int y1 = (int)MathF.Round(end.Y);

            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                points.Add(new Point(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
            return points;
        }

        // --- Animated Dotted Rectangle ---

        public static void DrawAnimatedDottedRectangle(this SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, float thickness = 1f, float dashLength = 4f, float gapLength = 4f, float offset = 0f)
        {
            // Define the 4 corners
            Vector2 tl = new Vector2(rect.Left, rect.Top);
            Vector2 tr = new Vector2(rect.Right, rect.Top);
            Vector2 br = new Vector2(rect.Right, rect.Bottom);
            Vector2 bl = new Vector2(rect.Left, rect.Bottom);

            float currentDist = 0f;

            // Draw Top
            DrawDottedLine(spriteBatch, pixel, tl, tr, color, thickness, dashLength, gapLength, offset, ref currentDist);
            // Draw Right
            DrawDottedLine(spriteBatch, pixel, tr, br, color, thickness, dashLength, gapLength, offset, ref currentDist);
            // Draw Bottom
            DrawDottedLine(spriteBatch, pixel, br, bl, color, thickness, dashLength, gapLength, offset, ref currentDist);
            // Draw Left
            DrawDottedLine(spriteBatch, pixel, bl, tl, color, thickness, dashLength, gapLength, offset, ref currentDist);
        }

        private static void DrawDottedLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color, float thickness, float dashLength, float gapLength, float globalOffset, ref float currentDist)
        {
            float length = Vector2.Distance(start, end);
            Vector2 dir = Vector2.Normalize(end - start);
            float angle = (float)Math.Atan2(dir.Y, dir.X);
            float totalPattern = dashLength + gapLength;

            float startP = 0f;
            while (startP < length)
            {
                float p = startP + currentDist; // Global distance
                float patternPos = (p - globalOffset) % totalPattern;
                if (patternPos < 0) patternPos += totalPattern;

                if (patternPos < dashLength)
                {
                    // We are in a dash. Draw until end of dash or end of line.
                    float distToGap = dashLength - patternPos;
                    float drawAmt = Math.Min(distToGap, length - startP);

                    spriteBatch.Draw(pixel, start + dir * startP, null, color, angle, Vector2.Zero, new Vector2(drawAmt, thickness), SpriteEffects.None, 0f);

                    startP += drawAmt;
                }
                else
                {
                    // We are in a gap. Skip until end of gap.
                    float distToDash = totalPattern - patternPos;
                    startP += Math.Min(distToDash, length - startP);
                }
            }
            currentDist += length;
        }
    }
}
