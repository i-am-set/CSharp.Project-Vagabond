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

        public MoveAnimation(Texture2D spriteSheet, int frameWidth, int frameHeight)
        {
            SpriteSheet = spriteSheet;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;

            // Ensure valid dimensions to prevent division by zero or invalid rects
            if (FrameWidth > 0 && FrameHeight > 0 && spriteSheet.Width >= FrameWidth)
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