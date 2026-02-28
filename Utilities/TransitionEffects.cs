using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

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
            float eased = _isOut ? Easing.EaseInCubic(progress) : Easing.EaseInCubic(1.0f - progress);

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
            float eased = _isOut ? Easing.EaseInCubic(progress) : Easing.EaseInCubic(1.0f - progress);

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
            float eased = _isOut ? Easing.EaseInCubic(progress) : Easing.EaseInCubic(1.0f - progress);

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

            float eased = _isOut ? Easing.EaseInCubic(progress) : Easing.EaseInCubic(1.0f - progress);

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

            float eased = _isOut ? Easing.EaseInCubic(progress) : (1.0f - Easing.EaseOutCubic(progress));

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

    // --- 7. FADE OFF (Simple Fade to Palette_Off) ---
    public class FadeOffTransition : ITransitionEffect
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
            var global = ServiceLocator.Get<Global>();

            float progress = GetProgress();
            float alpha = _isOut ? progress : (1.0f - progress);

            spriteBatch.Draw(pixel, new Rectangle(0, 0, (int)screenSize.X, (int)screenSize.Y), global.Palette_Off * alpha);
        }
    }
}