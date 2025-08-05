using Microsoft.Xna.Framework;
using System;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// An animation that produces a smooth, organic swaying motion using sine and cosine waves.
    /// </summary>
    public class OrganicSwayAnimation : IAnimation
    {
        /// <summary>
        /// The calculated positional offset for the current frame.
        /// </summary>
        public Vector2 Offset { get; private set; }

        private float _timerX;
        private float _timerY;
        private readonly float _speedX;
        private readonly float _speedY;
        private readonly float _amountX;
        private readonly float _amountY;
        private static readonly Random _random = new Random();

        public OrganicSwayAnimation(float speedX, float speedY, float amountX, float amountY)
        {
            _speedX = speedX;
            _speedY = speedY;
            _amountX = amountX;
            _amountY = amountY;

            // Start timers at random points to desynchronize multiple instances of the animation
            _timerX = (float)(_random.NextDouble() * Math.PI * 2);
            _timerY = (float)(_random.NextDouble() * Math.PI * 2);
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _timerX += deltaTime * _speedX;
            _timerY += deltaTime * _speedY;

            float swayX = (float)Math.Sin(_timerX) * _amountX;
            float swayY = (float)Math.Cos(_timerY) * _amountY;
            Offset = new Vector2(swayX, swayY);
        }
    }
}