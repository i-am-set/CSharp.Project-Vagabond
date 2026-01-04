using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

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
        SmallWave,      // Single pass "hump" wave (Center Aligned Expansion)
        LeftAlignedSmallWave // Single pass "hump" wave (Left Aligned Expansion)
    }

    /// <summary>
    /// Centralized configuration for text animation parameters.
    /// </summary>
    public static class TextAnimationSettings
    {
        public static float WaveSpeed = 5f;
        public static float WaveFrequency = 0.5f;
        public static float WaveAmplitude = 1.0f;

        public static float SmallWaveSpeed = 15f;
        public static float SmallWaveFrequency = 0.5f;
        public static float SmallWaveAmplitude = 2.0f;

        public static float PopSpeed = 6f;
        public static float PopFrequency = 0.4f;
        public static float PopAmplitude = 1.0f;
        public static float PopScaleMin = 0.8f;
        public static float PopScaleMax = 1.2f;

        public static float WobbleSpeed = 3f;
        public static float WobbleFrequency = 0.5f;
        public static float WobbleRotationMagnitude = 0.25f;
        public static float WobbleYAmplitude = 1.0f;

        public static float ShakeSpeed = 30f;
        public static float ShakeAmplitude = 1.0f;

        public static float NervousSpeed = 25f;
        public static float NervousAmplitude = 0.75f;

        public static float RainbowSpeed = 0.5f;
        public static float RainbowFrequency = 0.1f;
        public static float RainbowWaveSpeed = 4f;
        public static float RainbowWaveFreq = 0.5f;
        public static float RainbowWaveAmplitude = 1.0f;

        public static float BounceSpeed = 6f;
        public static float BounceFrequency = 0.5f;
        public static float BounceAmplitude = 2.0f;

        public static float DriftSpeed = 3f;
        public static float DriftFrequency = 0.3f;
        public static float DriftAmplitude = 0.75f;

        public static float GlitchSpeed = 20f;
        public static float GlitchAmplitude = 0.5f;
        public static float GlitchRotation = 0.05f;

        public static float FlickerSpeed = 10f;
        public static float FlickerMinAlpha = 0.3f;
        public static float FlickerMaxAlpha = 1.0f;
    }

    public static class TextUtils
    {
        // --- OPTIMIZATION: String Cache ---
        // Pre-allocate strings for the first 1024 characters (Basic Latin + Extended).
        // This covers almost all standard RPG text, eliminating 'new string()' allocations in the draw loop.
        private static readonly string[] _charStringCache;

        static TextUtils()
        {
            _charStringCache = new string[1024];
            for (int i = 0; i < _charStringCache.Length; i++)
            {
                _charStringCache[i] = ((char)i).ToString();
            }
        }

        private enum OutlineStyle
        {
            None,
            Cross, // 4-way
            Square // 8-way
        }

        /// <summary>
        /// Calculates the duration required for the SmallWave effect to traverse the entire text string once.
        /// </summary>
        public static float GetSmallWaveDuration(int textLength)
        {
            return (textLength * TextAnimationSettings.SmallWaveFrequency + MathHelper.Pi) / TextAnimationSettings.SmallWaveSpeed;
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
                    float waveArg = time * TextAnimationSettings.WaveSpeed + charIndex * TextAnimationSettings.WaveFrequency;
                    offset.Y = MathF.Sin(waveArg) * TextAnimationSettings.WaveAmplitude;
                    break;

                case TextEffectType.SmallWave:
                case TextEffectType.LeftAlignedSmallWave:
                    float smallWaveArg = time * TextAnimationSettings.SmallWaveSpeed - charIndex * TextAnimationSettings.SmallWaveFrequency;
                    if (smallWaveArg > 0 && smallWaveArg < MathHelper.Pi)
                    {
                        offset.Y = -MathF.Sin(smallWaveArg) * TextAnimationSettings.SmallWaveAmplitude;
                    }
                    break;

                case TextEffectType.PopWave:
                    float popArg = time * TextAnimationSettings.PopSpeed + charIndex * TextAnimationSettings.PopFrequency;
                    float sinVal = MathF.Sin(popArg);
                    offset.Y = sinVal * TextAnimationSettings.PopAmplitude;
                    float scalePulse = (sinVal + 1f) * 0.5f;
                    float scaleFactor = MathHelper.Lerp(TextAnimationSettings.PopScaleMin, TextAnimationSettings.PopScaleMax, scalePulse);
                    scale = new Vector2(scaleFactor);
                    break;

                case TextEffectType.Pop:
                    float pArg = time * TextAnimationSettings.PopSpeed + charIndex * TextAnimationSettings.PopFrequency;
                    float pSin = MathF.Sin(pArg);
                    float pPulse = (pSin + 1f) * 0.5f;
                    float pFactor = MathHelper.Lerp(TextAnimationSettings.PopScaleMin, TextAnimationSettings.PopScaleMax, pPulse);
                    scale = new Vector2(pFactor);
                    break;

                case TextEffectType.Wobble:
                    float wobbleArg = time * TextAnimationSettings.WobbleSpeed + charIndex * TextAnimationSettings.WobbleFrequency;
                    rotation = MathF.Sin(wobbleArg) * TextAnimationSettings.WobbleRotationMagnitude;
                    offset.Y = MathF.Cos(wobbleArg) * TextAnimationSettings.WobbleYAmplitude;
                    break;

                case TextEffectType.Shake:
                    float flickerTime = MathF.Floor(time * TextAnimationSettings.ShakeSpeed);
                    float r1 = MathF.Sin(flickerTime * 12.9898f + charIndex * 78.233f) * 43758.5453f;
                    float r2 = MathF.Sin(flickerTime * 39.7867f + charIndex * 12.9898f) * 43758.5453f;
                    float rndX = (r1 - MathF.Floor(r1)) * 2f - 1f;
                    float rndY = (r2 - MathF.Floor(r2)) * 2f - 1f;
                    offset = new Vector2(rndX, rndY) * TextAnimationSettings.ShakeAmplitude;
                    break;

                case TextEffectType.Nervous:
                    float nervousTime = MathF.Floor(time * TextAnimationSettings.NervousSpeed);
                    float n1 = MathF.Sin(nervousTime * 12.9898f + charIndex * 78.233f) * 43758.5453f;
                    float n2 = MathF.Sin(nervousTime * 39.7867f + charIndex * 12.9898f) * 43758.5453f;
                    float nervousRndX = (n1 - MathF.Floor(n1)) * 2f - 1f;
                    float nervousRndY = (n2 - MathF.Floor(n2)) * 2f - 1f;
                    offset = new Vector2(nervousRndX, nervousRndY) * TextAnimationSettings.NervousAmplitude;
                    break;

                case TextEffectType.Rainbow:
                    float hue = (time * TextAnimationSettings.RainbowSpeed + charIndex * TextAnimationSettings.RainbowFrequency) % 1.0f;
                    color = HslToRgb(hue, 0.8f, 0.6f);
                    break;

                case TextEffectType.RainbowWave:
                    float hueW = (time * TextAnimationSettings.RainbowSpeed + charIndex * TextAnimationSettings.RainbowFrequency) % 1.0f;
                    color = HslToRgb(hueW, 0.8f, 0.6f);
                    offset.Y = MathF.Sin(time * TextAnimationSettings.RainbowWaveSpeed + charIndex * TextAnimationSettings.RainbowWaveFreq) * TextAnimationSettings.RainbowWaveAmplitude;
                    break;

                case TextEffectType.Bounce:
                    float bounceArg = time * TextAnimationSettings.BounceSpeed + charIndex * TextAnimationSettings.BounceFrequency;
                    offset.Y = -MathF.Abs(MathF.Sin(bounceArg)) * TextAnimationSettings.BounceAmplitude;
                    break;

                case TextEffectType.Drift:
                    float driftArg = time * TextAnimationSettings.DriftSpeed + charIndex * TextAnimationSettings.DriftFrequency;
                    offset.X = MathF.Sin(driftArg) * TextAnimationSettings.DriftAmplitude;
                    break;

                case TextEffectType.Glitch:
                    float glitchTime = MathF.Floor(time * TextAnimationSettings.GlitchSpeed);
                    float g1 = MathF.Sin(glitchTime * 12.9898f + charIndex) * 43758.5453f;
                    float g2 = MathF.Sin(glitchTime * 93.9898f + charIndex) * 43758.5453f;

                    float glitchNoise1 = g1 - MathF.Floor(g1);
                    float glitchNoise2 = g2 - MathF.Floor(g2);

                    offset.X = (glitchNoise1 * 2f - 1f) * TextAnimationSettings.GlitchAmplitude;
                    offset.Y = (glitchNoise2 * 2f - 1f) * TextAnimationSettings.GlitchAmplitude;
                    rotation = (glitchNoise1 * 2f - 1f) * TextAnimationSettings.GlitchRotation;
                    break;

                case TextEffectType.Flicker:
                    float fTime = MathF.Floor(time * TextAnimationSettings.FlickerSpeed);
                    float f1 = MathF.Sin(fTime * 12.9898f + charIndex) * 43758.5453f;
                    float alphaNoise = (f1 - MathF.Floor(f1));
                    float alpha = MathHelper.Lerp(TextAnimationSettings.FlickerMinAlpha, TextAnimationSettings.FlickerMaxAlpha, alphaNoise);
                    color = baseColor * alpha;
                    break;

                case TextEffectType.DriftBounce:
                    float dbDriftArg = time * TextAnimationSettings.DriftSpeed + charIndex * TextAnimationSettings.DriftFrequency;
                    offset.X = MathF.Sin(dbDriftArg) * TextAnimationSettings.DriftAmplitude;
                    float dbBounceArg = time * TextAnimationSettings.BounceSpeed + charIndex * TextAnimationSettings.BounceFrequency;
                    offset.Y = -MathF.Abs(MathF.Sin(dbBounceArg)) * TextAnimationSettings.BounceAmplitude;
                    break;

                case TextEffectType.DriftWave:
                    float dwDriftArg = time * TextAnimationSettings.DriftSpeed + charIndex * TextAnimationSettings.DriftFrequency;
                    offset.X = MathF.Sin(dwDriftArg) * TextAnimationSettings.DriftAmplitude;
                    float dwWaveArg = time * TextAnimationSettings.WaveSpeed + charIndex * TextAnimationSettings.WaveFrequency;
                    offset.Y = MathF.Sin(dwWaveArg) * TextAnimationSettings.WaveAmplitude;
                    break;

                case TextEffectType.FlickerBounce:
                    float fbTime = MathF.Floor(time * TextAnimationSettings.FlickerSpeed);
                    float fb1 = MathF.Sin(fbTime * 12.9898f + charIndex) * 43758.5453f;
                    float fbAlphaNoise = (fb1 - MathF.Floor(fb1));
                    float fbAlpha = MathHelper.Lerp(TextAnimationSettings.FlickerMinAlpha, TextAnimationSettings.FlickerMaxAlpha, fbAlphaNoise);
                    color = baseColor * fbAlpha;
                    float fbBounceArg = time * TextAnimationSettings.BounceSpeed + charIndex * TextAnimationSettings.BounceFrequency;
                    offset.Y = -MathF.Abs(MathF.Sin(fbBounceArg)) * TextAnimationSettings.BounceAmplitude;
                    break;

                case TextEffectType.FlickerWave:
                    float fwTime = MathF.Floor(time * TextAnimationSettings.FlickerSpeed);
                    float fw1 = MathF.Sin(fwTime * 12.9898f + charIndex) * 43758.5453f;
                    float fwAlphaNoise = (fw1 - MathF.Floor(fw1));
                    float fwAlpha = MathHelper.Lerp(TextAnimationSettings.FlickerMinAlpha, TextAnimationSettings.FlickerMaxAlpha, fwAlphaNoise);
                    color = baseColor * fwAlpha;
                    float fwWaveArg = time * TextAnimationSettings.WaveSpeed + charIndex * TextAnimationSettings.WaveFrequency;
                    offset.Y = MathF.Sin(fwWaveArg) * TextAnimationSettings.WaveAmplitude;
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
        /// </summary>
        public static void DrawTextWithEffect(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, TextEffectType effect, float time, Vector2? baseScale = null)
        {
            DrawTextCore(spriteBatch, font, text, position, color, Color.Transparent, effect, time, baseScale, OutlineStyle.None);
        }

        /// <summary>
        /// Draws text with a specific effect, including a 4-way outline.
        /// </summary>
        public static void DrawTextWithEffectOutlined(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, Color outlineColor, TextEffectType effect, float time, Vector2? baseScale = null)
        {
            DrawTextCore(spriteBatch, font, text, position, color, outlineColor, effect, time, baseScale, OutlineStyle.Cross);
        }

        /// <summary>
        /// Draws text with a specific effect, including a full 8-way square outline.
        /// </summary>
        public static void DrawTextWithEffectSquareOutlined(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, Color outlineColor, TextEffectType effect, float time, Vector2? baseScale = null)
        {
            DrawTextCore(spriteBatch, font, text, position, color, outlineColor, effect, time, baseScale, OutlineStyle.Square);
        }

        /// <summary>
        /// Core drawing logic that iterates glyphs efficiently (O(N)) and handles effects/outlines.
        /// </summary>
        private static void DrawTextCore(
            SpriteBatch spriteBatch,
            BitmapFont font,
            string text,
            Vector2 position,
            Color color,
            Color outlineColor,
            TextEffectType effect,
            float time,
            Vector2? baseScale,
            OutlineStyle outlineStyle)
        {
            if (string.IsNullOrEmpty(text)) return;

            Vector2 layoutScale = baseScale ?? Vector2.One;
            Vector2 drawScaleBase = Vector2.One;
            var shadowColor = new Color(color.R / 4, color.G / 4, color.B / 4, color.A);

            // --- CENTER ALIGNMENT LOGIC ---
            // Calculate the total width of the text to determine the centering offset.
            // If layoutScale is > 1, the text grows.
            // If effect is LeftAlignedSmallWave, we do NOT apply centering offset, so it grows to the right.
            // Otherwise, we shift left by half the growth to grow from center.
            Vector2 centeringOffset = Vector2.Zero;
            if (effect != TextEffectType.LeftAlignedSmallWave)
            {
                Vector2 totalSize = font.MeasureString(text);
                centeringOffset = (totalSize * (Vector2.One - layoutScale)) / 2f;
            }

            var glyphs = font.GetGlyphs(text, position);
            int charIndex = 0;

            // Calculate the vertical center of the line to use as a stable rotation origin.
            float lineCenterY = position.Y + (font.LineHeight / 2f);

            foreach (var glyph in glyphs)
            {
                // Sync charIndex with the glyphs returned.
                while (charIndex < text.Length && text[charIndex] == '\n') charIndex++;
                if (charIndex >= text.Length) break;

                char c = text[charIndex];

                // Optimization: Skip drawing whitespace, but we MUST increment charIndex
                if (char.IsWhiteSpace(c))
                {
                    charIndex++;
                    continue;
                }

                // --- OPTIMIZATION: Use String Cache ---
                // Avoids 'new string()' allocation for every character every frame.
                string charStr;
                if (c < _charStringCache.Length)
                {
                    charStr = _charStringCache[c];
                }
                else
                {
                    charStr = c.ToString(); // Fallback for exotic characters
                }

                // 1. Calculate Layout Position
                Vector2 relativePos = glyph.Position - position;
                // Apply layout scale AND the centering offset
                Vector2 scaledPos = position + (relativePos * layoutScale) + centeringOffset;

                // 2. Calculate Effect Transform
                var (animOffset, effectScale, rotation, finalColor) = GetTextEffectTransform(effect, time, charIndex, color);

                // 3. Calculate Origin (Center of character)
                Vector2 charSize = font.MeasureString(charStr);
                Vector2 origin = new Vector2(charSize.X / 2f, font.LineHeight / 2f);

                // 4. Calculate Final Draw Position
                Vector2 targetCenterPos = new Vector2(scaledPos.X + origin.X, lineCenterY) + animOffset;

                // 5. Pixel Snapping
                Vector2 snappedTopLeft = new Vector2(
                    MathF.Round(targetCenterPos.X - origin.X),
                    MathF.Round(targetCenterPos.Y - origin.Y)
                );
                Vector2 finalDrawPos = snappedTopLeft + origin;

                // 6. Final Draw Scale
                Vector2 finalScale = drawScaleBase * effectScale;

                // Draw Outline / Shadow based on style
                if (outlineStyle == OutlineStyle.Cross)
                {
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(1, 0), outlineColor, rotation, origin, finalScale);
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(-1, 0), outlineColor, rotation, origin, finalScale);
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(0, 1), outlineColor, rotation, origin, finalScale);
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(0, -1), outlineColor, rotation, origin, finalScale);
                }
                else if (outlineStyle == OutlineStyle.Square)
                {
                    // Diagonals
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(1, 1), outlineColor, rotation, origin, finalScale);
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(1, -1), outlineColor, rotation, origin, finalScale);
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(-1, 1), outlineColor, rotation, origin, finalScale);
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(-1, -1), outlineColor, rotation, origin, finalScale);
                    // Cardinals
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(1, 0), outlineColor, rotation, origin, finalScale);
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(-1, 0), outlineColor, rotation, origin, finalScale);
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(0, 1), outlineColor, rotation, origin, finalScale);
                    DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(0, -1), outlineColor, rotation, origin, finalScale);
                }

                // Always draw shadow (Right side depth)
                DrawGlyph(spriteBatch, font, charStr, finalDrawPos + new Vector2(1, 0), shadowColor, rotation, origin, finalScale);

                // Draw Main Character
                DrawGlyph(spriteBatch, font, charStr, finalDrawPos, finalColor, rotation, origin, finalScale);

                charIndex++;
            }
        }

        private static void DrawGlyph(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale)
        {
            spriteBatch.DrawString(font, text, position, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
