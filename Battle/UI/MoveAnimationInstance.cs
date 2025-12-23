#nullable enable
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

        public Vector2 Position { get; }
        public bool IsFinished { get; private set; }
        public float LayerDepth { get; set; } = 0.1f; // Draw on top of most things

        // Event fired when the animation reaches the damage frame
        public event Action? OnImpactFrameReached;

        public MoveAnimationInstance(MoveAnimation animationData, Vector2 position, float animationSpeed, int damageFrameIndex)
        {
            _animationData = animationData;
            Position = position;
            _damageFrameIndex = damageFrameIndex;
            // Base frame rate of 12 FPS. AnimationSpeed is a multiplier.
            const float baseFrameDuration = 1f / 12f;
            _frameDuration = baseFrameDuration / Math.Max(0.1f, animationSpeed);
        }

        public void Update(GameTime gameTime)
        {
            if (IsFinished) return;

            // Check for impact trigger on the very first frame if index is 0
            if (_currentFrame == _damageFrameIndex && !_hasTriggeredImpact)
            {
                OnImpactFrameReached?.Invoke();
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
                    OnImpactFrameReached?.Invoke();
                    _hasTriggeredImpact = true;
                }

                if (_currentFrame >= _animationData.FrameCount)
                {
                    IsFinished = true;
                    // Failsafe: If animation finishes without triggering impact (e.g. short animation), trigger it now.
                    if (!_hasTriggeredImpact)
                    {
                        OnImpactFrameReached?.Invoke();
                        _hasTriggeredImpact = true;
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (IsFinished || _animationData.FrameCount == 0) return;

            // Clamp frame index to be safe
            int frameToDraw = Math.Clamp(_currentFrame, 0, _animationData.FrameCount - 1);
            var sourceRect = _animationData.SourceRectangles[frameToDraw];
            var origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);

            spriteBatch.DrawSnapped(
                _animationData.SpriteSheet,
                Position,
                sourceRect,
                Color.White,
                0f,
                origin,
                1f,
                SpriteEffects.None,
                LayerDepth
            );
        }
    }
}