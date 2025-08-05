using Microsoft.Xna.Framework;
using MonoGame.Extended.Animations;
using System.Collections.Generic;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// A centralized manager for updating and accessing shared UI animation instances.
    /// </summary>
    public class AnimationManager
    {
        private readonly Dictionary<string, IAnimation> _animations = new();

        /// <summary>
        /// Registers an animation instance with a unique key.
        /// If a key already exists, it will be overwritten.
        /// </summary>
        /// <param name="key">The unique identifier for the animation.</param>
        /// <param name="animation">The animation object to manage.</param>
        public void Register(string key, IAnimation animation)
        {
            _animations[key] = animation;
        }

        /// <summary>
        /// Removes an animation from the manager.
        /// </summary>
        /// <param name="key">The key of the animation to remove.</param>
        public void Unregister(string key)
        {
            _animations.Remove(key);
        }

        /// <summary>
        /// Retrieves a strongly-typed animation instance from the manager.
        /// </summary>
        /// <typeparam name="T">The type of the animation to retrieve.</typeparam>
        /// <param name="key">The key of the animation.</param>
        /// <returns>The animation object, or null if not found or of the wrong type.</returns>
        public T GetAnimation<T>(string key) where T : class, IAnimation
        {
            _animations.TryGetValue(key, out var animation);
            return animation as T;
        }

        /// <summary>
        /// Updates all registered animation objects.
        /// </summary>
        public void Update(GameTime gameTime)
        {
            // Using a standard foreach is safe as we don't modify the collection during iteration.
            foreach (var animation in _animations.Values)
            {
                animation.Update(gameTime);
            }
        }
    }
}