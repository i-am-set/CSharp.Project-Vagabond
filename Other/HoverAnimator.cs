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
        private bool _isAnimating;
        private float _animationTimer;
        private bool _wasActivatedLastFrame;

        private float _startOffset;
        private float _targetOffset;

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
            _startOffset = 0f;
            _targetOffset = 0f;
        }

        /// <summary>
        /// Updates the animation state and returns the current vertical offset.
        /// </summary>
        /// <param name="gameTime">The current game time.</param>
        /// <param name="isActivated">Whether the animation should be active (e.g., the element is hovered).</param>
        /// <param name="liftDistance">The total distance to move (usually negative for up).</param>
        /// <param name="duration">The time in seconds to complete the move.</param>
        /// <returns>The calculated vertical offset for drawing.</returns>
        public float UpdateAndGetOffset(GameTime gameTime, bool isActivated, float liftDistance, float duration)
        {
            // If the duration is zero (or less), the animation is instant.
            // We can bypass all the timer and interpolation logic to "snap" to the target position.
            if (duration <= 0f)
            {
                CurrentOffset = isActivated ? liftDistance : 0f;
                _wasActivatedLastFrame = isActivated;
                return CurrentOffset;
            }

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Check for a change in hover state to trigger an animation
            if (isActivated && !_wasActivatedLastFrame)
            {
                _isAnimating = true;
                _animationTimer = 0f;
                _startOffset = CurrentOffset;
                _targetOffset = liftDistance;
            }
            else if (!isActivated && _wasActivatedLastFrame)
            {
                _isAnimating = true;
                _animationTimer = 0f;
                _startOffset = CurrentOffset;
                _targetOffset = 0f;
            }

            if (_isAnimating)
            {
                _animationTimer += deltaTime;
                float progress = Math.Clamp(_animationTimer / duration, 0f, 1f);
                float easedProgress = Easing.EaseOutCubic(progress);

                CurrentOffset = MathHelper.Lerp(_startOffset, _targetOffset, easedProgress);

                if (progress >= 1.0f)
                {
                    _isAnimating = false;
                    CurrentOffset = _targetOffset; // Snap to final value
                }
            }

            _wasActivatedLastFrame = isActivated;

            return CurrentOffset;
        }
    }
}