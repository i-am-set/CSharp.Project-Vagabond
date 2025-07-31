using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectVagabond.UI
{
    /// <summary>
    /// A static class containing helper methods for drawing common, primitive UI elements.
    /// </summary>
    public static class UIPrimitives
    {
        /// <summary>
        /// Draws a bar composed of discrete segments, often used for health or energy displays.
        /// </summary>
        /// <param name="spriteBatch">The SpriteBatch to draw with.</param>
        /// <param name="pixel">A 1x1 white pixel texture.</param>
        /// <param name="bounds">The total area the bar's background should occupy.</param>
        /// <param name="progress">The progress of the bar, from 0.0f to 1.0f.</param>
        /// <param name="maxSegments">The total number of segments the bar is divided into.</param>
        /// <param name="fillColor">The color of the filled segments.</param>
        /// <param name="emptyColor">The color of the empty segments.</param>
        /// <param name="bgColor">The color of the bar's background, visible in the gaps and padding.</param>
        /// <param name="segmentWidth">The width of an individual segment in pixels.</param>
        /// <param name="segmentGap">The width of the gap between segments in pixels.</param>
        /// <param name="segmentHeight">The height of an individual segment in pixels.</param>
        /// <param name="horizontalPadding">The padding on the left and right side of the segments, inside the background.</param>
        public static void DrawSegmentedBar(
            SpriteBatch spriteBatch,
            Texture2D pixel,
            Rectangle bounds,
            float progress,
            int maxSegments,
            Color fillColor,
            Color emptyColor,
            Color bgColor,
            int segmentWidth,
            int segmentGap,
            int segmentHeight,
            int horizontalPadding)
        {
            // Draw background
            spriteBatch.Draw(pixel, bounds, bgColor);

            if (maxSegments <= 0) return;

            int filledSegments = (int)(progress * maxSegments);
            int totalSegmentWidth = segmentWidth + segmentGap;
            int segmentsStartX = bounds.X + horizontalPadding;

            for (int i = 0; i < maxSegments; i++)
            {
                int segmentX = segmentsStartX + (i * totalSegmentWidth);
                // Center the segment vertically in the bounds
                int segmentY = bounds.Y + (bounds.Height - segmentHeight) / 2;

                Rectangle segmentRect = new Rectangle(segmentX, segmentY, segmentWidth, segmentHeight);

                Color segmentColor = (i < filledSegments) ? fillColor : emptyColor;
                spriteBatch.Draw(pixel, segmentRect, segmentColor);
            }
        }
    }
}