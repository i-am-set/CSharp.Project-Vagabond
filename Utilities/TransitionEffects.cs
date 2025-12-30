using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Transitions
{
    // --- 1. STANDARD FADE ---
    public class FadeTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.1f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        public void Start(bool isTransitioningOut)
        {
            _isOut = isTransitioningOut;
            _timer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale)
        {
            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);
            float alpha = _isOut ? progress : 1.0f - progress;

            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.Draw(pixel, bounds, Color.Black * alpha);
        }
    }

    // --- 2. SHUTTERS (Top/Bottom Close) ---
    public class ShuttersTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.8f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        public void Start(bool isTransitioningOut)
        {
            _isOut = isTransitioningOut;
            _timer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale)
        {
            int width = bounds.Width;
            int height = bounds.Height;

            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);
            float eased = _isOut ? Easing.EaseInOutExpo(progress) : Easing.EaseInQuad(1.0f - progress);

            int halfHeight = height / 2;
            int currentHeight = (int)(halfHeight * eased) + (_isOut && progress >= 0.9f ? 2 : 0);

            var pixel = ServiceLocator.Get<Texture2D>();

            // Top Shutter
            spriteBatch.Draw(pixel, new Rectangle(0, 0, width, currentHeight), Color.Black);

            // Bottom Shutter
            spriteBatch.Draw(pixel, new Rectangle(0, height - currentHeight, width, currentHeight), Color.Black);
        }
    }

    // --- 3. DIAMONDS ---
    public class DiamondWipeTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 1.2f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        // Virtual pixel size
        private const int GRID_SIZE = 20;

        public void Start(bool isTransitioningOut)
        {
            _isOut = isTransitioningOut;
            _timer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            int width = bounds.Width;
            int height = bounds.Height;

            // Scale the grid size to match the window scale
            float scaledGridSize = GRID_SIZE * scale;

            int cols = (int)Math.Ceiling(width / scaledGridSize) + 2;
            int rows = (int)Math.Ceiling(height / scaledGridSize) + 2;

            float maxDelay = 0.6f;
            float growTime = 0.4f;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    float delay = ((float)(x + y) / (cols + rows)) * maxDelay;
                    float localTime = _timer - delay;
                    float progress = Math.Clamp(localTime / growTime, 0f, 1f);
                    float sizeScale = _isOut ? progress : 1.0f - progress;

                    if (sizeScale > 0)
                    {
                        Vector2 center = new Vector2(x * scaledGridSize, y * scaledGridSize);
                        float size = scaledGridSize * 1.5f * sizeScale;

                        spriteBatch.Draw(
                            pixel,
                            center,
                            null,
                            Color.Black,
                            MathHelper.PiOver4,
                            new Vector2(0.5f, 0.5f),
                            size,
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }

            if (_isOut && _timer > DURATION * 0.9f)
            {
                spriteBatch.Draw(pixel, bounds, Color.Black);
            }
        }
    }

    // --- 4. BLOCKS (Random Grid Fill - Small) ---
    public class BlocksWipeTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.5f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        private const int BLOCK_SIZE = 10;
        // We can't pre-calculate indices in Start() anymore because bounds might change or scale might change.
        // We'll use a deterministic random approach based on coordinates.

        public void Start(bool isTransitioningOut)
        {
            _isOut = isTransitioningOut;
            _timer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            float scaledBlockSize = BLOCK_SIZE * scale;
            int cols = (int)Math.Ceiling(bounds.Width / scaledBlockSize);
            int rows = (int)Math.Ceiling(bounds.Height / scaledBlockSize);

            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);

            // Use a fixed seed for stability during the transition
            var rng = new Random(12345);

            // We need to determine which blocks are "on" based on progress.
            // To do this efficiently without re-shuffling every frame, we can assign a random threshold to each block.

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    // Generate a deterministic random value for this block (0.0 to 1.0)
                    // Simple hash of coordinates
                    double blockThreshold = ((x * 37 + y * 113) % 1000) / 1000.0;

                    // Scramble it a bit more with Random seeded by coord
                    var blockRng = new Random(x * 1000 + y);
                    blockThreshold = blockRng.NextDouble();

                    bool shouldDraw;
                    if (_isOut)
                    {
                        shouldDraw = progress >= blockThreshold;
                    }
                    else
                    {
                        shouldDraw = (1.0f - progress) >= blockThreshold;
                    }

                    if (shouldDraw)
                    {
                        Rectangle rect = new Rectangle(
                            (int)(x * scaledBlockSize),
                            (int)(y * scaledBlockSize),
                            (int)Math.Ceiling(scaledBlockSize),
                            (int)Math.Ceiling(scaledBlockSize)
                        );
                        spriteBatch.Draw(pixel, rect, Color.Black);
                    }
                }
            }

            if (_isOut && progress >= 0.95f)
            {
                spriteBatch.Draw(pixel, bounds, Color.Black);
            }
        }
    }

    // --- 5. BIG BLOCKS EASE (Wave Scaling) ---
    public class BigBlocksEaseTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 1.0f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        private const int BLOCK_SIZE = 40;

        public void Start(bool isTransitioningOut)
        {
            _isOut = isTransitioningOut;
            _timer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            float scaledBlockSize = BLOCK_SIZE * scale;
            int cols = (int)Math.Ceiling(bounds.Width / scaledBlockSize);
            int rows = (int)Math.Ceiling(bounds.Height / scaledBlockSize);

            float maxDelay = 0.5f;
            float growTime = 0.4f;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    float delay = ((float)(x + y) / (cols + rows)) * maxDelay;
                    float localTime = _timer - delay;
                    float progress = Math.Clamp(localTime / growTime, 0f, 1f);

                    float eased = Easing.EaseOutCubic(progress);
                    float sizeScale = _isOut ? eased : 1.0f - eased;

                    if (sizeScale > 0.01f)
                    {
                        float finalScale = sizeScale * 1.05f; // Overlap buffer
                        float size = scaledBlockSize * finalScale;

                        float centerX = x * scaledBlockSize + scaledBlockSize / 2;
                        float centerY = y * scaledBlockSize + scaledBlockSize / 2;

                        Rectangle rect = new Rectangle(
                            (int)(centerX - size / 2),
                            (int)(centerY - size / 2),
                            (int)size,
                            (int)size
                        );

                        spriteBatch.Draw(pixel, rect, Color.Black);
                    }
                }
            }

            if (_isOut && _timer > DURATION * 0.95f)
            {
                spriteBatch.Draw(pixel, bounds, Color.Black);
            }
        }
    }

    // --- 6. PIXELS (Small Grid) ---
    public class PixelWipeTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 1.0f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        private const int BLOCK_SIZE = 1; // Virtual pixel size

        public void Start(bool isTransitioningOut)
        {
            _isOut = isTransitioningOut;
            _timer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            float scaledBlockSize = BLOCK_SIZE * scale;
            int cols = (int)Math.Ceiling(bounds.Width / scaledBlockSize);
            int rows = (int)Math.Ceiling(bounds.Height / scaledBlockSize);

            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);

            // Deterministic random drawing
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    // Simple hash for threshold
                    double blockThreshold = ((x * 73 + y * 179) % 1000) / 1000.0;
                    var blockRng = new Random(x * 5000 + y);
                    blockThreshold = blockRng.NextDouble();

                    bool shouldDraw;
                    if (_isOut)
                    {
                        shouldDraw = progress >= blockThreshold;
                    }
                    else
                    {
                        shouldDraw = (1.0f - progress) >= blockThreshold;
                    }

                    if (shouldDraw)
                    {
                        Rectangle rect = new Rectangle(
                            (int)(x * scaledBlockSize),
                            (int)(y * scaledBlockSize),
                            (int)Math.Ceiling(scaledBlockSize),
                            (int)Math.Ceiling(scaledBlockSize)
                        );
                        spriteBatch.Draw(pixel, rect, Color.Black);
                    }
                }
            }

            if (_isOut && progress >= 0.95f)
            {
                spriteBatch.Draw(pixel, bounds, Color.Black);
            }
        }
    }
}