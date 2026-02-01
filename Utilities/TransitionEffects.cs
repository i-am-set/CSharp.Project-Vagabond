using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

    public interface ITransitionEffect
    {
        bool IsComplete { get; }
        void Start(bool isTransitioningOut);
        void Update(GameTime gameTime);
        void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale);
        float GetProgress();
    }

    // --- 1. SHUTTER (Vertical Curtain) ---
    public class ShutterTransition : ITransitionEffect
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

        public float GetProgress() => Math.Clamp(_timer / DURATION, 0f, 1f);

        public void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            float width = screenSize.X;
            float height = screenSize.Y;

            float progress = GetProgress();
            float eased = _isOut ? Easing.EaseInExpo(progress) : Easing.EaseInExpo(1.0f - progress);

            float halfHeight = height / 2f;
            float currentHeight = halfHeight * eased * 1.05f;

            // Top Shutter
            spriteBatch.Draw(pixel, new Rectangle(0, 0, (int)width, (int)currentHeight), Color.Black);

            // Bottom Shutter
            spriteBatch.Draw(pixel, new Rectangle(0, (int)(height - currentHeight), (int)width, (int)currentHeight), Color.Black);
        }
    }

    // --- 2. CURTAIN (Horizontal Curtain) ---
    public class CurtainTransition : ITransitionEffect
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

        public float GetProgress() => Math.Clamp(_timer / DURATION, 0f, 1f);

        public void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            float width = screenSize.X;
            float height = screenSize.Y;

            float progress = GetProgress();
            float eased = _isOut ? Easing.EaseInExpo(progress) : Easing.EaseInExpo(1.0f - progress);

            float halfWidth = width / 2f;
            float currentWidth = halfWidth * eased * 1.05f;

            // Left Curtain
            spriteBatch.Draw(pixel, new Rectangle(0, 0, (int)currentWidth, (int)height), Color.Black);

            // Right Curtain
            spriteBatch.Draw(pixel, new Rectangle((int)(width - currentWidth), 0, (int)currentWidth, (int)height), Color.Black);
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

        public float GetProgress() => Math.Clamp(_timer / DURATION, 0f, 1f);

        public void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            float width = screenSize.X;
            float height = screenSize.Y;

            float progress = GetProgress();
            float eased = _isOut ? Easing.EaseInExpo(progress) : Easing.EaseInExpo(1.0f - progress);

            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            float curW = halfWidth * eased * 1.05f;
            float curH = halfHeight * eased * 1.05f;

            // Top
            spriteBatch.Draw(pixel, new Rectangle(0, 0, (int)width, (int)curH), Color.Black);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(0, (int)(height - curH), (int)width, (int)curH), Color.Black);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(0, 0, (int)curW, (int)height), Color.Black);
            // Right
            spriteBatch.Draw(pixel, new Rectangle((int)(width - curW), 0, (int)curW, (int)height), Color.Black);
        }
    }

    // --- 4. CENTER SQUARE (Expands/Collapses, No Spin) ---
    public class CenterSquareTransition : ITransitionEffect
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

        public float GetProgress() => Math.Clamp(_timer / DURATION, 0f, 1f);

        public void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            float progress = GetProgress();

            float eased = _isOut ? Easing.EaseInCubic(progress) : Easing.EaseInCubic(1.0f - progress);

            float maxDimension = (float)Math.Sqrt(screenSize.X * screenSize.X + screenSize.Y * screenSize.Y);
            float currentSize = maxDimension * eased;

            Vector2 center = screenSize / 2f;
            Vector2 origin = new Vector2(0.5f, 0.5f);
            Vector2 scaleVec = new Vector2(currentSize, currentSize);

            spriteBatch.Draw(
                pixel,
                center,
                null,
                Color.Black,
                0f,
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

        public float GetProgress() => Math.Clamp(_timer / DURATION, 0f, 1f);

        public void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            float progress = GetProgress();

            float eased = _isOut ? Easing.EaseInQuart(progress) : Easing.EaseInQuart(1.0f - progress);

            float maxDimension = (float)Math.Sqrt(screenSize.X * screenSize.X + screenSize.Y * screenSize.Y) * 1.5f * 1.2f;
            float currentSize = maxDimension * eased;

            Vector2 center = screenSize / 2f;
            Vector2 origin = new Vector2(0.5f, 0.5f);
            float rotation = MathHelper.PiOver4;

            if (currentSize > 0)
            {
                spriteBatch.Draw(
                    pixel,
                    center,
                    null,
                    Color.Black,
                    rotation,
                    origin,
                    new Vector2(currentSize, currentSize),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }

    // --- 6. SPINNING SQUARE (Rotates and Expands) ---
    public class SpinningSquareTransition : ITransitionEffect
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

        public float GetProgress() => Math.Clamp(_timer / DURATION, 0f, 1f);

        public void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            float progress = GetProgress();

            float eased = _isOut ? Easing.EaseInExpo(progress) : (1.0f - Easing.EaseOutExpo(progress));

            float maxDimension = (float)Math.Sqrt(screenSize.X * screenSize.X + screenSize.Y * screenSize.Y) * 1.2f;
            float currentSize = maxDimension * eased;

            float rotation = eased * MathHelper.Pi;

            Vector2 center = screenSize / 2f;
            Vector2 origin = new Vector2(0.5f, 0.5f);

            if (currentSize > 0)
            {
                spriteBatch.Draw(
                    pixel,
                    center,
                    null,
                    Color.Black,
                    rotation,
                    origin,
                    new Vector2(currentSize, currentSize),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }

    // --- 7. DIAMONDS (Grid based) ---
    public class DiamondWipeTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.6f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        private const int BASE_GRID_SIZE = 24;

        public void Start(bool isTransitioningOut)
        {
            _isOut = isTransitioningOut;
            _timer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public float GetProgress() => Math.Clamp(_timer / DURATION, 0f, 1f);

        public void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            float scaledGridSize = BASE_GRID_SIZE * contentScale;
            int cols = (int)Math.Ceiling(screenSize.X / scaledGridSize) + 1;
            int rows = (int)Math.Ceiling(screenSize.Y / scaledGridSize) + 1;

            float maxDelay = DURATION * 0.5f;
            float growTime = DURATION * 0.5f;

            Vector2 origin = new Vector2(0.5f, 0.5f);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    float delay = ((float)(x + y) / (cols + rows)) * maxDelay;
                    float localTime = _timer - delay;
                    float progress = Math.Clamp(localTime / growTime, 0f, 1f);

                    float scaleVal;
                    if (_isOut)
                    {
                        scaleVal = Easing.EaseOutBack(progress);
                    }
                    else
                    {
                        scaleVal = Easing.EaseInBack(1.0f - progress);
                    }

                    if (scaleVal > 0)
                    {
                        Vector2 center = new Vector2(x * scaledGridSize, y * scaledGridSize);
                        float size = scaledGridSize * 1.5f * scaleVal;
                        float rotation = MathHelper.PiOver4 + (progress * 0.5f);

                        spriteBatch.Draw(
                            pixel,
                            center,
                            null,
                            Color.Black,
                            rotation,
                            origin,
                            new Vector2(size, size),
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }

            if (_isOut && _timer > DURATION * 0.95f)
            {
                spriteBatch.Draw(pixel, Vector2.Zero, null, Color.Black, 0f, Vector2.Zero, screenSize, SpriteEffects.None, 0f);
            }
        }
    }

    // --- 8. BIG BLOCKS EASE ---
    public class BigBlocksEaseTransition : ITransitionEffect
    {
        private float _timer;
        private const float DURATION = 0.5f;
        private bool _isOut;
        public bool IsComplete => _timer >= DURATION;

        private const int BASE_BLOCK_SIZE = 40;

        public void Start(bool isTransitioningOut)
        {
            _isOut = isTransitioningOut;
            _timer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public float GetProgress() => Math.Clamp(_timer / DURATION, 0f, 1f);

        public void Draw(SpriteBatch spriteBatch, Vector2 screenSize, float contentScale)
        {
            var pixel = ServiceLocator.Get<Texture2D>();

            float scaledBlockSize = BASE_BLOCK_SIZE * contentScale;
            int cols = (int)Math.Ceiling(screenSize.X / scaledBlockSize);
            int rows = (int)Math.Ceiling(screenSize.Y / scaledBlockSize);

            float maxDelay = DURATION * 0.5f;
            float growTime = DURATION * 0.5f;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    float delay = ((float)(x + y) / (cols + rows)) * maxDelay;
                    float localTime = _timer - delay;
                    float progress = Math.Clamp(localTime / growTime, 0f, 1f);

                    float eased = Easing.EaseInCubic(progress);
                    float sizeScale = _isOut ? eased : 1.0f - eased;

                    if (sizeScale > 0.01f)
                    {
                        float finalScale = sizeScale * 1.05f; // Slight overlap
                        float size = scaledBlockSize * finalScale;

                        float centerX = x * scaledBlockSize + scaledBlockSize / 2f;
                        float centerY = y * scaledBlockSize + scaledBlockSize / 2f;

                        spriteBatch.Draw(
                            pixel,
                            new Vector2(centerX, centerY),
                            null,
                            Color.Black,
                            0f,
                            new Vector2(0.5f, 0.5f),
                            new Vector2(size, size),
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }

            if (_isOut && _timer > DURATION * 0.95f)
            {
                spriteBatch.Draw(pixel, Vector2.Zero, null, Color.Black, 0f, Vector2.Zero, screenSize, SpriteEffects.None, 0f);
            }
        }
    }
}