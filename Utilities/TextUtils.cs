using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ProjectVagabond.Utils
{
    public enum TextEffectType
    {
        None,
        Wave,           // Standard Sine Wave (Y offset)
        Shake,          // Random Jitter
        PopWave,        // Wave + Scaling (Balatro style)
        Wobble,         // Sine Wave Rotation
        Nervous,        // Fast, small shake + slight rotation
        Rainbow,        // Color Cycle (No movement)
        RainbowWave,    // Color Cycle + Wave
        Pop,            // Scaling Pulse (No movement)
        Bounce,         // Bouncing ball motion (Absolute Sine)
        Drift,          // Horizontal Sine Wave
        Glitch,         // Chaotic offsets and color tints
        Flicker,        // Opacity pulsing
        DriftBounce,    // Horizontal Drift + Vertical Bounce
        DriftWave,      // Horizontal Drift + Vertical Wave
        FlickerBounce,  // Opacity Pulse + Vertical Bounce
        FlickerWave,    // Opacity Pulse + Vertical Wave
        SmallWave       // Single pass "hump" wave for buttons
    }

    public static class TextUtils
    {
        // --- TUNING PARAMETERS ---

        // Wave
        private const float WAVE_SPEED = 5f;
        private const float WAVE_FREQUENCY = 0.5f;
        private const float WAVE_AMPLITUDE = 1.0f;

        // Small Wave (Button Hover)
        private const float SMALL_WAVE_SPEED = 15f;
        private const float SMALL_WAVE_FREQUENCY = 0.5f;
        private const float SMALL_WAVE_AMPLITUDE = 2.0f;

        // Pop / PopWave
        private const float POP_SPEED = 6f;
        private const float POP_FREQUENCY = 0.4f;
        private const float POP_AMPLITUDE = 1.0f;
        private const float POP_SCALE_MIN = 0.8f;
        private const float POP_SCALE_MAX = 1.2f;

        // Wobble
        private const float WOBBLE_SPEED = 3f;
        private const float WOBBLE_FREQUENCY = 0.5f;
        private const float WOBBLE_ROTATION_MAGNITUDE = 0.25f; // ~20 degrees
        private const float WOBBLE_Y_AMPLITUDE = 1.0f; // Slight vertical float

        // Shake
        private const float SHAKE_SPEED = 30f;
        private const float SHAKE_AMPLITUDE = 1.0f;

        // Nervous
        private const float NERVOUS_SPEED = 25f;
        private const float NERVOUS_AMPLITUDE = 0.75f;

        // Rainbow
        private const float RAINBOW_SPEED = 0.5f;
        private const float RAINBOW_FREQUENCY = 0.1f;
        private const float RAINBOW_WAVE_SPEED = 4f;
        private const float RAINBOW_WAVE_FREQ = 0.5f;
        private const float RAINBOW_WAVE_AMPLITUDE = 1.0f;

        // Bounce
        private const float BOUNCE_SPEED = 6f;
        private const float BOUNCE_FREQUENCY = 0.5f;
        private const float BOUNCE_AMPLITUDE = 2.0f;

        // Drift
        private const float DRIFT_SPEED = 3f;
        private const float DRIFT_FREQUENCY = 0.3f;
        private const float DRIFT_AMPLITUDE = 0.75f;

        // Glitch
        private const float GLITCH_SPEED = 20f; // Faster
        private const float GLITCH_AMPLITUDE = 0.5f; // Visible pixel offset
        private const float GLITCH_ROTATION = 0.05f; // Visible rotation jitter

        // Flicker
        private const float FLICKER_SPEED = 10f;
        private const float FLICKER_MIN_ALPHA = 0.3f;
        private const float FLICKER_MAX_ALPHA = 1.0f;

        /// <summary>
        /// Calculates the duration required for the SmallWave effect to traverse the entire text string once.
        /// Used to create seamless loops.
        /// </summary>
        public static float GetSmallWaveDuration(int textLength)
        {
            // Formula: (Length * Frequency + Pi) / Speed
            // This ensures the sine wave (0 to Pi) has fully cleared the last character.
            return (textLength * SMALL_WAVE_FREQUENCY + MathHelper.Pi) / SMALL_WAVE_SPEED;
        }

        /// <summary>
        /// Calculates the visual transformation for a single character based on a text effect.
        /// </summary>
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
                    float waveArg = time * WAVE_SPEED + charIndex * WAVE_FREQUENCY;
                    offset.Y = MathF.Sin(waveArg) * WAVE_AMPLITUDE;
                    break;

                case TextEffectType.SmallWave:
                    // Single pass hump: sin(arg) where arg is 0..Pi
                    // Note: We subtract index to make it travel left-to-right
                    float smallWaveArg = time * SMALL_WAVE_SPEED - charIndex * SMALL_WAVE_FREQUENCY;
                    if (smallWaveArg > 0 && smallWaveArg < MathHelper.Pi)
                    {
                        offset.Y = -MathF.Sin(smallWaveArg) * SMALL_WAVE_AMPLITUDE;
                    }
                    break;

                case TextEffectType.PopWave:
                    float popArg = time * POP_SPEED + charIndex * POP_FREQUENCY;
                    float sinVal = MathF.Sin(popArg);
                    offset.Y = sinVal * POP_AMPLITUDE;
                    float scalePulse = (sinVal + 1f) * 0.5f;
                    float scaleFactor = MathHelper.Lerp(POP_SCALE_MIN, POP_SCALE_MAX, scalePulse);
                    scale = new Vector2(scaleFactor);
                    break;

                case TextEffectType.Pop:
                    float pArg = time * POP_SPEED + charIndex * POP_FREQUENCY;
                    float pSin = MathF.Sin(pArg);
                    float pPulse = (pSin + 1f) * 0.5f;
                    float pFactor = MathHelper.Lerp(POP_SCALE_MIN, POP_SCALE_MAX, pPulse);
                    scale = new Vector2(pFactor);
                    break;

                case TextEffectType.Wobble:
                    float wobbleArg = time * WOBBLE_SPEED + charIndex * WOBBLE_FREQUENCY;
                    rotation = MathF.Sin(wobbleArg) * WOBBLE_ROTATION_MAGNITUDE;
                    // Add slight vertical movement to make it feel like it's floating
                    offset.Y = MathF.Cos(wobbleArg) * WOBBLE_Y_AMPLITUDE;
                    break;

                case TextEffectType.Shake:
                    float flickerTime = MathF.Floor(time * SHAKE_SPEED);
                    float r1 = MathF.Sin(flickerTime * 12.9898f + charIndex * 78.233f) * 43758.5453f;
                    float r2 = MathF.Sin(flickerTime * 39.7867f + charIndex * 12.9898f) * 43758.5453f;
                    float rndX = (r1 - MathF.Floor(r1)) * 2f - 1f;
                    float rndY = (r2 - MathF.Floor(r2)) * 2f - 1f;
                    offset = new Vector2(rndX, rndY) * SHAKE_AMPLITUDE;
                    break;

                case TextEffectType.Nervous:
                    float nervousTime = MathF.Floor(time * NERVOUS_SPEED);
                    float n1 = MathF.Sin(nervousTime * 12.9898f + charIndex * 78.233f) * 43758.5453f;
                    float n2 = MathF.Sin(nervousTime * 39.7867f + charIndex * 12.9898f) * 43758.5453f;
                    float nervousRndX = (n1 - MathF.Floor(n1)) * 2f - 1f;
                    float nervousRndY = (n2 - MathF.Floor(n2)) * 2f - 1f;
                    offset = new Vector2(nervousRndX, nervousRndY) * NERVOUS_AMPLITUDE;
                    break;

                case TextEffectType.Rainbow:
                    float hue = (time * RAINBOW_SPEED + charIndex * RAINBOW_FREQUENCY) % 1.0f;
                    color = HslToRgb(hue, 0.8f, 0.6f);
                    break;

                case TextEffectType.RainbowWave:
                    float hueW = (time * RAINBOW_SPEED + charIndex * RAINBOW_FREQUENCY) % 1.0f;
                    color = HslToRgb(hueW, 0.8f, 0.6f);
                    offset.Y = MathF.Sin(time * RAINBOW_WAVE_SPEED + charIndex * RAINBOW_WAVE_FREQ) * RAINBOW_WAVE_AMPLITUDE;
                    break;

                case TextEffectType.Bounce:
                    float bounceArg = time * BOUNCE_SPEED + charIndex * BOUNCE_FREQUENCY;
                    offset.Y = -MathF.Abs(MathF.Sin(bounceArg)) * BOUNCE_AMPLITUDE;
                    break;

                case TextEffectType.Drift:
                    float driftArg = time * DRIFT_SPEED + charIndex * DRIFT_FREQUENCY;
                    offset.X = MathF.Sin(driftArg) * DRIFT_AMPLITUDE;
                    break;

                case TextEffectType.Glitch:
                    float glitchTime = MathF.Floor(time * GLITCH_SPEED);
                    float g1 = MathF.Sin(glitchTime * 12.9898f + charIndex) * 43758.5453f;
                    float g2 = MathF.Sin(glitchTime * 93.9898f + charIndex) * 43758.5453f;

                    float glitchNoise1 = g1 - MathF.Floor(g1); // 0 to 1
                    float glitchNoise2 = g2 - MathF.Floor(g2); // 0 to 1

                    // Map 0..1 to -1..1 for centering
                    offset.X = (glitchNoise1 * 2f - 1f) * GLITCH_AMPLITUDE;
                    offset.Y = (glitchNoise2 * 2f - 1f) * GLITCH_AMPLITUDE;
                    rotation = (glitchNoise1 * 2f - 1f) * GLITCH_ROTATION;
                    break;

                case TextEffectType.Flicker:
                    float fTime = MathF.Floor(time * FLICKER_SPEED);
                    float f1 = MathF.Sin(fTime * 12.9898f + charIndex) * 43758.5453f;
                    float alphaNoise = (f1 - MathF.Floor(f1));
                    float alpha = MathHelper.Lerp(FLICKER_MIN_ALPHA, FLICKER_MAX_ALPHA, alphaNoise);
                    color = baseColor * alpha;
                    break;

                case TextEffectType.DriftBounce:
                    float dbDriftArg = time * DRIFT_SPEED + charIndex * DRIFT_FREQUENCY;
                    offset.X = MathF.Sin(dbDriftArg) * DRIFT_AMPLITUDE;
                    float dbBounceArg = time * BOUNCE_SPEED + charIndex * BOUNCE_FREQUENCY;
                    offset.Y = -MathF.Abs(MathF.Sin(dbBounceArg)) * BOUNCE_AMPLITUDE;
                    break;

                case TextEffectType.DriftWave:
                    float dwDriftArg = time * DRIFT_SPEED + charIndex * DRIFT_FREQUENCY;
                    offset.X = MathF.Sin(dwDriftArg) * DRIFT_AMPLITUDE;
                    float dwWaveArg = time * WAVE_SPEED + charIndex * WAVE_FREQUENCY;
                    offset.Y = MathF.Sin(dwWaveArg) * WAVE_AMPLITUDE;
                    break;

                case TextEffectType.FlickerBounce:
                    float fbTime = MathF.Floor(time * FLICKER_SPEED);
                    float fb1 = MathF.Sin(fbTime * 12.9898f + charIndex) * 43758.5453f;
                    float fbAlphaNoise = (fb1 - MathF.Floor(fb1));
                    float fbAlpha = MathHelper.Lerp(FLICKER_MIN_ALPHA, FLICKER_MAX_ALPHA, fbAlphaNoise);
                    color = baseColor * fbAlpha;
                    float fbBounceArg = time * BOUNCE_SPEED + charIndex * BOUNCE_FREQUENCY;
                    offset.Y = -MathF.Abs(MathF.Sin(fbBounceArg)) * BOUNCE_AMPLITUDE;
                    break;

                case TextEffectType.FlickerWave:
                    float fwTime = MathF.Floor(time * FLICKER_SPEED);
                    float fw1 = MathF.Sin(fwTime * 12.9898f + charIndex) * 43758.5453f;
                    float fwAlphaNoise = (fw1 - MathF.Floor(fw1));
                    float fwAlpha = MathHelper.Lerp(FLICKER_MIN_ALPHA, FLICKER_MAX_ALPHA, fwAlphaNoise);
                    color = baseColor * fwAlpha;
                    float fwWaveArg = time * WAVE_SPEED + charIndex * WAVE_FREQUENCY;
                    offset.Y = MathF.Sin(fwWaveArg) * WAVE_AMPLITUDE;
                    break;
            }

            return (offset, scale, rotation, color);
        }

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

        /// <summary>
        /// Draws text with a specific effect applied to each character.
        /// Replaces the old DrawWavedText with a unified system.
        /// </summary>
        public static void DrawTextWithEffect(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, TextEffectType effect, float time, Vector2? baseScale = null)
        {
            float startX = position.X;
            float baseY = position.Y;
            Vector2 scaleFactor = baseScale ?? Vector2.One;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string charStr = c.ToString();
                string sub = text.Substring(0, i);

                // Sentinel trick for correct spacing, scaled by the base scale
                float charOffsetX = (font.MeasureString(sub + "|").Width - font.MeasureString("|").Width) * scaleFactor.X;

                var (offset, effectScale, rotation, finalColor) = GetTextEffectTransform(effect, time, i, color);

                // Apply base scale to the offset as well if needed, though usually offset is absolute pixels.
                // Let's keep offset absolute for now, but position is scaled.
                Vector2 pos = new Vector2(startX + charOffsetX, baseY) + offset;

                // Round to nearest pixel to match DrawStringSnapped behavior and prevent sub-pixel blur
                pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));

                Vector2 origin = font.MeasureString(charStr) / 2f;

                // Combine effect scale with base scale
                Vector2 finalScale = effectScale * scaleFactor;

                // Draw Shadow (Right side outline)
                var shadowColor = new Color(finalColor.R / 4, finalColor.G / 4, finalColor.B / 4, finalColor.A);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(1, 0), shadowColor, rotation, origin, finalScale, SpriteEffects.None, 0f);

                // Draw Main Text
                // Draw centered on the character position to support rotation/scale
                spriteBatch.DrawString(font, charStr, pos + origin, finalColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// Draws text with a specific effect, including a 4-way outline.
        /// Used for UI elements that require high contrast (like Inventory Info Panel).
        /// </summary>
        public static void DrawTextWithEffectOutlined(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, Color outlineColor, TextEffectType effect, float time, Vector2? baseScale = null)
        {
            float startX = position.X;
            float baseY = position.Y;
            Vector2 scaleFactor = baseScale ?? Vector2.One;
            var shadowColor = new Color(color.R / 4, color.G / 4, color.B / 4, color.A);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string charStr = c.ToString();
                string sub = text.Substring(0, i);

                float charOffsetX = (font.MeasureString(sub + "|").Width - font.MeasureString("|").Width) * scaleFactor.X;

                var (offset, effectScale, rotation, finalColor) = GetTextEffectTransform(effect, time, i, color);

                Vector2 pos = new Vector2(startX + charOffsetX, baseY) + offset;
                pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));

                Vector2 origin = font.MeasureString(charStr) / 2f;
                Vector2 finalScale = effectScale * scaleFactor;

                // Draw 4-way Outline
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(1, 0), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(-1, 0), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(0, 1), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(0, -1), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);

                // Draw Shadow
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(1, 0), shadowColor, rotation, origin, finalScale, SpriteEffects.None, 0f);

                // Draw Main Text
                spriteBatch.DrawString(font, charStr, pos + origin, finalColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// Draws text with a specific effect, including a full 8-way square outline.
        /// Used for battle targeting text.
        /// </summary>
        public static void DrawTextWithEffectSquareOutlined(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, Color outlineColor, TextEffectType effect, float time, Vector2? baseScale = null)
        {
            float startX = position.X;
            float baseY = position.Y;
            Vector2 scaleFactor = baseScale ?? Vector2.One;
            var shadowColor = new Color(color.R / 4, color.G / 4, color.B / 4, color.A);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string charStr = c.ToString();
                string sub = text.Substring(0, i);

                float charOffsetX = (font.MeasureString(sub + "|").Width - font.MeasureString("|").Width) * scaleFactor.X;

                var (offset, effectScale, rotation, finalColor) = GetTextEffectTransform(effect, time, i, color);

                Vector2 pos = new Vector2(startX + charOffsetX, baseY) + offset;
                pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));

                Vector2 origin = font.MeasureString(charStr) / 2f;
                Vector2 finalScale = effectScale * scaleFactor;

                // Draw 8-way Outline
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(1, 1), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(1, -1), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(-1, 1), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(-1, -1), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(1, 0), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(-1, 0), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(0, 1), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(0, -1), outlineColor, rotation, origin, finalScale, SpriteEffects.None, 0f);

                // Draw Shadow
                spriteBatch.DrawString(font, charStr, pos + origin + new Vector2(1, 0), shadowColor, rotation, origin, finalScale, SpriteEffects.None, 0f);

                // Draw Main Text
                spriteBatch.DrawString(font, charStr, pos + origin, finalColor, rotation, origin, finalScale, SpriteEffects.None, 0f);
            }
        }
    }
}