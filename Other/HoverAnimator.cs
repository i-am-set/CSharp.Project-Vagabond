﻿using Microsoft.Xna.Framework;
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

        private const float AnimationDuration = 0.15f; // How long the entire hop takes
        private const float HopDistance = 3f;         // The maximum distance it hops to the right

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

            float currentOffset = 0f;

            if (_isAnimating)
            {
                _animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_animationTimer >= AnimationDuration)
                {
                    _isAnimating = false;
                    currentOffset = 0f;
                }
                else
                {
                    float progress = _animationTimer / AnimationDuration;
                    float wave = (float)Math.Sin(progress * 2.0 * Math.PI);
                    float decay = 1.0f - progress;
                    currentOffset = HopDistance * wave * decay;
                }
            }

            // 3. Store the activation state for the next frame.
            _wasActivatedLastFrame = isActivated;

            return currentOffset;
        }
    }
}