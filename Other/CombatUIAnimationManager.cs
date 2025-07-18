using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    /// <summary>
    /// Abstract base class for all UI animations managed by the CombatUIAnimationManager.
    /// </summary>
    public abstract class UIAnimation
    {
        public abstract void Update(GameTime gameTime);
    }

    /// <summary>
    /// A concrete animation class that provides a two-state (on/off) pulsing effect.
    /// </summary>
    public class PulsingAnimation : UIAnimation
    {
        public bool IsInflated { get; private set; }
        private float _timer;
        private readonly float _duration;

        public PulsingAnimation(float duration)
        {
            _duration = duration;
        }

        public override void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_timer >= _duration)
            {
                IsInflated = !IsInflated;
                _timer = 0f;
            }
        }
    }

    /// <summary>
    /// A concrete animation class that provides a smooth vertical bobbing effect using a sine wave.
    /// </summary>
    public class BobbingAnimation : UIAnimation
    {
        public float YOffset { get; private set; }
        private float _timer;
        private readonly float _speed;
        private readonly float _amount;

        public BobbingAnimation(float speed, float amount)
        {
            _speed = speed;
            _amount = amount;
        }

        public override void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            YOffset = (float)Math.Sin(_timer * _speed) * _amount;
        }
    }

    /// <summary>
    /// Manages all combat-related UI animations, centralizing their update logic.
    /// </summary>
    public class CombatUIAnimationManager
    {
        private readonly Dictionary<string, UIAnimation> _animations = new Dictionary<string, UIAnimation>();

        /// <summary>
        /// Initializes a new instance of the CombatUIAnimationManager and pre-registers
        /// a set of standard pulsing animations with varying speeds.
        /// </summary>
        public CombatUIAnimationManager()
        {
            // Pre-register four standard pulsing animations.
            // A shorter duration results in a faster pulse.
            RegisterAnimation("PulseSlow", new PulsingAnimation(1.0f));
            RegisterAnimation("PulseMedium", new PulsingAnimation(0.7f));
            RegisterAnimation("PulseFast", new PulsingAnimation(0.4f));
            RegisterAnimation("PulseVeryFast", new PulsingAnimation(0.2f));
        }

        /// <summary>
        /// Registers a new animation with the manager.
        /// </summary>
        /// <param name="key">A unique string key to identify the animation.</param>
        /// <param name="animation">The animation instance to manage.</param>
        public void RegisterAnimation(string key, UIAnimation animation)
        {
            _animations[key] = animation;
        }

        /// <summary>
        /// Updates all registered animations.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            foreach (var animation in _animations.Values)
            {
                animation.Update(gameTime);
            }
        }

        /// <summary>
        /// Gets the current state of a registered PulsingAnimation.
        /// </summary>
        /// <param name="key">The key of the animation to check.</param>
        /// <returns>True if the animation is in its "inflated" state, otherwise false.</returns>
        public bool IsPulsing(string key)
        {
            if (_animations.TryGetValue(key, out var animation) && animation is PulsingAnimation pulsing)
            {
                return pulsing.IsInflated;
            }
            return false;
        }

        /// <summary>
        /// Gets the current vertical offset of a registered BobbingAnimation.
        /// </summary>
        /// <param name="key">The key of the animation to check.</param>
        /// <returns>The calculated Y-offset for the animation.</returns>
        public float GetBobbingOffset(string key)
        {
            if (_animations.TryGetValue(key, out var animation) && animation is BobbingAnimation bobbing)
            {
                return bobbing.YOffset;
            }
            return 0f;
        }
    }
}