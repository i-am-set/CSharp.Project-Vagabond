using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
namespace ProjectVagabond
{
    public class TextureFactory
    {
        public Texture2D CreateColoredTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(Core.Instance.GraphicsDevice, width, height);
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
            var texture = new Texture2D(Core.Instance.GraphicsDevice, 8, 8);
            var colorData = new Color[64];

            // Define a simple wave pattern using sine-like curves
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    // Create a wave pattern using a simple formula
                    bool isWave = (int)(2 * Math.Sin((x + y) * Math.PI / 4)) == y - 4;

                    if (isWave)
                    {
                        colorData[y * 8 + x] = new Color(0, 100, 255); // Bright blue wave crest
                    }
                    else
                    {
                        colorData[y * 8 + x] = new Color(0, 50, 150); // Darker blue water
                    }
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        public Texture2D CreatePlayerTexture()
        {
            var texture = new Texture2D(Core.Instance.GraphicsDevice, 8, 8);
            var colorData = new Color[64];

            for (int y = 0; y < 8; y++) // Create a simple diamond shape for player
            {
                for (int x = 0; x < 8; x++)
                {
                    int distance = Math.Abs(x - 4) + Math.Abs(y - 4);
                    if (distance <= 3)
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

        public Texture2D CreatePathTexture()
        {
            var texture = new Texture2D(Core.Instance.GraphicsDevice, 8, 8);
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
            var texture = new Texture2D(Core.Instance.GraphicsDevice, 8, 8);
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
            var texture = new Texture2D(Core.Instance.GraphicsDevice, 8, 8);
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

        public Texture2D CreateEmptyTexture()
        {
            var texture = new Texture2D(Core.Instance.GraphicsDevice, 8, 8);
            var colorData = new Color[64];

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    colorData[y * 8 + x] = Color.Transparent;
                }
            }

            texture.SetData(colorData);
            return texture;
        }
    }
}
