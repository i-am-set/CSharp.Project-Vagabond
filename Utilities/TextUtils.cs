using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ProjectVagabond.Utils
{
    public static class TextUtils
    {
        /// <summary>
        /// Calculates the visual transformation for a single character based on a text effect.
        /// </summary>
        /// <param name="effect">The type of animation.</param>
        /// <param name="time">Total game time in seconds.</param>
        /// <param name="charIndex">The index of the character in the string (or global index).</param>
        /// <param name="baseColor">The original color of the text.</param>
        /// <returns>A tuple containing Offset, Scale, Rotation, and Color.</returns>
        public static (Vector2 Offset, Vector2 Scale, float Rotation, Color Color) GetTextEffectTransform(
            TextEffectType effect,
            float time,
            int charIndex,
            Color baseColor)
        {
            Vector2 offset = Vector2.Zero;
            Vector2 scale = Vector2.One;
            float rotation = 0f;
            Color color = baseColor;

            switch (effect)
            {
                case TextEffectType.Wave:
                    // Classic Sine Wave
                    float waveArg = time * 5f + charIndex * 0.5f;
                    offset.Y = MathF.Sin(waveArg) * 3f;
                    break;

                case TextEffectType.PopWave:
                    // Balatro-style: Bounces up and scales up at the peak
                    float popArg = time * 6f + charIndex * 0.4f;
                    float sinVal = MathF.Sin(popArg);

                    offset.Y = sinVal * 4f;

                    // Scale gets larger when moving UP (sinVal < 0 in screen space usually, but let's just use phase)
                    // We want max scale at the "top" of the wave.
                    float scalePulse = (sinVal + 1f) * 0.5f; // 0 to 1
                    float scaleFactor = MathHelper.Lerp(0.8f, 1.3f, scalePulse);
                    scale = new Vector2(scaleFactor);
                    break;

                case TextEffectType.Wobble:
                    // Rotates back and forth
                    float wobbleArg = time * 4f + charIndex * 0.6f;
                    rotation = MathF.Sin(wobbleArg) * 0.2f; // ~11 degrees
                    break;

                case TextEffectType.Shake:
                    // Random jitter. We use a pseudo-random hash based on time steps to make it "flicker"
                    // 15 FPS flicker rate
                    float flickerTime = MathF.Floor(time * 15f);
                    // Simple hash
                    float r1 = MathF.Sin(flickerTime * 12.9898f + charIndex * 78.233f) * 43758.5453f;
                    float r2 = MathF.Sin(flickerTime * 39.7867f + charIndex * 12.9898f) * 43758.5453f;

                    // Extract fractional part for -1 to 1 range
                    float rndX = (r1 - MathF.Floor(r1)) * 2f - 1f;
                    float rndY = (r2 - MathF.Floor(r2)) * 2f - 1f;

                    offset = new Vector2(rndX, rndY) * 1.5f;
                    break;

                case TextEffectType.Nervous:
                    // Fast, small shake + slight rotation
                    float nervousTime = MathF.Floor(time * 20f);
                    float n1 = MathF.Sin(nervousTime * 12.9898f + charIndex) * 43758.5453f;
                    float n2 = MathF.Sin(nervousTime * 93.9898f + charIndex) * 43758.5453f;

                    offset.X = (n1 - MathF.Floor(n1)) * 1f;
                    offset.Y = (n2 - MathF.Floor(n2)) * 1f;
                    rotation = (n1 - MathF.Floor(n1)) * 0.05f;
                    break;

                case TextEffectType.Rainbow:
                    // Cycle Hue
                    float hue = (time * 0.5f + charIndex * 0.1f) % 1.0f;
                    color = HslToRgb(hue, 0.8f, 0.6f);
                    // Add a slight bob
                    offset.Y = MathF.Sin(time * 4f + charIndex * 0.5f) * 2f;
                    break;
            }

            return (offset, scale, rotation, color);
        }

        // Helper for Rainbow effect
        private static Color HslToRgb(float h, float s, float l)
        {
            float r, g, b;

            if (s == 0f)
            {
                r = g = b = l;
            }
            else
            {
                float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
                float p = 2f * l - q;
                r = HueToRgb(p, q, h + 1f / 3f);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1f / 3f);
            }

            return new Color(r, g, b);
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        // --- Existing Methods (Kept for compatibility) ---

        public static void DrawWavedText(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, float waveTimer, float speed, float frequency, float amplitude, int charIndexOffset = 0)
        {
            float startX = position.X;
            float baseY = position.Y;

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

                spriteBatch.DrawStringSnapped(font, charStr, new Vector2(startX + charOffsetX, baseY + yWaveOffset), color);
            }
        }

        public static void DrawWavedTextOutlined(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color textColor, Color outlineColor, float waveTimer, float speed, float frequency, float amplitude, int charIndexOffset = 0)
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

                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 0), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(-1, 0), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(0, 1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(0, -1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 0), shadowColor);
                spriteBatch.DrawString(font, charStr, charPos, textColor);
            }
        }

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

                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, -1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(-1, 1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(-1, -1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 0), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(-1, 0), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(0, 1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(0, -1), outlineColor);
                spriteBatch.DrawString(font, charStr, charPos + new Vector2(1, 0), shadowColor);
                spriteBatch.DrawString(font, charStr, charPos, textColor);
            }
        }
    }
}
