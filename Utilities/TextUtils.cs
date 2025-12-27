using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectVagabond.Utils
{
    public static class TextUtils
    {
        /// <summary>
        /// Draws text with a one-shot left-to-right wave animation.
        /// Uses sentinel measurement to ensure pixel-perfect spacing matches the static font rendering.
        /// </summary>
        /// <param name="charIndexOffset">The starting index for the wave calculation, useful for multi-segment text.</param>
        public static void DrawWavedText(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, float waveTimer, float speed, float frequency, float amplitude, int charIndexOffset = 0)
        {
            float startX = position.X;
            float baseY = position.Y;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string charStr = c.ToString();

                // --- SENTINEL MEASUREMENT TRICK ---
                // Measure the string up to this character, including a sentinel character '|'.
                // Then subtract the width of the sentinel.
                // This forces MeasureString to include trailing spaces and kerning correctly.
                string sub = text.Substring(0, i);
                float charOffsetX = font.MeasureString(sub + "|").Width - font.MeasureString("|").Width;

                // Calculate Wave Offset (Left to Right)
                // Argument: Time * Speed - Index * Frequency
                // We add the charIndexOffset to 'i' to ensure continuity across multiple text segments
                float waveArg = waveTimer * speed - (i + charIndexOffset) * frequency;

                float yWaveOffset = 0f;
                // We only want a single positive bump (0 to PI).
                if (waveArg > 0 && waveArg < MathHelper.Pi)
                {
                    float waveVal = MathF.Sin(waveArg);
                    // Bump UP (negative Y). Clamp to exactly WaveAmplitude.
                    yWaveOffset = -MathF.Round(waveVal * amplitude);
                }

                spriteBatch.DrawStringSnapped(font, charStr, new Vector2(startX + charOffsetX, baseY + yWaveOffset), color);
            }
        }

        /// <summary>
        /// Draws text with an outline and a one-shot left-to-right wave animation.
        /// Manually handles layers to prevent double-shadow artifacts on the outline.
        /// </summary>
        /// <param name="charIndexOffset">The starting index for the wave calculation, useful for multi-segment text.</param>
        public static void DrawWavedTextOutlined(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color textColor, Color outlineColor, float waveTimer, float speed, float frequency, float amplitude, int charIndexOffset = 0)
        {
            float startX = position.X;
            float baseY = position.Y;

            // Calculate shadow color for the main text (1/4th brightness)
            var shadowColor = new Color(textColor.R / 4, textColor.G / 4, textColor.B / 4, textColor.A);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string charStr = c.ToString();

                string sub = text.Substring(0, i);
                float charOffsetX = font.MeasureString(sub + "|").Width - font.MeasureString("|").Width;

                float waveArg = waveTimer * speed - (i + charIndexOffset) * frequency;
                float yWaveOffset = 0f;
                if (waveArg > 0 && waveArg < MathHelper.Pi)
                {
                    float waveVal = MathF.Sin(waveArg);
                    yWaveOffset = -MathF.Round(waveVal * amplitude);
                }

                // Round the base position for pixel perfection
                Vector2 charPos = new Vector2(MathF.Round(startX + charOffsetX), MathF.Round(baseY + yWaveOffset));

                // 1. Draw Outline (Raw DrawString, no extra shadows)
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 0), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(-1, 0), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(0, 1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(0, -1), outlineColor);

                // 2. Draw Depth Shadow (Raw DrawString)
                // This adds the "snapped" look to the main text, drawn over the outline but under the text
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 0), shadowColor);

                // 3. Draw Main Text (Raw DrawString)
                spriteBatch.DrawString(font, charStr, charPos, textColor);
            }
        }

        /// <summary>
        /// Draws text with a full 8-direction square outline and wave animation.
        /// </summary>
        public static void DrawWavedTextSquareOutlined(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color textColor, Color outlineColor, float waveTimer, float speed, float frequency, float amplitude, int charIndexOffset = 0)
        {
            float startX = position.X;
            float baseY = position.Y;
            var shadowColor = new Color(textColor.R / 4, textColor.G / 4, textColor.B / 4, textColor.A);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string charStr = c.ToString();
                string sub = text.Substring(0, i);
                float charOffsetX = font.MeasureString(sub + "|").Width - font.MeasureString("|").Width;

                float waveArg = waveTimer * speed - (i + charIndexOffset) * frequency;
                float yWaveOffset = 0f;
                if (waveArg > 0 && waveArg < MathHelper.Pi)
                {
                    float waveVal = MathF.Sin(waveArg);
                    yWaveOffset = -MathF.Round(waveVal * amplitude);
                }

                Vector2 charPos = new Vector2(MathF.Round(startX + charOffsetX), MathF.Round(baseY + yWaveOffset));

                // Diagonals
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, -1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(-1, 1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(-1, -1), outlineColor);

                // Cardinals
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 0), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(-1, 0), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(0, 1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(0, -1), outlineColor);

                // Shadow
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 0), shadowColor);

                // Main
                spriteBatch.DrawString(font, charStr, charPos, textColor);
            }
        }
    }
}