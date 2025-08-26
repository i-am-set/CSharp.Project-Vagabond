using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectVagabond.Utils
{
    /// <summary>
    /// Represents a single frame in an animation cycle.
    /// </summary>
    public struct AnimationFrame
    {
        public Rectangle SourceRectangle { get; }
        public float Duration { get; }

        public AnimationFrame(Rectangle sourceRectangle, float duration)
        {
            SourceRectangle = sourceRectangle;
            Duration = duration;
        }
    }

    /// <summary>
    /// Represents a named sequence of frames, like "walk" or "idle".
    /// </summary>
    public class AnimationCycle
    {
        public string Name { get; }
        public bool IsLooping { get; }
        public AnimationFrame[] Frames { get; }

        public AnimationCycle(string name, bool isLooping, AnimationFrame[] frames)
        {
            Name = name;
            IsLooping = isLooping;
            Frames = frames;
        }
    }

    /// <summary>
    /// A data container that holds a texture atlas and all its defined animation cycles.
    /// This is a lightweight replacement for MonoGame.Extended's SpriteSheet.
    /// </summary>
    public class SimpleSpriteSheet
    {
        public Texture2D Texture { get; }
        public Dictionary<string, AnimationCycle> Cycles { get; }

        public SimpleSpriteSheet(Texture2D texture, Dictionary<string, AnimationCycle> cycles)
        {
            Texture = texture;
            Cycles = cycles;
        }
    }

    /// <summary>
    /// A lightweight animation player that updates and draws frames from a SimpleSpriteSheet.
    /// This is a replacement for MonoGame.Extended's AnimatedSprite.
    /// </summary>
    public class SimpleAnimator
    {
        private readonly SimpleSpriteSheet _spriteSheet;
        private AnimationCycle _currentCycle;
        private int _currentFrameIndex;
        private float _frameTimer;

        public bool IsAnimationFinished { get; private set; }
        public AnimationFrame CurrentFrame => _currentCycle?.Frames[_currentFrameIndex] ?? default;


        public SimpleAnimator(SimpleSpriteSheet spriteSheet)
        {
            _spriteSheet = spriteSheet;
        }

        public bool HasAnimation(string animationName)
        {
            return _spriteSheet.Cycles.ContainsKey(animationName);
        }

        public IEnumerable<string> GetAnimationNames()
        {
            return _spriteSheet?.Cycles.Keys ?? Enumerable.Empty<string>();
        }

        public void Play(string animationName)
        {
            if (_spriteSheet.Cycles.TryGetValue(animationName, out var newCycle))
            {
                // Don't restart an animation that's already playing unless it's a looping one
                if (_currentCycle == newCycle && !IsAnimationFinished)
                {
                    return;
                }

                _currentCycle = newCycle;
                _currentFrameIndex = 0;
                _frameTimer = 0f;
                IsAnimationFinished = false;
            }
        }

        public void Update(GameTime gameTime)
        {
            if (_currentCycle == null || IsAnimationFinished)
            {
                return;
            }

            _frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            var currentFrame = _currentCycle.Frames[_currentFrameIndex];

            if (_frameTimer >= currentFrame.Duration)
            {
                _frameTimer -= currentFrame.Duration;
                _currentFrameIndex++;

                if (_currentFrameIndex >= _currentCycle.Frames.Length)
                {
                    if (_currentCycle.IsLooping)
                    {
                        _currentFrameIndex = 0;
                    }
                    else
                    {
                        _currentFrameIndex = _currentCycle.Frames.Length - 1;
                        IsAnimationFinished = true;
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 position, Color color, float rotation = 0f, float scale = 1f)
        {
            if (_currentCycle != null)
            {
                var frame = _currentCycle.Frames[_currentFrameIndex];
                // The origin is now the bottom-center of the sprite, acting as a pivot for the wrist.
                var origin = new Vector2(frame.SourceRectangle.Width / 2f, frame.SourceRectangle.Height);
                spriteBatch.Draw(_spriteSheet.Texture, position, frame.SourceRectangle, color, rotation, origin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}