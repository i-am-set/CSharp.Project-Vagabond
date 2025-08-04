using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Particles;
using System;

namespace ProjectVagabond.Scenes
{
    /// <summary>
    /// A reusable component to handle the juicy intro animation for a scene's main element.
    /// </summary>
    public class SceneIntroAnimator
    {
        private enum AnimState { Inflating, Deflating, Stretching, Expanding, Done }

        public bool IsComplete { get; private set; }

        private AnimState _state;
        private float _timer;

        // Timing constants for each phase, marking the end time of that phase.
        private const float INFLATE_END = 0.1f;
        private const float DEFLATE_END = 0.2f;
        private const float STRETCH_END = 0.35f;
        private const float EXPAND_END = 0.5f;

        private Rectangle _finalBounds;
        private Rectangle _contentBounds;
        private Rectangle _currentRect;
        private Vector2 _centerPoint;
        private Matrix _contentTransform = Matrix.Identity;

        public SceneIntroAnimator()
        {
            IsComplete = true; // Start as complete until Start() is called
        }

        public void Start(Rectangle finalBounds, Rectangle contentBounds)
        {
            _finalBounds = finalBounds;
            _contentBounds = contentBounds;
            _centerPoint = finalBounds.Center.ToVector2();
            _timer = 0f;
            _state = AnimState.Inflating;
            IsComplete = false;
        }

        public void Update(GameTime gameTime)
        {
            if (IsComplete) return;

            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // State transitions
            if (_state == AnimState.Inflating && _timer >= INFLATE_END)
            {
                _state = AnimState.Deflating;
            }
            else if (_state == AnimState.Deflating && _timer >= DEFLATE_END)
            {
                _state = AnimState.Stretching;
            }
            else if (_state == AnimState.Stretching && _timer >= STRETCH_END)
            {
                _state = AnimState.Expanding;
            }
            else if (_state == AnimState.Expanding && _timer >= EXPAND_END)
            {
                _state = AnimState.Done;
                IsComplete = true;
            }

            // Update animation properties based on state
            UpdateAnimationProperties();
        }

        private void UpdateAnimationProperties()
        {
            // Calculate overall progress from the start of the animation to the end.
            float overallProgress = MathHelper.Clamp(_timer / EXPAND_END, 0f, 1f);

            // Interpolate the stretch amount from 1.5x down to 1.0x over the entire duration.
            // The content starts stretched and returns to normal.
            float stretchAmount = MathHelper.Lerp(1.5f, 1.0f, Easing.EaseOutCubic(overallProgress));
            var screenCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);
            _contentTransform = Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y, 0) *
                                Matrix.CreateScale(stretchAmount, 1.0f, 1.0f) *
                                Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);

            switch (_state)
            {
                case AnimState.Inflating:
                    float inflateProgress = _timer / INFLATE_END;
                    float inflateSize = MathHelper.Lerp(0, 6, Easing.EaseOutBack(inflateProgress));
                    _currentRect = new Rectangle((int)(_centerPoint.X - inflateSize / 2), (int)(_centerPoint.Y - inflateSize / 2), (int)inflateSize, (int)inflateSize);
                    break;

                case AnimState.Deflating:
                    float deflateProgress = (_timer - INFLATE_END) / (DEFLATE_END - INFLATE_END);
                    float deflateSize = MathHelper.Lerp(6, 2, Easing.EaseInCubic(deflateProgress));
                    _currentRect = new Rectangle((int)(_centerPoint.X - deflateSize / 2), (int)(_centerPoint.Y - deflateSize / 2), (int)deflateSize, (int)deflateSize);
                    break;

                case AnimState.Stretching:
                    float stretchProgress = (_timer - DEFLATE_END) / (STRETCH_END - DEFLATE_END);
                    float stretchWidth = MathHelper.Lerp(2, _finalBounds.Width, Easing.EaseInCirc(stretchProgress));
                    _currentRect = new Rectangle((int)(_centerPoint.X - stretchWidth / 2), (int)(_centerPoint.Y - 2 / 2), (int)stretchWidth, 2);
                    break;

                case AnimState.Expanding:
                    float expandProgress = (_timer - STRETCH_END) / (EXPAND_END - STRETCH_END);
                    float expandHeight = MathHelper.Lerp(2, _finalBounds.Height, Easing.EaseInOutCubic(expandProgress));
                    _currentRect = new Rectangle(_finalBounds.X, (int)(_centerPoint.Y - expandHeight / 2), _finalBounds.Width, (int)expandHeight);
                    break;

                case AnimState.Done:
                    _currentRect = _finalBounds;
                    // When done, ensure the transform is back to identity to avoid any lingering effects.
                    _contentTransform = Matrix.Identity;
                    break;
            }
        }

        public Matrix GetContentTransform() => _contentTransform;

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Action drawContentAction)
        {
            // The drawContentAction is ignored, as the scene now draws itself before this method is called.
            // This method is now only responsible for drawing the mask on top of the scene.
            if (IsComplete)
            {
                return;
            }

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            var pixel = ServiceLocator.Get<Texture2D>();
            var color = Color.Black;

            // Draw four rectangles around the animated "_currentRect" to create a "hole" or mask effect.
            // Top rectangle
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, _currentRect.Top), color);
            // Bottom rectangle
            spriteBatch.Draw(pixel, new Rectangle(0, _currentRect.Bottom, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - _currentRect.Bottom), color);
            // Left rectangle
            spriteBatch.Draw(pixel, new Rectangle(0, _currentRect.Top, _currentRect.Left, _currentRect.Height), color);
            // Right rectangle
            spriteBatch.Draw(pixel, new Rectangle(_currentRect.Right, _currentRect.Top, Global.VIRTUAL_WIDTH - _currentRect.Right, _currentRect.Height), color);

            spriteBatch.End();
        }
    }
}