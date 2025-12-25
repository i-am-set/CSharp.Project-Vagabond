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
        public static void DrawWavedText(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, float waveTimer, float speed, float frequency, float amplitude)
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
                float waveArg = waveTimer * speed - i * frequency;

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
    }
}
