﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;

namespace ProjectVagabond
{
    public class ClockRenderer
    {
        private Vector2 _clockPosition;
        private const int CLOCK_SIZE = 64;
        private readonly RadioGroup _timeScaleGroup;

        public RadioGroup TimeScaleGroup => _timeScaleGroup;

        public ClockRenderer()
        {
            _timeScaleGroup = new RadioGroup(defaultIndex: 0);

            _timeScaleGroup.AddButton(new ToggleButton(Rectangle.Empty, $"{Global.Instance.TimeScaleMultiplier1}x"));
            _timeScaleGroup.AddButton(new ToggleButton(Rectangle.Empty, $"{Global.Instance.TimeScaleMultiplier2}x"));
            _timeScaleGroup.AddButton(new ToggleButton(Rectangle.Empty, $"{Global.Instance.TimeScaleMultiplier3}x"));

            _timeScaleGroup.OnSelectionChanged += HandleTimeScaleChange;
            
            HandleTimeScaleChange(_timeScaleGroup.GetSelectedButton());
        }

        private static void HandleTimeScaleChange(ToggleButton selectedButton)
        {
            if (selectedButton == null) return;

            if (selectedButton.Text == $"{Global.Instance.TimeScaleMultiplier1}x")
            {
                Core.CurrentWorldClockManager.TimeScale = Global.Instance.TimeScaleMultiplier1;
            }
            else if (selectedButton.Text == $"{Global.Instance.TimeScaleMultiplier2}x")
            {
                Core.CurrentWorldClockManager.TimeScale = Global.Instance.TimeScaleMultiplier2;
            }
            else if (selectedButton.Text == $"{Global.Instance.TimeScaleMultiplier3}x")
            {
                Core.CurrentWorldClockManager.TimeScale = Global.Instance.TimeScaleMultiplier3;
            }
            else // Fallback
            {
                Core.CurrentWorldClockManager.TimeScale = 1.0f;
            }
        }

        public void Update(GameTime gameTime)
        {
            _timeScaleGroup.Update(Mouse.GetState());
        }

        public void DrawClock(SpriteBatch spriteBatch, GameTime gameTime)
        {
            var clockManager = Core.CurrentWorldClockManager;
            var pixel = Core.Pixel;
            var font = Global.Instance.DefaultFont;

            int statsBaseX = 50;
            int statsBaseY = 50 + Global.GRID_SIZE * Global.GRID_CELL_SIZE + 10;
            int statsHeight = 14 + 16 + Global.FONT_SIZE;
            _clockPosition = new Vector2(statsBaseX, statsBaseY + statsHeight + 10);

            Vector2 clockCenter = _clockPosition + new Vector2(CLOCK_SIZE / 2f, CLOCK_SIZE / 2f);

            // Draw hour marker dots
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            const int DOT_RADIUS = CLOCK_SIZE / 2 - 3;
            const int DOT_SIZE = 2;
            for (int i = 1; i <= 12; i++)
            {
                float angle = (i / 12f) * MathHelper.TwoPi - MathHelper.PiOver2;
                Vector2 dotPosition = new Vector2(clockCenter.X + DOT_RADIUS * (float)Math.Cos(angle), clockCenter.Y + DOT_RADIUS * (float)Math.Sin(angle));
                spriteBatch.Draw(pixel, new Rectangle((int)(dotPosition.X - DOT_SIZE / 2f), (int)(dotPosition.Y - DOT_SIZE / 2f), DOT_SIZE, DOT_SIZE), Global.Instance.Palette_BrightWhite);
            }

            // Get current time
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
            Vector2 periodPosition = new Vector2(clockCenter.X - periodSize.X / 2, _clockPosition.Y + CLOCK_SIZE * 0.7f - periodSize.Y / 2);
            spriteBatch.DrawString(font, period, periodPosition, Global.Instance.Palette_BrightWhite);

            // Define hand properties
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            Vector2 handOrigin = new Vector2(0, 0.5f);
            int hourHandLength = 16;
            int minuteHandLength = 24;
            int secondHandLength = 24;

            // Draw hands
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            // Hour hand
            spriteBatch.Draw(pixel, clockCenter, null, Global.Instance.Palette_BrightWhite, hourRotation, handOrigin, new Vector2(hourHandLength, 2), SpriteEffects.None, 0);
            spriteBatch.Draw(pixel, clockCenter, null, Global.Instance.Palette_BrightWhite, minuteRotation, handOrigin, new Vector2(minuteHandLength, 2), SpriteEffects.None, 0);
            spriteBatch.Draw(pixel, clockCenter, null, Global.Instance.Palette_Red, secondRotation, handOrigin, new Vector2(secondHandLength, 1), SpriteEffects.None, 0);

            // Set button positions and draw them
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            int buttonWidth = 30;
            int buttonHeight = 18;
            int buttonSpacing = 0;
            float totalGroupWidth = (buttonWidth * 3) + (buttonSpacing * 2);
            Vector2 groupStartPosition = new Vector2(clockCenter.X - (totalGroupWidth / 2), _clockPosition.Y + CLOCK_SIZE + 5);

            var buttons = _timeScaleGroup.Buttons;
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].Bounds = new Rectangle(
                    (int)groupStartPosition.X + i * (buttonWidth + buttonSpacing),
                    (int)groupStartPosition.Y,
                    buttonWidth,
                    buttonHeight
                );
            }
            _timeScaleGroup.Draw(spriteBatch, font, gameTime);
        }
    }
}
