#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// Creates a dynamic, "bubbling" void effect around a rectangular area using Perlin noise.
    /// This effect is rendered by generating textures on the fly for each edge, which is highly performant.
    /// </summary>
    public class VoidEdgeEffect
    {
        #region Nested Perlin Noise Class
        /// <summary>
        /// A self-contained implementation of classic 2D Perlin noise.
        /// </summary>
        private class SeededPerlin
        {
            private readonly int[] p = new int[512];

            public SeededPerlin(int seed)
            {
                var rng = new Random(seed);
                var permutation = new int[256];
                for (int i = 0; i < 256; i++)
                {
                    permutation[i] = i;
                }

                for (int i = permutation.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
                }

                for (int i = 0; i < 256; i++)
                {
                    p[i] = p[256 + i] = permutation[i];
                }
            }

            public float Noise(float x, float y)
            {
                int X = (int)Math.Floor(x) & 255;
                int Y = (int)Math.Floor(y) & 255;

                x -= (float)Math.Floor(x);
                y -= (float)Math.Floor(y);

                float u = Fade(x);
                float v = Fade(y);

                int A = p[X] + Y;
                int B = p[X + 1] + Y;

                return Lerp(v, Lerp(u, Grad(p[A], x, y), Grad(p[B], x - 1, y)),
                               Lerp(u, Grad(p[A + 1], x, y - 1), Grad(p[B + 1], x - 1, y - 1)));
            }

            private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
            private static float Lerp(float t, float a, float b) => a + t * (b - a);
            private static float Grad(int hash, float x, float y)
            {
                int h = hash & 7;
                float u = h < 4 ? x : y;
                float v = h < 4 ? y : x;
                return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
            }
        }
        #endregion

        // --- Tuning Parameters ---
        public Color EdgeColor { get; set; }
        public int EdgeWidth { get; set; }
        public float NoiseScale { get; set; }
        public float NoiseSpeed { get; set; }

        private readonly SeededPerlin _noise;
        private float _time;

        private readonly GraphicsDevice _graphicsDevice;
        private Texture2D? _topTexture;
        private Texture2D? _bottomTexture;
        private Texture2D? _leftTexture;
        private Texture2D? _rightTexture;

        private Color[]? _topData;
        private Color[]? _bottomData;
        private Color[]? _leftData;
        private Color[]? _rightData;

        public VoidEdgeEffect(Color edgeColor, int edgeWidth, float noiseScale, float noiseSpeed)
        {
            EdgeColor = edgeColor;
            EdgeWidth = edgeWidth;
            NoiseScale = noiseScale;
            NoiseSpeed = noiseSpeed;

            _graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            _noise = new SeededPerlin(Environment.TickCount);
        }

        public void Update(GameTime gameTime, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            _time += (float)gameTime.ElapsedGameTime.TotalSeconds * NoiseSpeed;

            // Recreate textures and data arrays if the bounds have changed
            if (_topTexture == null || _topTexture.Width != bounds.Width || _topTexture.Height != EdgeWidth)
            {
                _topTexture?.Dispose();
                _topTexture = new Texture2D(_graphicsDevice, bounds.Width, EdgeWidth);
                _topData = new Color[bounds.Width * EdgeWidth];
            }
            if (_bottomTexture == null || _bottomTexture.Width != bounds.Width || _bottomTexture.Height != EdgeWidth)
            {
                _bottomTexture?.Dispose();
                _bottomTexture = new Texture2D(_graphicsDevice, bounds.Width, EdgeWidth);
                _bottomData = new Color[bounds.Width * EdgeWidth];
            }
            if (_leftTexture == null || _leftTexture.Width != EdgeWidth || _leftTexture.Height != bounds.Height)
            {
                _leftTexture?.Dispose();
                _leftTexture = new Texture2D(_graphicsDevice, EdgeWidth, bounds.Height);
                _leftData = new Color[EdgeWidth * bounds.Height];
            }
            if (_rightTexture == null || _rightTexture.Width != EdgeWidth || _rightTexture.Height != bounds.Height)
            {
                _rightTexture?.Dispose();
                _rightTexture = new Texture2D(_graphicsDevice, EdgeWidth, bounds.Height);
                _rightData = new Color[EdgeWidth * bounds.Height];
            }

            // Generate noise and color data for each edge texture
            UpdateTopTexture(bounds.Width);
            UpdateBottomTexture(bounds.Width);
            UpdateLeftTexture(bounds.Height);
            UpdateRightTexture(bounds.Height);
        }

        private void UpdateTopTexture(int width)
        {
            for (int x = 0; x < width; x++)
            {
                float noiseVal = (_noise.Noise(x * NoiseScale, _time * 0.1f) + 1f) * 0.5f; // Range [0, 1]
                int length = (int)(noiseVal * EdgeWidth);

                for (int y = 0; y < EdgeWidth; y++)
                {
                    int index = y * width + x;
                    if (y < length)
                    {
                        _topData![index] = EdgeColor;
                    }
                    else
                    {
                        _topData![index] = Color.Transparent;
                    }
                }
            }
            _topTexture!.SetData(_topData);
        }

        private void UpdateBottomTexture(int width)
        {
            for (int x = 0; x < width; x++)
            {
                float noiseVal = (_noise.Noise(x * NoiseScale, _time * 0.1f + 1000f) + 1f) * 0.5f;
                int length = (int)(noiseVal * EdgeWidth);

                for (int y = 0; y < EdgeWidth; y++)
                {
                    int index = y * width + x;
                    if (y < length)
                    {
                        _bottomData![index] = EdgeColor;
                    }
                    else
                    {
                        _bottomData![index] = Color.Transparent;
                    }
                }
            }
            _bottomTexture!.SetData(_bottomData);
        }

        private void UpdateLeftTexture(int height)
        {
            for (int y = 0; y < height; y++)
            {
                float noiseVal = (_noise.Noise(_time * 0.1f + 2000f, y * NoiseScale) + 1f) * 0.5f;
                int length = (int)(noiseVal * EdgeWidth);

                for (int x = 0; x < EdgeWidth; x++)
                {
                    int index = y * EdgeWidth + x;
                    if (x < length)
                    {
                        _leftData![index] = EdgeColor;
                    }
                    else
                    {
                        _leftData![index] = Color.Transparent;
                    }
                }
            }
            _leftTexture!.SetData(_leftData);
        }

        private void UpdateRightTexture(int height)
        {
            for (int y = 0; y < height; y++)
            {
                float noiseVal = (_noise.Noise(_time * 0.1f + 3000f, y * NoiseScale) + 1f) * 0.5f;
                int length = (int)(noiseVal * EdgeWidth);

                for (int x = 0; x < EdgeWidth; x++)
                {
                    int index = y * EdgeWidth + x;
                    if (x < length)
                    {
                        _rightData![index] = EdgeColor;
                    }
                    else
                    {
                        _rightData![index] = Color.Transparent;
                    }
                }
            }
            _rightTexture!.SetData(_rightData);
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (_topTexture == null || _bottomTexture == null || _leftTexture == null || _rightTexture == null)
            {
                return;
            }

            // Top
            spriteBatch.DrawSnapped(_topTexture, new Vector2(bounds.Left, bounds.Top), Color.White);
            // Bottom
            spriteBatch.DrawSnapped(_bottomTexture, new Vector2(bounds.Left, bounds.Bottom), null, Color.White, 0f, new Vector2(0, _bottomTexture.Height), 1f, SpriteEffects.FlipVertically, 0f);
            // Left
            spriteBatch.DrawSnapped(_leftTexture, new Vector2(bounds.Left, bounds.Top), Color.White);
            // Right
            spriteBatch.DrawSnapped(_rightTexture, new Vector2(bounds.Right, bounds.Top), null, Color.White, 0f, new Vector2(_rightTexture.Width, 0), 1f, SpriteEffects.FlipHorizontally, 0f);
        }
    }
}