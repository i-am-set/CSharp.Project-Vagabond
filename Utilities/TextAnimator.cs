using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// Defines the specific animation pattern applied to text characters.
    /// </summary>
    public enum TextEffectType
    {
        None,
        Wave, // Standard Sine Wave (Y offset)
        Shake, // Random Jitter
        PopWave, // Wave + Scaling (Balatro style)
        Wobble, // Sine Wave Rotation
        Nervous, // Fast, small shake + slight rotation
        Rainbow, // Color Cycle (No movement)
        RainbowWave, // Color Cycle + Wave
        Pop, // Scaling Pulse (No movement)
        Bounce, // Bouncing ball motion (Absolute Sine)
        Drift, // Horizontal Sine Wave
        Glitch, // Chaotic offsets and color tints
        Flicker, // Opacity pulsing
        DriftBounce, // Horizontal Drift + Vertical Bounce
        DriftWave, // Horizontal Drift + Vertical Wave
        FlickerBounce, // Opacity Pulse + Vertical Bounce
        FlickerWave, // Opacity Pulse + Vertical Wave
        SmallWave, // Single pass "hump" wave (Center Aligned Expansion)
        LeftAlignedSmallWave, // Single pass "hump" wave (Left-to-Right)
        RightAlignedSmallWave // Single pass "hump" wave (Right-to-Left)
    }
    /// <summary>
    /// Parameter object to simplify Draw calls.
    /// </summary>
    public struct TextDrawOptions
    {
        public Vector2 Position;
        public Color Color;
        public Color OutlineColor;
        public TextEffectType Effect;
        public float Time;
        public Vector2 Scale;
        public bool UseOutline;
        public bool UseSquareOutline;

        public static TextDrawOptions Default(Vector2 position, Color color)
        {
            return new TextDrawOptions
            {
                Position = position,
                Color = color,
                Scale = Vector2.One,
                Effect = TextEffectType.None,
                Time = 0f,
                UseOutline = false,
                OutlineColor = Color.Black,
                UseSquareOutline = false
            };
        }
    }

    /// <summary>
    /// Centralized configuration for text animation parameters.
    /// </summary>
    public static class TextAnimationSettings
    {
        // Wave
        public static float WaveSpeed = 5f;
        public static float WaveFrequency = 0.5f;
        public static float WaveAmplitude = 1.0f;

        // Small Wave
        public static float SmallWaveSpeed = 10f;
        public static float SmallWaveFrequency = 0.5f;
        public static float SmallWaveAmplitude = 2.0f;

        // Pop
        public static float PopSpeed = 6f;
        public static float PopFrequency = 0.4f;
        public static float PopAmplitude = 1.0f;
        public static float PopScaleMin = 0.8f;
        public static float PopScaleMax = 1.2f;

        // Wobble
        public static float WobbleSpeed = 3f;
        public static float WobbleFrequency = 0.5f;
        public static float WobbleRotationMagnitude = 0.25f;
        public static float WobbleYAmplitude = 1.0f;

        // Shake
        public static float ShakeSpeed = 30f;
        public static float ShakeAmplitude = 1.0f;

        // Nervous
        public static float NervousSpeed = 25f;
        public static float NervousAmplitude = 0.75f;

        // Rainbow
        public static float RainbowSpeed = 0.5f;
        public static float RainbowFrequency = 0.1f;
        public static float RainbowWaveSpeed = 4f;
        public static float RainbowWaveFreq = 0.5f;
        public static float RainbowWaveAmplitude = 1.0f;

        // Bounce
        public static float BounceSpeed = 6f;
        public static float BounceFrequency = 0.5f;
        public static float BounceAmplitude = 2.0f;

        // Drift
        public static float DriftSpeed = 3f;
        public static float DriftFrequency = 0.3f;
        public static float DriftAmplitude = 0.75f;

        // Glitch
        public static float GlitchSpeed = 20f;
        public static float GlitchAmplitude = 0.5f;
        public static float GlitchRotation = 0.05f;

        // Flicker
        public static float FlickerSpeed = 10f;
        public static float FlickerMinAlpha = 0.3f;
        public static float FlickerMaxAlpha = 1.0f;
    }

    /// <summary>
    /// Handles rendering of rich text with per-character animations.
    /// </summary>
    public static class TextAnimator
    {
        public static void ClearFontCache()
        {
            // No-op, caches removed
        }

        public static float GetSmallWaveDuration(int textLength)
        {
            return (textLength * TextAnimationSettings.SmallWaveFrequency + MathHelper.Pi) / TextAnimationSettings.SmallWaveSpeed;
        }

        /// <summary>
        /// Calculates the transform for a single character based on the active text effect.
        /// </summary>
        public static (Vector2 Offset, Vector2 Scale, float Rotation, Color Color) GetTextEffectTransform(
            TextEffectType effect,
            float time,
            int charIndex,
            Color baseColor,
            int textLength = 0) // Added optional textLength parameter
        {
            Vector2 offset = Vector2.Zero;
            Vector2 scale = Vector2.One;
            float rotation = 0f;
            Color color = baseColor;

            switch (effect)
            {
                case TextEffectType.Wave:
                    offset.Y = MathF.Sin(time * TextAnimationSettings.WaveSpeed + charIndex * TextAnimationSettings.WaveFrequency) * TextAnimationSettings.WaveAmplitude;
                    break;

                case TextEffectType.SmallWave:
                case TextEffectType.LeftAlignedSmallWave:
                    float smallWaveArg = time * TextAnimationSettings.SmallWaveSpeed - charIndex * TextAnimationSettings.SmallWaveFrequency;
                    if (smallWaveArg > 0 && smallWaveArg < MathHelper.Pi)
                    {
                        offset.Y = -MathF.Sin(smallWaveArg) * TextAnimationSettings.SmallWaveAmplitude;
                    }
                    break;

                case TextEffectType.RightAlignedSmallWave:
                    if (textLength > 0)
                    {
                        // Invert the index logic: Start from the end (textLength - 1)
                        // Delay = (Length - 1 - Index) * Freq
                        // So Index = Length-1 has 0 delay. Index = 0 has max delay.
                        float delay = (textLength - 1 - charIndex) * TextAnimationSettings.SmallWaveFrequency;
                        float rightWaveArg = time * TextAnimationSettings.SmallWaveSpeed - delay;

                        if (rightWaveArg > 0 && rightWaveArg < MathHelper.Pi)
                        {
                            offset.Y = -MathF.Sin(rightWaveArg) * TextAnimationSettings.SmallWaveAmplitude;
                        }
                    }
                    else
                    {
                        // Fallback to LeftAligned if length is unknown
                        float fallbackArg = time * TextAnimationSettings.SmallWaveSpeed - charIndex * TextAnimationSettings.SmallWaveFrequency;
                        if (fallbackArg > 0 && fallbackArg < MathHelper.Pi)
                        {
                            offset.Y = -MathF.Sin(fallbackArg) * TextAnimationSettings.SmallWaveAmplitude;
                        }
                    }
                    break;

                case TextEffectType.PopWave:
                    float popArg = time * TextAnimationSettings.PopSpeed + charIndex * TextAnimationSettings.PopFrequency;
                    float sinVal = MathF.Sin(popArg);
                    offset.Y = sinVal * TextAnimationSettings.PopAmplitude;
                    scale = new Vector2(MathHelper.Lerp(TextAnimationSettings.PopScaleMin, TextAnimationSettings.PopScaleMax, (sinVal + 1f) * 0.5f));
                    break;

                case TextEffectType.Pop:
                    float pArg = time * TextAnimationSettings.PopSpeed + charIndex * TextAnimationSettings.PopFrequency;
                    scale = new Vector2(MathHelper.Lerp(TextAnimationSettings.PopScaleMin, TextAnimationSettings.PopScaleMax, (MathF.Sin(pArg) + 1f) * 0.5f));
                    break;

                case TextEffectType.Wobble:
                    float wobbleArg = time * TextAnimationSettings.WobbleSpeed + charIndex * TextAnimationSettings.WobbleFrequency;
                    rotation = MathF.Sin(wobbleArg) * TextAnimationSettings.WobbleRotationMagnitude;
                    offset.Y = MathF.Cos(wobbleArg) * TextAnimationSettings.WobbleYAmplitude;
                    break;

                case TextEffectType.Shake:
                    float shakeTime = MathF.Floor(time * TextAnimationSettings.ShakeSpeed);
                    float r1 = MathF.Sin(shakeTime * 12.9898f + charIndex * 78.233f);
                    float r2 = MathF.Sin(shakeTime * 39.7867f + charIndex * 12.9898f);
                    offset = new Vector2((r1 - MathF.Floor(r1)) * 2f - 1f, (r2 - MathF.Floor(r2)) * 2f - 1f) * TextAnimationSettings.ShakeAmplitude;
                    break;

                case TextEffectType.Nervous:
                    float nervTime = MathF.Floor(time * TextAnimationSettings.NervousSpeed);
                    float n1 = MathF.Sin(nervTime * 12.9898f + charIndex * 78.233f);
                    float n2 = MathF.Sin(nervTime * 39.7867f + charIndex * 12.9898f);
                    offset = new Vector2((n1 - MathF.Floor(n1)) * 2f - 1f, (n2 - MathF.Floor(n2)) * 2f - 1f) * TextAnimationSettings.NervousAmplitude;
                    break;

                case TextEffectType.Rainbow:
                    color = HslToRgb((time * TextAnimationSettings.RainbowSpeed + charIndex * TextAnimationSettings.RainbowFrequency) % 1.0f, 0.8f, 0.6f);
                    break;

                case TextEffectType.RainbowWave:
                    color = HslToRgb((time * TextAnimationSettings.RainbowSpeed + charIndex * TextAnimationSettings.RainbowFrequency) % 1.0f, 0.8f, 0.6f);
                    offset.Y = MathF.Sin(time * TextAnimationSettings.RainbowWaveSpeed + charIndex * TextAnimationSettings.RainbowWaveFreq) * TextAnimationSettings.RainbowWaveAmplitude;
                    break;

                case TextEffectType.Bounce:
                    offset.Y = -MathF.Abs(MathF.Sin(time * TextAnimationSettings.BounceSpeed + charIndex * TextAnimationSettings.BounceFrequency)) * TextAnimationSettings.BounceAmplitude;
                    break;

                case TextEffectType.Drift:
                    offset.X = MathF.Sin(time * TextAnimationSettings.DriftSpeed + charIndex * TextAnimationSettings.DriftFrequency) * TextAnimationSettings.DriftAmplitude;
                    break;

                case TextEffectType.Glitch:
                    float gTime = MathF.Floor(time * TextAnimationSettings.GlitchSpeed);
                    float g1 = MathF.Sin(gTime * 12.9898f + charIndex);
                    float gNoise = g1 - MathF.Floor(g1);
                    offset = new Vector2(gNoise * 2f - 1f) * TextAnimationSettings.GlitchAmplitude;
                    rotation = (gNoise * 2f - 1f) * TextAnimationSettings.GlitchRotation;
                    break;

                case TextEffectType.Flicker:
                    float fTime = MathF.Floor(time * TextAnimationSettings.FlickerSpeed);
                    float f1 = MathF.Sin(fTime * 12.9898f + charIndex);
                    color = baseColor * MathHelper.Lerp(TextAnimationSettings.FlickerMinAlpha, TextAnimationSettings.FlickerMaxAlpha, f1 - MathF.Floor(f1));
                    break;

                case TextEffectType.DriftBounce:
                    offset.X = MathF.Sin(time * TextAnimationSettings.DriftSpeed + charIndex * TextAnimationSettings.DriftFrequency) * TextAnimationSettings.DriftAmplitude;
                    offset.Y = -MathF.Abs(MathF.Sin(time * TextAnimationSettings.BounceSpeed + charIndex * TextAnimationSettings.BounceFrequency)) * TextAnimationSettings.BounceAmplitude;
                    break;

                case TextEffectType.DriftWave:
                    offset.X = MathF.Sin(time * TextAnimationSettings.DriftSpeed + charIndex * TextAnimationSettings.DriftFrequency) * TextAnimationSettings.DriftAmplitude;
                    offset.Y = MathF.Sin(time * TextAnimationSettings.WaveSpeed + charIndex * TextAnimationSettings.WaveFrequency) * TextAnimationSettings.WaveAmplitude;
                    break;

                case TextEffectType.FlickerBounce:
                    float fbTime = MathF.Floor(time * TextAnimationSettings.FlickerSpeed);
                    float fb1 = MathF.Sin(fbTime * 12.9898f + charIndex);
                    color = baseColor * MathHelper.Lerp(TextAnimationSettings.FlickerMinAlpha, TextAnimationSettings.FlickerMaxAlpha, fb1 - MathF.Floor(fb1));
                    offset.Y = -MathF.Abs(MathF.Sin(time * TextAnimationSettings.BounceSpeed + charIndex * TextAnimationSettings.BounceFrequency)) * TextAnimationSettings.BounceAmplitude;
                    break;

                case TextEffectType.FlickerWave:
                    float fwTime = MathF.Floor(time * TextAnimationSettings.FlickerSpeed);
                    float fw1 = MathF.Sin(fwTime * 12.9898f + charIndex);
                    color = baseColor * MathHelper.Lerp(TextAnimationSettings.FlickerMinAlpha, TextAnimationSettings.FlickerMaxAlpha, fw1 - MathF.Floor(fw1));
                    offset.Y = MathF.Sin(time * TextAnimationSettings.WaveSpeed + charIndex * TextAnimationSettings.WaveFrequency) * TextAnimationSettings.WaveAmplitude;
                    break;
            }

            return (offset, scale, rotation, color);
        }

        private static Color HslToRgb(float h, float s, float l)
        {
            if (s == 0f) return new Color(l, l, l);
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;
            return new Color(HueToRgb(p, q, h + 1f / 3f), HueToRgb(p, q, h), HueToRgb(p, q, h - 1f / 3f));
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

        // --- Public Draw Methods ---

        public static void DrawTextWithEffect(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, TextEffectType effect, float time, Vector2? baseScale = null)
        {
            var opts = TextDrawOptions.Default(position, color);
            opts.Effect = effect;
            opts.Time = time;
            opts.Scale = baseScale ?? Vector2.One;
            DrawTextCore(spriteBatch, font, text, opts);
        }

        public static void DrawTextWithEffectOutlined(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, Color outlineColor, TextEffectType effect, float time, Vector2? baseScale = null)
        {
            var opts = TextDrawOptions.Default(position, color);
            opts.Effect = effect;
            opts.Time = time;
            opts.Scale = baseScale ?? Vector2.One;
            opts.UseOutline = true;
            opts.OutlineColor = outlineColor;
            opts.UseSquareOutline = false;
            DrawTextCore(spriteBatch, font, text, opts);
        }

        public static void DrawTextWithEffectSquareOutlined(SpriteBatch spriteBatch, BitmapFont font, string text, Vector2 position, Color color, Color outlineColor, TextEffectType effect, float time, Vector2? baseScale = null)
        {
            var opts = TextDrawOptions.Default(position, color);
            opts.Effect = effect;
            opts.Time = time;
            opts.Scale = baseScale ?? Vector2.One;
            opts.UseOutline = true;
            opts.OutlineColor = outlineColor;
            opts.UseSquareOutline = true;
            DrawTextCore(spriteBatch, font, text, opts);
        }

        private static void DrawTextCore(SpriteBatch spriteBatch, BitmapFont font, string text, TextDrawOptions options)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Round the base position to prevent sub-pixel jitter
            options.Position = new Vector2(MathF.Round(options.Position.X), MathF.Round(options.Position.Y));

            Vector2 layoutScale = options.Scale;
            var shadowColor = new Color(options.Color.R / 4, options.Color.G / 4, options.Color.B / 4, options.Color.A);

            Vector2 centeringOffset = Vector2.Zero;
            // Don't center offset for aligned waves to keep anchor point stable
            if (options.Effect != TextEffectType.LeftAlignedSmallWave && options.Effect != TextEffectType.RightAlignedSmallWave)
            {
                Vector2 totalSize = font.MeasureString(text);
                centeringOffset = (totalSize * (Vector2.One - layoutScale)) / 2f;
                centeringOffset = new Vector2(MathF.Round(centeringOffset.X), MathF.Round(centeringOffset.Y));
            }

            var glyphs = font.GetGlyphs(text, options.Position);
            int charIndex = 0;

            foreach (var glyph in glyphs)
            {
                while (charIndex < text.Length && text[charIndex] == '\n') charIndex++;
                if (charIndex >= text.Length) break;

                char c = text[charIndex];
                if (char.IsWhiteSpace(c))
                {
                    charIndex++;
                    continue;
                }

                // Calculate relative position from the start of the string
                Vector2 relativePos = glyph.Position - options.Position;

                // Round relative position to lock character spacing to the pixel grid
                relativePos = new Vector2(MathF.Round(relativePos.X), MathF.Round(relativePos.Y));

                // Apply layout scaling and centering
                Vector2 scaledPos = options.Position + (relativePos * layoutScale) + centeringOffset;

                // Get animation transform, passing text length
                var (animOffset, effectScale, rotation, finalColor) = GetTextEffectTransform(options.Effect, options.Time, charIndex, options.Color, text.Length);

                // Get the texture region for the glyph
                var character = glyph.Character;
                if (character != null)
                {
                    var region = character.TextureRegion;
                    if (region != null)
                    {
                        // Calculate origin as the center of the glyph texture
                        Vector2 origin = new Vector2(region.Width / 2f, region.Height / 2f);

                        // Calculate the draw position (Center of the glyph)
                        // glyph.Position is Top-Left. We want Center.
                        // So Base Center = scaledPos + origin.
                        Vector2 targetCenter = scaledPos + origin + animOffset;

                        // Snap the center position to integer coordinates to prevent jitter
                        // We snap the Top-Left equivalent to ensure the texture pixels align with screen pixels
                        Vector2 snappedTopLeft = new Vector2(MathF.Round(targetCenter.X - origin.X), MathF.Round(targetCenter.Y - origin.Y));
                        Vector2 finalDrawPos = snappedTopLeft + origin;

                        Vector2 finalScale = layoutScale * effectScale;

                        if (options.UseOutline)
                        {
                            if (options.UseSquareOutline)
                            {
                                DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos + new Vector2(1, 1), options.OutlineColor, rotation, origin, finalScale);
                                DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos + new Vector2(1, -1), options.OutlineColor, rotation, origin, finalScale);
                                DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos + new Vector2(-1, 1), options.OutlineColor, rotation, origin, finalScale);
                                DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos + new Vector2(-1, -1), options.OutlineColor, rotation, origin, finalScale);
                            }
                            DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos + new Vector2(1, 0), options.OutlineColor, rotation, origin, finalScale);
                            DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos + new Vector2(-1, 0), options.OutlineColor, rotation, origin, finalScale);
                            DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos + new Vector2(0, 1), options.OutlineColor, rotation, origin, finalScale);
                            DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos + new Vector2(0, -1), options.OutlineColor, rotation, origin, finalScale);
                        }

                        // Shadow
                        DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos + new Vector2(1, 0), shadowColor, rotation, origin, finalScale);

                        // Main Character
                        DrawGlyph(spriteBatch, region.Texture, region.Bounds, finalDrawPos, finalColor, rotation, origin, finalScale);
                    }
                }

                charIndex++;
            }
        }

        private static void DrawGlyph(SpriteBatch spriteBatch, Texture2D texture, Rectangle sourceRect, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale)
        {
            spriteBatch.Draw(texture, position, sourceRect, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}