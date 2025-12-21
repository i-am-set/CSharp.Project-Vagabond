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

        public void Draw(SpriteBatch spriteBatch)
        {
            var global = ServiceLocator.Get<Global>();

            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);
            float alpha = _isOut ? progress : 1.0f - progress;

            var pixel = ServiceLocator.Get<Texture2D>();
            // Draw to Virtual Resolution
            var bounds = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

            spriteBatch.Draw(pixel, bounds, global.Palette_Black * alpha);
        }
    }

    // --- 2. SHUTTERS (Top/Bottom Close) ---
    public class ShuttersTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.2f;
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

        public void Draw(SpriteBatch spriteBatch)
        {
            var global = ServiceLocator.Get<Global>();

            // Use Virtual Resolution
            int width = Global.VIRTUAL_WIDTH;
            int height = Global.VIRTUAL_HEIGHT;

            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);
            float eased = _isOut ? Easing.EaseInOutExpo(progress) : Easing.EaseInQuad(1.0f - progress);

            int halfHeight = height / 2;
            int currentHeight = (int)(halfHeight * eased) + (_isOut && progress >= 0.9f ? 2 : 0);

            var pixel = ServiceLocator.Get<Texture2D>();

            // Top Shutter
            spriteBatch.Draw(pixel, new Rectangle(0, 0, width, currentHeight), global.Palette_Black);

            // Bottom Shutter
            spriteBatch.Draw(pixel, new Rectangle(0, height - currentHeight, width, currentHeight), global.Palette_Black);
        }
    }

    // --- 3. DIAMONDS ---
    public class DiamondWipeTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 1.2f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        // Fixed virtual pixel size (20px on a 320px screen = 16 columns)
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

        public void Draw(SpriteBatch spriteBatch)
        {
            var global = ServiceLocator.Get<Global>();
            var pixel = ServiceLocator.Get<Texture2D>();

            int width = Global.VIRTUAL_WIDTH;
            int height = Global.VIRTUAL_HEIGHT;

            int cols = (int)Math.Ceiling((float)width / GRID_SIZE) + 2;
            int rows = (int)Math.Ceiling((float)height / GRID_SIZE) + 2;

            float maxDelay = 0.6f;
            float growTime = 0.4f;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    float delay = ((float)(x + y) / (cols + rows)) * maxDelay;
                    float localTime = _timer - delay;
                    float progress = Math.Clamp(localTime / growTime, 0f, 1f);
                    float scale = _isOut ? progress : 1.0f - progress;

                    if (scale > 0)
                    {
                        Vector2 center = new Vector2(x * GRID_SIZE, y * GRID_SIZE);
                        float size = GRID_SIZE * 1.5f * scale;

                        spriteBatch.Draw(
                            pixel,
                            center,
                            null,
                            global.Palette_Black,
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
                spriteBatch.Draw(pixel, new Rectangle(0, 0, width, height), global.Palette_Black);
            }
        }
    }

    // --- 4. BLOCKS (Random Grid Fill) ---
    public class BlocksWipeTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.8f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        // Fixed virtual pixel size (40px on a 320px screen = 8 columns)
        private const int BLOCK_SIZE = 40;
        private List<Point> _shuffledIndices = new List<Point>();
        private int _cols;
        private int _rows;

        public void Start(bool isTransitioningOut)
        {
            _isOut = isTransitioningOut;
            _timer = 0f;

            _cols = (int)Math.Ceiling((float)Global.VIRTUAL_WIDTH / BLOCK_SIZE);
            _rows = (int)Math.Ceiling((float)Global.VIRTUAL_HEIGHT / BLOCK_SIZE);

            _shuffledIndices.Clear();
            for (int y = 0; y < _rows; y++)
            {
                for (int x = 0; x < _cols; x++)
                {
                    _shuffledIndices.Add(new Point(x, y));
                }
            }

            var rng = new Random();
            int n = _shuffledIndices.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var value = _shuffledIndices[k];
                _shuffledIndices[k] = _shuffledIndices[n];
                _shuffledIndices[n] = value;
            }
        }

        public void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var global = ServiceLocator.Get<Global>();
            var pixel = ServiceLocator.Get<Texture2D>();

            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);
            int totalBlocks = _shuffledIndices.Count;

            int blocksToDraw = _isOut
                ? (int)(totalBlocks * progress)
                : (int)(totalBlocks * (1.0f - progress));

            for (int i = 0; i < blocksToDraw; i++)
            {
                if (i >= _shuffledIndices.Count) break;

                Point index = _shuffledIndices[i];
                Rectangle rect = new Rectangle(
                    index.X * BLOCK_SIZE,
                    index.Y * BLOCK_SIZE,
                    BLOCK_SIZE,
                    BLOCK_SIZE
                );

                spriteBatch.Draw(pixel, rect, global.Palette_Black);
            }

            if (_isOut && progress >= 0.95f)
            {
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), global.Palette_Black);
            }
        }

        // --- 5. SHUTTERS SLOW (Top/Bottom Close) ---
        public class ShuttersTransition : ITransitionEffect
        {
            private float _timer;
            private const float DURATION = 0.6f;
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

            public void Draw(SpriteBatch spriteBatch)
            {
                var global = ServiceLocator.Get<Global>();

                // Use Virtual Resolution
                int width = Global.VIRTUAL_WIDTH;
                int height = Global.VIRTUAL_HEIGHT;

                float progress = Math.Clamp(_timer / DURATION, 0f, 1f);
                float eased = _isOut ? Easing.EaseInOutExpo(progress) : Easing.EaseInQuad(1.0f - progress);

                int halfHeight = height / 2;
                int currentHeight = (int)(halfHeight * eased) + (_isOut && progress >= 0.9f ? 2 : 0);

                var pixel = ServiceLocator.Get<Texture2D>();

                // Top Shutter
                spriteBatch.Draw(pixel, new Rectangle(0, 0, width, currentHeight), global.Palette_Black);

                // Bottom Shutter
                spriteBatch.Draw(pixel, new Rectangle(0, height - currentHeight, width, currentHeight), global.Palette_Black);
            }
        }
    }
}