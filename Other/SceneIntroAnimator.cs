using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using System;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// A reusable component to handle the juicy intro animation for a scene's main element.
    /// </summary>
    public class SceneIntroAnimator
    {
        public bool IsComplete { get; private set; }

        private float _timer;
        private const float DURATION = 0.05f;

        private Matrix _contentTransform = Matrix.Identity;

        public SceneIntroAnimator()
        {
            IsComplete = true; // Start as complete until Start() is called
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

            if (_timer >= DURATION)
            {
                IsComplete = true;
                _contentTransform = Matrix.Identity; // Final state
                return;
            }

            float progress = _timer / DURATION;
            float easedProgress = Easing.EaseOutCubic(progress);

            // Animate from stretched-and-squashed to normal proportions.
            float scaleX = MathHelper.Lerp(3.0f, 1.0f, easedProgress);
            float scaleY = MathHelper.Lerp(0.0f, 1.0f, easedProgress);

            var screenCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);

            // Create the transformation matrix.
            _contentTransform = Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y, 0) *
                                Matrix.CreateScale(scaleX, scaleY, 1.0f) *
                                Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);
        }

        public Matrix GetContentTransform() => _contentTransform;

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Action drawContentAction)
        {
            // This method is now empty. The animation is applied via a transform in the SceneManager.
        }
    }
}