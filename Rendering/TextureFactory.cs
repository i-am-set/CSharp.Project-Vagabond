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
            int size = 10;
            var texture = new Texture2D(Core.Instance.GraphicsDevice, size, size);
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

        public Texture2D CreateSmallXTexture()
        {
            const int size = 10;
            const int margin = 3;
            var texture = new Texture2D(Core.Instance.GraphicsDevice, size, size);
            var colorData = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // check if (x,y) is inside the inner square
                    bool inInner = x >= margin && x < (size - margin)
                                && y >= margin && y < (size - margin);

                    if (inInner)
                    {
                        // rebase to [0..innerSize-1]
                        int xi = x - margin;
                        int yi = y - margin;
                        int innerSize = size - 2 * margin;  

                        // draw the two diagonals of the inner square
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
            const int size = 10;
            const int centerGap = 4;                // how many pixels to leave blank at the center of each side
            int start = (size - centerGap) / 2;     // for size=10, start=4
            int end = start + centerGap;            // end=6 (so pixels 4 and 5 are skipped)

            var texture = new Texture2D(Core.Instance.GraphicsDevice, size, size);
            var colorData = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool draw = false;

                    // Top edge, except the center gap
                    if (y == 0 && (x < start || x >= end))
                        draw = true;

                    // Bottom edge, except the center gap
                    if (y == size - 1 && (x < start || x >= end))
                        draw = true;

                    // Left edge, except the center gap
                    if (x == 0 && (y < start || y >= end))
                        draw = true;

                    // Right edge, except the center gap
                    if (x == size - 1 && (y < start || y >= end))
                        draw = true;

                    colorData[y * size + x] = draw ? Color.White : Color.Transparent;
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

        public Texture2D CreateCircleTexture()
        {
            const int size = 16;
            var texture = new Texture2D(Core.Instance.GraphicsDevice, size, size);
            var colorData = new Color[size * size];

            float radius = size / 2f;
            var center = new Vector2(radius - 0.5f, radius - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var position = new Vector2(x, y);
                    float distance = Vector2.Distance(center, position);

                    float alpha = Math.Clamp(1.0f - (distance - (radius - 1)), 0, 1);

                    colorData[y * size + x] = Color.White * alpha;
                }
            }

            texture.SetData(colorData);
            return texture;
        }
    }
}
