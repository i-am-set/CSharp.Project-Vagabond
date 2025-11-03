#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// A data container for a loaded and processed move animation sprite sheet.
    /// </summary>
    public class MoveAnimation
    {
        public Texture2D SpriteSheet { get; }
        public int FrameWidth { get; }
        public int FrameHeight { get; }
        public int FrameCount { get; }
        public List<Rectangle> SourceRectangles { get; } = new List<Rectangle>();

        public MoveAnimation(Texture2D spriteSheet)
        {
            SpriteSheet = spriteSheet;
            FrameHeight = spriteSheet.Height;
            FrameWidth = spriteSheet.Height; // Frame width is always equal to the sprite sheet's height.

            if (FrameWidth > 0)
            {
                FrameCount = spriteSheet.Width / FrameWidth;
                for (int i = 0; i < FrameCount; i++)
                {
                    SourceRectangles.Add(new Rectangle(i * FrameWidth, 0, FrameWidth, FrameHeight));
                }
            }
            else
            {
                FrameCount = 0;
            }
        }
    }
}
#nullable restore