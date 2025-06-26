using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using System;

namespace ProjectVagabond
{
    public class ClockRenderer
    {
        private Vector2 _clockPosition;
        private const int CLOCK_SIZE = 64;

        public void DrawClock(SpriteBatch spriteBatch)
        {
            var clockManager = Core.CurrentWorldClockManager;
            var pixel = Core.Pixel;
            var font = Global.Instance.DefaultFont;

            int statsBaseX = 50;
            int statsBaseY = 50 + Global.GRID_SIZE * Global.GRID_CELL_SIZE + 10;
            int statsHeight = 14 + 16 + Global.FONT_SIZE;
            _clockPosition = new Vector2(statsBaseX, statsBaseY + statsHeight + 10); // 10px padding

            Vector2 clockCenter = _clockPosition + new Vector2(CLOCK_SIZE / 2f, CLOCK_SIZE / 2f);

            // Draw hour marker dots
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            const int DOT_RADIUS = CLOCK_SIZE / 2 - 3;
            const int DOT_SIZE = 2;
            for (int i = 1; i <= 12; i++)
            {
                float angle = (i / 12f) * MathHelper.TwoPi - MathHelper.PiOver2;

                Vector2 dotPosition = new Vector2(
                    clockCenter.X + DOT_RADIUS * (float)Math.Cos(angle),
                    clockCenter.Y + DOT_RADIUS * (float)Math.Sin(angle)
                );

                spriteBatch.Draw(pixel,
                    new Rectangle((int)(dotPosition.X - DOT_SIZE / 2f), (int)(dotPosition.Y - DOT_SIZE / 2f), DOT_SIZE, DOT_SIZE),
                    Global.Instance.Palette_BrightWhite);
            }

            // Get current time from the WorldClockManager
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            int hour = clockManager.CurrentHour;
            int minute = clockManager.CurrentMinute;
            int second = clockManager.CurrentSecond;

            // Calculate hand rotations in radians.
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            float secondRotation = (second / 60f) * MathHelper.TwoPi - MathHelper.PiOver2;
            
            float minuteRotation = (minute / 60f) * MathHelper.TwoPi - MathHelper.PiOver2;

            float hourRotation = (((hour % 12) + minute / 60f) / 12f) * MathHelper.TwoPi - MathHelper.PiOver2;

            // Draw AM/PM text
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            string period = hour >= 12 ? "PM" : "AM";
            Vector2 periodSize = font.MeasureString(period);
            Vector2 periodPosition = new Vector2(
                clockCenter.X - periodSize.X / 2,
                _clockPosition.Y + CLOCK_SIZE * 0.7f - periodSize.Y / 2
            );
            spriteBatch.DrawString(font, period, periodPosition, Global.Instance.Palette_BrightWhite);

            // Define hand properties
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            Vector2 handOrigin = new Vector2(0, 0.5f);
            int hourHandLength = 16;
            int minuteHandLength = 24;
            int secondHandLength = 24;

            // Draw hands using the 1x1 pixel texture, rotated and scaled to form lines
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            // Hour hand
            spriteBatch.Draw(pixel, clockCenter, null, Global.Instance.Palette_BrightWhite, hourRotation, handOrigin, new Vector2(hourHandLength, 2), SpriteEffects.None, 0);
            // Minute hand
            spriteBatch.Draw(pixel, clockCenter, null, Global.Instance.Palette_BrightWhite, minuteRotation, handOrigin, new Vector2(minuteHandLength, 2), SpriteEffects.None, 0);
            // Second hand
            spriteBatch.Draw(pixel, clockCenter, null, Global.Instance.Palette_Red, secondRotation, handOrigin, new Vector2(secondHandLength, 1), SpriteEffects.None, 0);
        }
    }
}