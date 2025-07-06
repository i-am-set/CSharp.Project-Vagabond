using Microsoft.Xna.Framework;
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
        private readonly ToggleButton _pausePlayButton;
        private readonly ImageButton _clockButton;

        public event Action OnClockClicked;

        public ClockRenderer()
        {
            _timeScaleGroup = new RadioGroup(defaultIndex: 0);

            _timeScaleGroup.AddButton(new ToggleButton(Rectangle.Empty, $"{Global.Instance.TimeScaleMultiplier1}x", customDefaultTextColor: Global.Instance.Palette_Gray, customToggledTextColor: Global.Instance.Palette_BrightWhite));
            _timeScaleGroup.AddButton(new ToggleButton(Rectangle.Empty, $"{Global.Instance.TimeScaleMultiplier2}x", customDefaultTextColor: Global.Instance.Palette_Gray, customToggledTextColor: Global.Instance.Palette_BrightWhite));
            _timeScaleGroup.AddButton(new ToggleButton(Rectangle.Empty, $"{Global.Instance.TimeScaleMultiplier3}x", customDefaultTextColor: Global.Instance.Palette_Gray, customToggledTextColor: Global.Instance.Palette_BrightWhite));

            _timeScaleGroup.OnSelectionChanged += HandleTimeScaleChange;

            _pausePlayButton = new ToggleButton(Rectangle.Empty, "||");
            _pausePlayButton.OnClick += () => Core.CurrentGameState.TogglePause();

            _clockButton = new ImageButton(Rectangle.Empty);
            _clockButton.OnClick += () =>
            {
                if (Core.CurrentGameState.IsExecutingActions)
                {
                    Core.CurrentGameState.TogglePause();
                }
                else
                {
                    OnClockClicked?.Invoke();
                }
            };

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
            var currentMouseState = Mouse.GetState();

            _clockButton.Update(currentMouseState);

            if (_clockButton.IsHovered)
            {
                Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);
                string tooltipText;
                if (Core.CurrentGameState.IsExecutingActions)
                {
                    tooltipText = Core.CurrentGameState.IsPaused ? "Click to resume" : "Click to pause";
                }
                else
                {
                    tooltipText = "Click to wait";
                }
                Core.CurrentTooltipManager.RequestTooltip(_clockButton, tooltipText.ToUpper(), virtualMousePos, Global.TOOLTIP_AVERAGE_POPUP_TIME);
            }

            _timeScaleGroup.Update(currentMouseState);
            _pausePlayButton.Update(currentMouseState);
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

            _clockButton.Bounds = new Rectangle((int)_clockPosition.X, (int)_clockPosition.Y, CLOCK_SIZE, CLOCK_SIZE);

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

            // Get current time with high precision
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            var currentTime = clockManager.CurrentTimeSpan;
            double totalHours = currentTime.TotalHours;
            double totalMinutes = currentTime.TotalMinutes;
            double totalSeconds = currentTime.TotalSeconds;

            // Calculate hand rotations in radians using high-precision total time values.
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            float secondRotation = (float)(totalSeconds / 60.0 * MathHelper.TwoPi - MathHelper.PiOver2);
            float minuteRotation = (float)(totalMinutes / 60.0 * MathHelper.TwoPi - MathHelper.PiOver2);
            float hourRotation = (float)(totalHours / 12.0 * MathHelper.TwoPi - MathHelper.PiOver2);

            // Draw AM/PM text (still uses integer hour for simplicity)
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            string period = clockManager.CurrentHour >= 12 ? "PM" : "AM";
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
            // Minute hand
            spriteBatch.Draw(pixel, clockCenter, null, Global.Instance.Palette_BrightWhite, minuteRotation, handOrigin, new Vector2(minuteHandLength, 2), SpriteEffects.None, 0);
            // Second hand
            spriteBatch.Draw(pixel, clockCenter, null, Global.Instance.Palette_Red, secondRotation, handOrigin, new Vector2(secondHandLength, 1), SpriteEffects.None, 0);

            _clockButton.Draw(spriteBatch, font, gameTime);

            // Set button positions and draw them
            // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
            int buttonWidth = 30;
            int buttonHeight = 18;
            int buttonSpacing = 2;

            // Pause/Play Button
            _pausePlayButton.IsEnabled = Core.CurrentGameState.IsExecutingActions;
            _pausePlayButton.Text = Core.CurrentGameState.IsPaused ? "►" : "▐▐";

            // Time Scale Buttons
            var timeButtons = _timeScaleGroup.Buttons;
            float totalGroupWidth = (buttonWidth * timeButtons.Count) + (buttonSpacing * (timeButtons.Count - 1));

            // Add pause button width if it's visible
            if (_pausePlayButton.IsEnabled)
            {
                totalGroupWidth += buttonWidth + buttonSpacing;
            }

            Vector2 groupStartPosition = new Vector2(clockCenter.X - (totalGroupWidth / 2), _clockPosition.Y + CLOCK_SIZE + 5);
            float currentX = groupStartPosition.X;

            // Draw Pause/Play button if active
            if (_pausePlayButton.IsEnabled)
            {
                _pausePlayButton.Bounds = new Rectangle((int)currentX, (int)groupStartPosition.Y, buttonWidth, buttonHeight);
                _pausePlayButton.Draw(spriteBatch, font, gameTime);
                currentX += buttonWidth + buttonSpacing;
            }

            // Draw Time Scale buttons
            for (int i = 0; i < timeButtons.Count; i++)
            {
                timeButtons[i].Bounds = new Rectangle((int)currentX, (int)groupStartPosition.Y, buttonWidth, buttonHeight);
                timeButtons[i].Draw(spriteBatch, font, gameTime);
                currentX += buttonWidth + buttonSpacing;
            }
        }
    }
}