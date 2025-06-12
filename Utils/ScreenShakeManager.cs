using Microsoft.Xna.Framework;
using System;

namespace ProjectVagabond
{
    public class ScreenShakeManager
    {
        private Random _random;
        private float _shakeTimer;
        private float _shakeIntensity;
        private Vector2 _shakeOffset;
        private bool _isDecayed = true;

        public ScreenShakeManager()
        {
            _random = new Random();
            _shakeTimer = 0f;
            _shakeIntensity = 0f;
            _shakeOffset = Vector2.Zero;
        }

        /// <summary>
        /// Call this method to start a screenshake.
        /// </summary>
        /// <param name="magnitude">The intensity of the shake (e.g., 4.0f for a strong shake).</param>
        /// <param name="duration">The duration of the shake in seconds (e.g., 0.5f).</param>
        public void TriggerShake(float magnitude, float duration, bool isDecayed = true)
        {
            _isDecayed = isDecayed;

            if (magnitude > _shakeIntensity)
            {
                _shakeIntensity = magnitude;
            }
            _shakeTimer = duration;
        }

        public void Update(GameTime gameTime)
        {
            if (_shakeTimer > 0)
            {
                _shakeTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_shakeTimer <= 0)
                {
                    _shakeIntensity = 0f;
                    _shakeOffset = Vector2.Zero;
                }
                else
                {
                    // The core of the shake logic
                    // Generate a random offset within the magnitude
                    float offsetX = (float)(_random.NextDouble() * 2 - 1) * _shakeIntensity;
                    float offsetY = (float)(_random.NextDouble() * 2 - 1) * _shakeIntensity;
                    _shakeOffset = new Vector2(offsetX, offsetY);

                    // Decay the magnitude over time for a smoother effect
                    if (_isDecayed)
                     _shakeIntensity *= (1.0f - (float)gameTime.ElapsedGameTime.TotalSeconds / _shakeTimer);
                }
            } 
            else
            {
                _isDecayed = true;
            }
        }

        /// <summary>
        /// Gets the transformation matrix to apply to the SpriteBatch.
        /// </summary>
        public Matrix GetShakeMatrix()
        {
            return Matrix.CreateTranslation(_shakeOffset.X, _shakeOffset.Y, 0);
        }
    }
}
