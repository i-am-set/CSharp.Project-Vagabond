using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

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

        public Texture2D CreateWaterTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var texture = new Texture2D(graphicsDevice, 8, 8);
            var colorData = new Color[64];

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    bool isWave = (int)(2 * Math.Sin((x + y) * Math.PI / 4)) == y - 4;

                    if (isWave)
                    {
                        colorData[y * 8 + x] = new Color(0, 100, 255);
                    }
                    else
                    {
                        colorData[y * 8 + x] = new Color(0, 50, 150);
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreatePlayerTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            int size = 10;
            var texture = new Texture2D(graphicsDevice, size, size);
            var colorData = new Color[size * size];

            for (int y = 0; y < size; y++) // Create a simple diamond shape for player
            {
                for (int x = 0; x < size; x++)
                {
                    int distance = Math.Abs(x - size / 2) + Math.Abs(y - size / 2);
                    if (distance <= 4)
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

        public Texture2D CreatePathTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var texture = new Texture2D(graphicsDevice, 8, 8);
            var colorData = new Color[64];

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    if ((x == 3 || x == 4) && (y == 3 || y == 4))
                    {
                        colorData[y * 8 + x] = Color.White;
                    }
                    else
                    {
                        colorData[y * 8 + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }


        public Texture2D CreateRunPathTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var texture = new Texture2D(graphicsDevice, 8, 8);
            var colorData = new Color[64];

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    if (x >= 2 && x <= 5 && y >= 2 && y <= 5)
                    {
                        colorData[y * 8 + x] = Color.White;
                    }
                    else
                    {
                        colorData[y * 8 + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreatePathEndTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            var texture = new Texture2D(graphicsDevice, 8, 8);
            var colorData = new Color[64];

            for (int y = 0; y < 8; y++) // Create an X shape for path end
            {
                for (int x = 0; x < 8; x++)
                {
                    if (x == y || x == (7 - y))
                    {
                        colorData[y * 8 + x] = Color.White;
                    }
                    else
                    {
                        colorData[y * 8 + x] = Color.Transparent;
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreateSmallXTexture()
        {
            var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            const int size = 10;
            const int margin = 3;
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
            const int size = 10;
            const int centerGap = 4;
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
    }
}