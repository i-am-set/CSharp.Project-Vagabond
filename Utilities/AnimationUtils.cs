using Microsoft.Xna.Framework;
using System;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Defines the mathematical curve used for entry and exit animations.
    /// </summary>
    public enum EntryExitStyle
    {
        Pop, // Scale 0->1 with overshoot
        Fade, // Opacity 0->1
        SlideUp, // Moves up from an offset
        SlideDown, // Moves down from an offset
        Zoom, // Scale 0->1 linear (no overshoot)
        PopJiggle, // Scale 0->1 with overshoot AND a damped rotation wiggle
        SwoopLeft, // Slides in from left
        SwoopRight, // Slides in from right
        JuicyCollect // Scale Up (Anticipation) -> Scale Down (Vanish)
    }
    /// <summary>
    /// Defines the pattern for Idle animations.
    /// </summary>
    public enum IdleAnimationType
    {
        None,
        Bob,        // Up and down movement
        Breathe,    // Subtle scaling
        Pulse,      // Opacity pulse
        Shake       // Subtle jitter
    }

    /// <summary>
    /// Defines the pattern for Hover animations.
    /// </summary>
    public enum HoverAnimationType
    {
        None,
        Lift,           // Moves up slightly (Standard UIAnimator)
        ScaleUp,        // Grows slightly (Standard UIAnimator)
        Wiggle,         // Rotates back and forth (Standard UIAnimator)
        Juicy,          // Balatro-style: Scale Up + Lift + Subtle Rotation

        // --- Legacy / Button.cs Support ---
        Hop,            // A quick "hop" to the right and back.
        SlideAndHold,   // Slides to the right and holds the position until unhovered.
        Scale           // Scales up elastically (Legacy alias for ScaleUp).
    }

    /// <summary>
    /// General purpose math and transform calculations for animations.
    /// Decoupled from specific rendering implementations.
    /// </summary>
    public static class AnimationUtils
    {
        /// <summary>
        /// Calculates the visual state (Scale, Opacity, Offset, Rotation) for an element based on its animation progress.
        /// </summary>
        /// <param name="style">The style of animation.</param>
        /// <param name="progress">0.0 (Start) to 1.0 (End).</param>
        /// <param name="isEntering">True if appearing, False if disappearing.</param>
        /// <param name="magnitude">The distance to slide for slide effects.</param>
        /// <param name="seed">A random seed value (0-1 or similar) to randomize shakes/wiggles.</param>
        /// <returns>A tuple containing Scale, Opacity, Position Offset, and Rotation.</returns>
        public static (Vector2 Scale, float Opacity, Vector2 Offset, float Rotation) CalculateEntryExitTransform(
            EntryExitStyle style,
            float progress,
            bool isEntering,
            float magnitude = 20f,
            float seed = 0f)
        {
            float t = Math.Clamp(progress, 0f, 1f);
            Vector2 scale = Vector2.One;
            float opacity = 1f;
            Vector2 offset = Vector2.Zero;
            float rotation = 0f;

            switch (style)
            {
                case EntryExitStyle.Pop:
                    float s = isEntering ? Easing.EaseOutBack(t) : 1.0f - Easing.EaseInBack(t);
                    scale = new Vector2(s);
                    opacity = isEntering ? t : 1.0f - t;
                    break;

                case EntryExitStyle.PopJiggle:
                    if (isEntering)
                    {
                        scale = new Vector2(Easing.EaseOutBack(t));
                        opacity = t;
                        // Damped sine wave rotation
                        float angle = t * 10f;
                        float decay = 1.0f - t;
                        rotation = MathF.Sin(angle) * 0.15f * decay;
                    }
                    else
                    {
                        scale = new Vector2(1.0f - Easing.EaseInBack(t));
                        opacity = 1.0f - t;
                    }
                    break;

                case EntryExitStyle.Fade:
                    opacity = isEntering ? t : (1.0f - t);
                    break;

                case EntryExitStyle.Zoom:
                    float zoomS = isEntering ? Easing.EaseOutCubic(t) : (1.0f - Easing.EaseInCubic(t));
                    scale = new Vector2(zoomS);
                    opacity = isEntering ? t : (1.0f - t);
                    break;

                case EntryExitStyle.SlideUp:
                    offset.Y = isEntering
                        ? MathHelper.Lerp(magnitude, 0f, Easing.EaseOutCubic(t))
                        : MathHelper.Lerp(0f, -magnitude, Easing.EaseInCubic(t));
                    opacity = isEntering ? t : 1.0f - t;
                    break;

                case EntryExitStyle.SlideDown:
                    offset.Y = isEntering
                        ? MathHelper.Lerp(-magnitude, 0f, Easing.EaseOutCubic(t))
                        : MathHelper.Lerp(0f, magnitude, Easing.EaseInCubic(t));
                    opacity = isEntering ? t : 1.0f - t;
                    break;

                case EntryExitStyle.SwoopLeft:
                    offset.X = isEntering
                        ? MathHelper.Lerp(-magnitude, 0f, Easing.EaseOutCubic(t))
                        : MathHelper.Lerp(0f, -magnitude, Easing.EaseInCubic(t));
                    opacity = isEntering ? t : 1.0f - t;
                    break;

                case EntryExitStyle.SwoopRight:
                    offset.X = isEntering
                        ? MathHelper.Lerp(magnitude, 0f, Easing.EaseOutCubic(t))
                        : MathHelper.Lerp(0f, magnitude, Easing.EaseInCubic(t));
                    opacity = isEntering ? t : 1.0f - t;
                    break;

                case EntryExitStyle.JuicyCollect:
                    if (isEntering)
                    {
                        // Standard pop in
                        scale = new Vector2(Easing.EaseOutBack(t));
                        opacity = t;
                    }
                    else
                    {
                        // Exit: Violent Shake + Rapid Shrink
                        // No growing anticipation. Just snap away.

                        // Scale: 1.0 -> 0.0 (EaseInBack for a slight "suck in" feel, or EaseInQuart for speed)
                        float sVal = MathHelper.Lerp(1.0f, 0.0f, Easing.EaseInBack(t));
                        scale = new Vector2(sVal);

                        // Opacity: Fade out near the end
                        opacity = 1.0f - Easing.EaseInQuad(t);

                        // Shake: Violent shake that decays as t goes 0->1
                        float shakeIntensity = 1f; // Max pixels to shake
                        float shakeDecay = 1.0f - t; // Linear decay
                        float shakeFrequency = 30f; // Very fast vibration

                        // Use seed to randomize X vs Y phase
                        offset.X = MathF.Sin(t * shakeFrequency + seed) * shakeIntensity * shakeDecay;
                        offset.Y = MathF.Cos(t * shakeFrequency * 0.9f + seed) * shakeIntensity * shakeDecay;
                    }
                    break;
            }

            return (scale, opacity, offset, rotation);
        }

        public static Vector2 CalculateIdleOffset(IdleAnimationType type, float time, float magnitude = 2f, float speed = 4f)
        {
            switch (type)
            {
                case IdleAnimationType.Bob:
                    return new Vector2(0, MathF.Sin(time * speed) * magnitude);
                case IdleAnimationType.Shake:
                    // Simple pseudo-random shake based on time
                    return new Vector2(
                        MathF.Sin(time * speed * 1.5f) * magnitude,
                        MathF.Cos(time * speed) * magnitude
                    );
                default:
                    return Vector2.Zero;
            }
        }

        public static Vector2 CalculateIdleScale(IdleAnimationType type, float time, float magnitude = 0.05f, float speed = 3f)
        {
            if (type == IdleAnimationType.Breathe)
            {
                float s = 1.0f + (MathF.Sin(time * speed) * magnitude);
                return new Vector2(s);
            }
            return Vector2.One;
        }
    }
}