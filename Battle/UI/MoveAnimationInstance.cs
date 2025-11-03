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

        public Vector2 Position { get; }
        public bool IsFinished { get; private set; }
        public float LayerDepth { get; set; } = 0.1f; // Draw on top of most things

        public MoveAnimationInstance(MoveAnimation animationData, Vector2 position, float animationSpeed)
        {
            _animationData = animationData;
            Position = position;
            // Base frame rate of 12 FPS. AnimationSpeed is a multiplier.
            const float baseFrameDuration = 1f / 12f;
            _frameDuration = baseFrameDuration / Math.Max(0.1f, animationSpeed);
        }

        public void Update(GameTime gameTime)
        {
            if (IsFinished) return;

            _frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_frameTimer >= _frameDuration)
            {
                _frameTimer -= _frameDuration;
                _currentFrame++;
                if (_currentFrame >= _animationData.FrameCount)
                {
                    IsFinished = true;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (IsFinished || _animationData.FrameCount == 0) return;

            var sourceRect = _animationData.SourceRectangles[_currentFrame];
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
#nullable restore