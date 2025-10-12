using Microsoft.Xna.Framework;
using System;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// A helper class to manage on-hover animation for UI elements.
    /// The animation triggers when a condition (like being hovered) is first met.
    /// </summary>
    public class HoverAnimator
    {
        private bool _isAnimating = false;
        private float _animationTimer = 0f;
        private bool _wasActivatedLastFrame = false;

        private const float AnimationDuration = 0.2f; // How long the entire hop takes
        private const float HopDistance = 2f;         // The maximum distance it hops

        public float CurrentOffset { get; private set; }

        /// <summary>
        /// Resets the animator to its default, non-animating state.
        /// </summary>
        public void Reset()
        {
            _isAnimating = false;
            _animationTimer = 0f;
            _wasActivatedLastFrame = false;
            CurrentOffset = 0f;
        }

        /// <summary>
        /// Updates the animation state and returns the current horizontal offset.
        /// </summary>
        /// <param name="gameTime">The current game time.</param>
        /// <param name="isActivated">Whether the animation should be active (e.g., the element is hovered).</param>
        /// <returns>The calculated horizontal offset for drawing.</returns>
        public float UpdateAndGetOffset(GameTime gameTime, bool isActivated)
        {
            if (isActivated && !_wasActivatedLastFrame)
            {
                _isAnimating = true;
                _animationTimer = 0f;
            }

            CurrentOffset = 0f;

            if (_isAnimating)
            {
                _animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_animationTimer >= AnimationDuration)
                {
                    _isAnimating = false;
                    CurrentOffset = 0f;
                }
                else
                {
                    float progress = _animationTimer / AnimationDuration;
                    // Use Math.Sin to create the 0 -> 1 -> 0 arc of the animation
                    float wave = (float)Math.Sin(progress * Math.PI);
                    // Apply an easing function to the arc itself to change its feel.
                    // EaseOutCubic makes it pop up quickly and settle gently at the peak.
                    float easedWave = Easing.EaseOutCubic(wave);
                    CurrentOffset = HopDistance * easedWave;
                }
            }

            // Store the activation state for the next frame.
            _wasActivatedLastFrame = isActivated;

            return CurrentOffset;
        }
    }
}