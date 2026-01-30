using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Animations;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;

using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;

namespace ProjectVagabond
{
    public class TextureFactory
    {
        public TextureFactory() { }
        public Texture2D CreateColoredTexture(int width, int height, Color color)
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var texture = new Texture2D(graphicsDevice, width, height);
            var colorData = new Color[width * height];
            for (int i = 0; i < colorData.Length; i++)
            {
                colorData[i] = color;
            }
            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreateNoiseTexture(int width, int height)
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var texture = new Texture2D(graphicsDevice, width, height);
            var data = new Color[width * height];
            var random = new Random();

            for (int i = 0; i < data.Length; i++)
            {
                float val = (float)random.NextDouble();
                data[i] = new Color(val, val, val, 1.0f);
            }

            texture.SetData(data);
            return texture;
        }

        public Texture2D CreateTwoColorTexture(int width, int height, Color color1, Color color2)
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var texture = new Texture2D(graphicsDevice, width, height);
            var colorData = new Color[width * height];
            int midpoint = width / 2;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    colorData[y * width + x] = (x < midpoint) ? color1 : color2;
                }
            }
            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreatePlayerTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            int size = 5;
            var texture = new Texture2D(graphicsDevice, size, size);
            var colorData = new Color[size * size];

            for (int y = 0; y < size; y++) // Create a simple diamond shape for player
            {
                for (int x = 0; x < size; x++)
                {
                    int centerX = size / 2;
                    int centerY = size / 2;
                    int distance = Math.Abs(x - centerX) + Math.Abs(y - centerY);
                    if (distance <= 2)
                    {
                        colorData[y * size + x] = Color.White;
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreateSmallXTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int size = 5;
            const int margin = 1;
            var texture = new Texture2D(graphicsDevice, size, size);
            var colorData = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inInner = x >= margin && x < (size - margin) && y >= margin && y < (size - margin);
                    if (inInner)
                    {
                        int xi = x - margin;
                        int yi = y - margin;
                        int innerSize = size - 2 * margin;
                        if (xi == yi || xi == (innerSize - 1) - yi)
                            colorData[y * size + x] = Color.White;
                        else
                            colorData[y * size + x] = Color.Transparent;
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreateSelectionSquareTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int size = 5;
            const int centerGap = 3;
            int start = (size - centerGap) / 2;
            int end = start + centerGap;

            var texture = new Texture2D(graphicsDevice, size, size);
            var colorData = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool draw = false;
                    if (y == 0 && (x < start || x >= end)) draw = true;
                    if (y == size - 1 && (x < start || x >= end)) draw = true;
                    if (x == 0 && (y < start || y >= end)) draw = true;
                    if (x == size - 1 && (y < start || y >= end)) draw = true;
                    colorData[y * size + x] = draw ? Color.White : Color.Transparent;
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreateEmptyTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var texture = new Texture2D(graphicsDevice, 8, 8);
            var colorData = new Color[64];
            for (int i = 0; i < colorData.Length; i++)
            {
                colorData[i] = Color.Transparent;
            }
            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreateCircleTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int size = 64; // Match the CLOCK_SIZE for a 1:1 texture
            var texture = new Texture2D(graphicsDevice, size, size);
            var colorData = new Color[size * size];

            float radius = size / 2f;
            var center = new Vector2(radius - 0.5f, radius - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var position = new Vector2(x, y);
                    float distance = Vector2.Distance(center, position);

                    // Use a hard edge for a crisp, pixelated circle
                    if (distance <= radius)
                    {
                        colorData[y * size + x] = Color.White;
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        /// <summary>
        /// Creates a hollow ring texture for shockwave effects.
        /// </summary>
        public Texture2D CreateRingTexture(int size = 64, int thickness = 4)
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var texture = new Texture2D(graphicsDevice, size, size);
            var colorData = new Color[size * size];

            float radius = size / 2f;
            float innerRadius = radius - thickness;
            var center = new Vector2(radius - 0.5f, radius - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var position = new Vector2(x, y);
                    float distance = Vector2.Distance(center, position);

                    if (distance <= radius && distance >= innerRadius)
                    {
                        colorData[y * size + x] = Color.White;
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreateCircleParticleTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int size = 6;
            var texture = new Texture2D(graphicsDevice, size, size);
            var colorData = new Color[size * size];

            // A 6x6 pixel circle pattern
            bool[,] pattern = new bool[6, 6]
            {
            { false, false, true,  true,  false, false },
            { false, true,  true,  true,  true,  false },
            { true,  true,  true,  true,  true,  true  },
            { true,  true,  true,  true,  true,  true  },
            { false, true,  true,  true,  true,  false },
            { false, false, true,  true,  false, false }
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    colorData[y * size + x] = pattern[y, x] ? Color.White : Color.Transparent;
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreateSoftCircleParticleTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int size = 16;
            var texture = new Texture2D(graphicsDevice, size, size);
            var colorData = new Color[size * size];
            float radius = size / 2f;
            var center = new Vector2(radius - 0.5f, radius - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(center, new Vector2(x, y));
                    float progress = Math.Clamp(distance / radius, 0f, 1f);
                    // Use an ease-in quadratic falloff for a smooth gradient
                    float alpha = (1.0f - progress) * (1.0f - progress);
                    colorData[y * size + x] = new Color(Color.White, alpha);
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreateEnemyPlaceholderTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int width = 256;
            const int height = 256;
            var texture = new Texture2D(graphicsDevice, width, height);
            var colorData = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Simple gradient for some visual interest
                    float gradient = (float)y / height;
                    colorData[y * width + x] = Color.Lerp(Color.MediumPurple, Color.DarkSlateBlue, gradient);
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        /// <summary>
        /// Creates a seamless diagonal stripe pattern for mana bars.
        /// </summary>
        public Texture2D CreateManaPatternTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int width = 16;
            const int height = 16;
            var texture = new Texture2D(graphicsDevice, width, height);
            var colorData = new Color[width * height];

            // Create diagonal stripes
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Pattern: (x + y) % 4 < 2
                    // This creates diagonal bands of 2 pixels wide
                    bool isBand = (x + y) % 8 < 4;

                    if (isBand)
                    {
                        colorData[y * width + x] = Color.White; // Full opacity
                    }
                    else
                    {
                        colorData[y * width + x] = Color.White * 0.75f; // Half opacity
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        /// <summary>
        /// Creates a 3x3 plus-shaped texture for heal particles.
        /// </summary>
        public Texture2D CreatePlusParticleTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int size = 3;
            var texture = new Texture2D(graphicsDevice, size, size);
            var colorData = new Color[size * size];

            // 3x3 Plus Pattern:
            // . X .
            // X X X
            // . X .

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isFilled = (x == 1) || (y == 1);
                    colorData[y * size + x] = isFilled ? Color.White : Color.Transparent;
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        /// <summary>
        /// Creates a 6px tall button texture for the new action menu.
        /// </summary>
        public Texture2D CreateMiniActionButtonTexture(int width)
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int height = 6;
            var texture = new Texture2D(graphicsDevice, width, height);
            var colorData = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Simple white fill, tinting handled by renderer
                    colorData[y * width + x] = Color.White;
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        /// <summary>
        /// Creates a 9x3 texture containing Neutral, Up, and Down arrows (3x3 each).
        /// Used as a fallback for StatChangeIconsSpriteSheet.
        /// </summary>
        public Texture2D CreateStatArrowTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int iconSize = 3;
            const int width = iconSize * 3; // 9
            const int height = iconSize;    // 3
            var texture = new Texture2D(graphicsDevice, width, height);
            var colorData = new Color[width * height];

            // Clear to transparent
            for (int i = 0; i < colorData.Length; i++) colorData[i] = Color.Transparent;

            // Frame 0: Neutral (Dot)
            // . . .
            // . X .
            // . . .
            colorData[1 * width + 1] = Color.White;

            // Frame 1: Up Arrow
            // . X .
            // X X X
            // . . .
            int offset1 = iconSize; // X=3
            colorData[0 * width + (offset1 + 1)] = Color.White;
            colorData[1 * width + (offset1 + 0)] = Color.White;
            colorData[1 * width + (offset1 + 1)] = Color.White;
            colorData[1 * width + (offset1 + 2)] = Color.White;

            // Frame 2: Down Arrow
            // . . .
            // X X X
            // . X .
            int offset2 = iconSize * 2; // X=6
            colorData[1 * width + (offset2 + 0)] = Color.White;
            colorData[1 * width + (offset2 + 1)] = Color.White;
            colorData[1 * width + (offset2 + 2)] = Color.White;
            colorData[2 * width + (offset2 + 1)] = Color.White;

            texture.SetData(colorData);
            return texture;
        }
    }
}