#nullable enable
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// Represents a single slot in the inventory grid, holding its position and visual state.
    /// </summary>
    public class InventorySlot
    {
        public Vector2 Position { get; }
        public Rectangle SourceRectangle { get; private set; }

        private float _frameChangeTimer;
        private float _nextFrameChangeTime;
        private static readonly Random _random = new Random();

        // --- Tuning ---
        private const float MIN_FRAME_CHANGE_SECONDS = 2.0f;
        private const float MAX_FRAME_CHANGE_SECONDS = 8.0f;

        public InventorySlot(Vector2 position, Rectangle sourceRectangle)
        {
            Position = position;
            SourceRectangle = sourceRectangle;
            ResetFrameChangeTimer();
        }

        private void ResetFrameChangeTimer()
        {
            _frameChangeTimer = 0f;
            _nextFrameChangeTime = (float)(_random.NextDouble() * (MAX_FRAME_CHANGE_SECONDS - MIN_FRAME_CHANGE_SECONDS) + MIN_FRAME_CHANGE_SECONDS);
        }

        public void Update(GameTime gameTime, IReadOnlyList<Rectangle> allFrames)
        {
            _frameChangeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_frameChangeTimer >= _nextFrameChangeTime)
            {
                if (allFrames.Count > 1)
                {
                    Rectangle newFrame;
                    do
                    {
                        newFrame = allFrames[_random.Next(allFrames.Count)];
                    } while (newFrame == SourceRectangle); // Ensure the new frame is different

                    SourceRectangle = newFrame;
                }
                ResetFrameChangeTimer();
            }
        }
    }
}
#nullable restore