using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Transitions
{
    /// <summary>
    /// Helper class for deterministic, allocation-free pseudo-random numbers in transitions.
    /// </summary>
    internal static class TransitionMath
    {
        public static float Hash(int x, int y)
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }

    // --- 1. SHUTTER (Vertical Curtain) ---
    public class ShutterTransition : ITransitionEffect
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

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale)
        {
            int width = bounds.Width;
            int height = bounds.Height;

            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);
            float eased = _isOut ? Easing.EaseInOutCubic(progress) : Easing.EaseInCubic(1.0f - progress);

            int halfHeight = height / 2;
            int currentHeight = (int)(halfHeight * eased);

            // Overlap fix
            if (_isOut && progress >= 0.9f) currentHeight = halfHeight + 2;

            var pixel = ServiceLocator.Get<Texture2D>();

            // Top Shutter
            spriteBatch.Draw(pixel, new Rectangle(0, 0, width, currentHeight), Color.Black);

            // Bottom Shutter
            spriteBatch.Draw(pixel, new Rectangle(0, height - currentHeight, width, currentHeight), Color.Black);
        }
    }

    // --- 2. CURTAIN (Horizontal Curtain) ---
    public class CurtainTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.4f;
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
            var pixel = ServiceLocator.Get<Texture2D>();
            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);

            float eased = _isOut ? Easing.EaseInOutCubic(progress) : Easing.EaseInCubic(1.0f - progress);

            int halfWidth = bounds.Width / 2;
            int currentWidth = (int)(halfWidth * eased);

            if (_isOut && progress >= 0.99f) currentWidth = halfWidth + 2;

            // Left Curtain
            spriteBatch.Draw(pixel, new Rectangle(0, 0, currentWidth, bounds.Height), Color.Black);

            // Right Curtain
            spriteBatch.Draw(pixel, new Rectangle(bounds.Width - currentWidth, 0, currentWidth, bounds.Height), Color.Black);
        }
    }

    // --- 3. APERTURE (Closes from all 4 sides) ---
    public class ApertureTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.5f;
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
            var pixel = ServiceLocator.Get<Texture2D>();
            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);

            float eased = _isOut ? Easing.EaseInOutCubic(progress) : Easing.EaseInCubic(1.0f - progress);

            int halfWidth = bounds.Width / 2;
            int halfHeight = bounds.Height / 2;

            int curW = (int)(halfWidth * eased);
            int curH = (int)(halfHeight * eased);

            if (_isOut && progress >= 0.99f)
            {
                curW = halfWidth + 2;
                curH = halfHeight + 2;
            }

            // Draw 4 overlapping rectangles to cover the screen
            // Top
            spriteBatch.Draw(pixel, new Rectangle(0, 0, bounds.Width, curH), Color.Black);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(0, bounds.Height - curH, bounds.Width, curH), Color.Black);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(0, 0, curW, bounds.Height), Color.Black);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(bounds.Width - curW, 0, curW, bounds.Height), Color.Black);
        }
    }

    // --- 4. CENTER SQUARE (Expands/Collapses, No Spin) ---
    public class CenterSquareTransition : ITransitionEffect
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

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float scale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);

            float eased = _isOut ? Easing.EaseInQuint(progress) : Easing.EaseInQuint(1.0f - progress);

            // Calculate max size needed to cover screen (diagonal length)
            float maxDimension = (float)Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height);
            float currentSize = maxDimension * eased;

            Vector2 center = new Vector2(bounds.Center.X, bounds.Center.Y);
            Vector2 origin = new Vector2(0.5f, 0.5f);
            Vector2 scaleVec = new Vector2(currentSize, currentSize);

            spriteBatch.Draw(
                pixel,
                center,
                null,
                Color.Black,
                0f, // No rotation
                origin,
                scaleVec,
                SpriteEffects.None,
                0f
            );
        }
    }

    // --- 5. CENTER DIAMOND (Expands/Collapses, Rotated 45 deg) ---
    public class CenterDiamondTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.7f;
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
            var pixel = ServiceLocator.Get<Texture2D>();
            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);

            float eased = _isOut ? Easing.EaseInQuint(progress) : Easing.EaseInQuint(1.0f - progress);

            // Calculate size needed to cover screen when rotated 45 degrees
            // Diagonal of screen is roughly the diameter needed, multiplied by 1.5 to be safe
            float maxDimension = (float)Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) * 1.5f;
            float currentSize = maxDimension * eased;

            Vector2 center = new Vector2(bounds.Center.X, bounds.Center.Y);
            Vector2 origin = new Vector2(0.5f, 0.5f);

            spriteBatch.Draw(
                pixel,
                center,
                null,
                Color.Black,
                MathHelper.PiOver4, // 45 degrees
                origin,
                new Vector2(currentSize, currentSize),
                SpriteEffects.None,
                0f
            );
        }
    }

    // --- 6. SPINNING SQUARE (Rotates and Expands) ---
    public class SpinningSquareTransition : ITransitionEffect
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
            var pixel = ServiceLocator.Get<Texture2D>();
            float progress = Math.Clamp(_timer / DURATION, 0f, 1f);

            float eased = _isOut ? Easing.EaseInOutExpo(progress) : Easing.EaseInOutQuart(1.0f - progress);

            float maxDimension = (float)Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height);
            float currentSize = maxDimension * eased;
            float rotation = eased * MathHelper.Pi;

            Vector2 center = new Vector2(bounds.Center.X, bounds.Center.Y);
            Vector2 origin = new Vector2(0.5f, 0.5f);
            Vector2 scaleVec = new Vector2(currentSize, currentSize);

            spriteBatch.Draw(
                pixel,
                center,
                null,
                Color.Black,
                rotation,
                origin,
                scaleVec,
                SpriteEffects.None,
                0f
            );
        }
    }

    // --- 7. DIAMONDS (Grid based) ---
    public class DiamondWipeTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 1.0f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

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
            float scaledGridSize = GRID_SIZE * scale;

            int cols = (int)Math.Ceiling(width / scaledGridSize) + 2;
            int rows = (int)Math.Ceiling(height / scaledGridSize) + 2;

            float maxDelay = 0.5f;
            float growTime = 0.5f;

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
                        float size = scaledGridSize * 1.8f * sizeScale;

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

    // --- 8. BIG BLOCKS EASE ---
    public class BigBlocksEaseTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.8f;
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

            float maxDelay = 0.4f;
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
                        float finalScale = sizeScale * 1.05f;
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
}