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
        private enum AnimState { Shrinking, Contracting, Collapsing, Done }

        public bool IsComplete { get; private set; }
        public event Action OnComplete;

        private AnimState _state;
        private float _timer;

        // Timing constants for each phase, marking the end time of that phase.
        private const float SHRINK_END = 0.08f;
        private const float CONTRACT_END = 0.12f;
        private const float COLLAPSE_END = 0.15f;

        private Rectangle _initialBounds;
        private Rectangle _currentRect;
        private Vector2 _centerPoint;
        private Matrix _contentTransform = Matrix.Identity;

        public SceneOutroAnimator()
        {
            IsComplete = true;
        }

        public void Start(Rectangle initialBounds)
        {
            _initialBounds = initialBounds;
            _centerPoint = initialBounds.Center.ToVector2();
            _currentRect = _initialBounds;
            _timer = 0f;
            _state = AnimState.Shrinking;
            IsComplete = false;
        }

        public void Update(GameTime gameTime)
        {
            if (IsComplete) return;

            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_state == AnimState.Shrinking && _timer >= SHRINK_END)
            {
                _state = AnimState.Contracting;
            }
            else if (_state == AnimState.Contracting && _timer >= CONTRACT_END)
            {
                _state = AnimState.Collapsing;
            }
            else if (_state == AnimState.Collapsing && _timer >= COLLAPSE_END)
            {
                _state = AnimState.Done;
                IsComplete = true;
                OnComplete?.Invoke();
            }

            UpdateAnimationProperties();
        }

        private void UpdateAnimationProperties()
        {
            _contentTransform = Matrix.Identity;

            switch (_state)
            {
                case AnimState.Shrinking:
                    float shrinkProgress = _timer / SHRINK_END;
                    float shrinkHeight = MathHelper.Lerp(_initialBounds.Height, 2, Easing.EaseInCubic(shrinkProgress));
                    _currentRect = new Rectangle(_initialBounds.X, (int)(_centerPoint.Y - shrinkHeight / 2), _initialBounds.Width, (int)shrinkHeight);
                    break;

                case AnimState.Contracting:
                    float contractProgress = (_timer - SHRINK_END) / (CONTRACT_END - SHRINK_END);
                    float contractWidth = MathHelper.Lerp(_initialBounds.Width, 2, Easing.EaseInCubic(contractProgress));
                    _currentRect = new Rectangle((int)(_centerPoint.X - contractWidth / 2), (int)(_centerPoint.Y - 2 / 2), (int)contractWidth, 2);
                    break;

                case AnimState.Collapsing:
                    float collapseProgress = (_timer - CONTRACT_END) / (COLLAPSE_END - CONTRACT_END);
                    float collapseSize = MathHelper.Lerp(2, 0, Easing.EaseInCubic(collapseProgress));
                    _currentRect = new Rectangle((int)(_centerPoint.X - collapseSize / 2), (int)(_centerPoint.Y - collapseSize / 2), (int)collapseSize, (int)collapseSize);

                    // Calculate content stretch effect, always centered on the screen.
                    float stretchAmount = MathHelper.Lerp(1.0f, 1.5f, Easing.EaseInCubic(collapseProgress));
                    var screenCenter = new Vector2(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f);
                    _contentTransform = Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y, 0) *
                                        Matrix.CreateScale(stretchAmount, 1.0f, 1.0f) *
                                        Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);
                    break;

                case AnimState.Done:
                    _currentRect = new Rectangle((int)_centerPoint.X, (int)_centerPoint.Y, 0, 0);
                    break;
            }
        }

        public Matrix GetContentTransform() => _contentTransform;

        public void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Action drawContentAction)
        {
            if (IsComplete) return;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            var pixel = ServiceLocator.Get<Texture2D>();
            var color = Color.Black;

            // Draw four rectangles around the animated "_currentRect" to create a "hole" or mask effect.
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, _currentRect.Top), color);
            spriteBatch.Draw(pixel, new Rectangle(0, _currentRect.Bottom, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT - _currentRect.Bottom), color);
            spriteBatch.Draw(pixel, new Rectangle(0, _currentRect.Top, _currentRect.Left, _currentRect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(_currentRect.Right, _currentRect.Top, Global.VIRTUAL_WIDTH - _currentRect.Right, _currentRect.Height), color);

            spriteBatch.End();
        }
    }
}