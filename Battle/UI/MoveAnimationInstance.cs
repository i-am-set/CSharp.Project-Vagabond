using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Represents a single, currently playing instance of a move animation.
    /// </summary>
    public class MoveAnimationInstance
    {
        private readonly MoveAnimation _animationData;
        private readonly float _frameDuration;
        private float _frameTimer;
        private int _currentFrame;
        private readonly int _damageFrameIndex;
        private bool _hasTriggeredImpact = false;
        private readonly Func<Vector2> _positionProvider;
        private readonly Action _onImpact;

        public bool IsFinished { get; private set; }
        public float LayerDepth { get; set; } = 0.1f; // Draw on top of most things

        public MoveAnimationInstance(MoveAnimation animationData, Func<Vector2> positionProvider, float secondsPerFrame, int damageFrameIndex, Action onImpact)
        {
            _animationData = animationData;
            _positionProvider = positionProvider;
            _damageFrameIndex = damageFrameIndex;
            _frameDuration = secondsPerFrame;
            _onImpact = onImpact;
        }

        public void Update(GameTime gameTime)
        {
            if (IsFinished) return;

            // Check for impact trigger on the very first frame if index is 0
            if (_currentFrame == _damageFrameIndex && !_hasTriggeredImpact)
            {
                _onImpact?.Invoke();
                _hasTriggeredImpact = true;
            }

            _frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_frameTimer >= _frameDuration)
            {
                _frameTimer -= _frameDuration;
                _currentFrame++;

                // Check for impact trigger on subsequent frames
                if (_currentFrame == _damageFrameIndex && !_hasTriggeredImpact)
                {
                    _onImpact?.Invoke();
                    _hasTriggeredImpact = true;
                }

                if (_currentFrame >= _animationData.FrameCount)
                {
                    IsFinished = true;
                    // Failsafe: If animation finishes without triggering impact (e.g. short animation), trigger it now.
                    if (!_hasTriggeredImpact)
                    {
                        _onImpact?.Invoke();
                        _hasTriggeredImpact = true;
                    }
                }
            }
        }

        public void ForceComplete()
        {
            if (!_hasTriggeredImpact)
            {
                _onImpact?.Invoke();
                _hasTriggeredImpact = true;
            }
            IsFinished = true;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (IsFinished || _animationData.FrameCount == 0) return;

            // Clamp frame index to be safe
            int frameToDraw = Math.Clamp(_currentFrame, 0, _animationData.FrameCount - 1);
            var sourceRect = _animationData.SourceRectangles[frameToDraw];
            var origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);

            // Retrieve current position dynamically to track target movement
            Vector2 currentPosition = _positionProvider?.Invoke() ?? Vector2.Zero;

            spriteBatch.DrawSnapped(
                _animationData.SpriteSheet,
                currentPosition,
                sourceRect,
                Color.White,
                0f, // No rotation
                origin,
                1f, // No scale variance
                SpriteEffects.None,
                LayerDepth
            );
        }
    }
}