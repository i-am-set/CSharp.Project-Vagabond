using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// A reusable component to handle the juicy outro animation for a scene.
    /// It performs a reverse "iris wipe" to transition to black.
    /// </summary>
    public class SceneOutroAnimator
    {
        public bool IsComplete { get; private set; }
        public event Action OnComplete;

        private float _timer;
        private const float DURATION = 0.05f;

        private Matrix _transform = Matrix.Identity;

        public SceneOutroAnimator()
        {
            IsComplete = true;
        }

        public void Start()
        {
            _timer = 0f;
            IsComplete = false;
        }

        public void Update(GameTime gameTime)
        {
            if (IsComplete) return;

            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            var screenCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);

            if (_timer >= DURATION)
            {
                IsComplete = true;
                // Final state ensures it's fully squashed vertically.
                _transform = Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y, 0) *
                             Matrix.CreateScale(3.0f, 0.0f, 1.0f) *
                             Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);

                OnComplete?.Invoke();
                return;
            }

            float progress = _timer / DURATION;
            float easedProgress = Easing.EaseInCubic(progress);

            // Animate from normal proportions to stretched-and-squashed.
            float scaleX = MathHelper.Lerp(1.0f, 3.0f, easedProgress);
            float scaleY = MathHelper.Lerp(1.0f, 0.0f, easedProgress);

            _transform = Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y, 0) *
                         Matrix.CreateScale(scaleX, scaleY, 1.0f) *
                         Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);
        }

        public Matrix GetContentTransform() => _transform;

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Action drawContentAction)
        {
            // This method is now empty. The animation is applied via a transform in the SceneManager,
            // and the black screen is handled by the TransitionScene.
        }
    }
}