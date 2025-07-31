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
        private readonly GameState _gameState;
        private readonly WorldClockManager _worldClockManager;
        private readonly TooltipManager _tooltipManager;
        private readonly Global _global;
        private MapRenderer _mapRenderer;

        private Vector2 _clockPosition;
        private const int CLOCK_SIZE = 64;
        private readonly RadioGroup _timeScaleGroup;
        public RadioGroup TimeScaleGroup => _timeScaleGroup;
        private readonly ToggleButton _pausePlayButton;
        private readonly ImageButton _clockButton;

        public event Action OnClockClicked;

        public ClockRenderer()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _global = ServiceLocator.Get<Global>();

            _timeScaleGroup = new RadioGroup(defaultIndex: 0);

            _timeScaleGroup.AddButton(new ToggleButton(Rectangle.Empty, $"{_global.TimeScaleMultiplier1}x", customDefaultTextColor: _global.Palette_Gray, customToggledTextColor: _global.Palette_BrightWhite));
            _timeScaleGroup.AddButton(new ToggleButton(Rectangle.Empty, $"{_global.TimeScaleMultiplier2}x", customDefaultTextColor: _global.Palette_Gray, customToggledTextColor: _global.Palette_BrightWhite));
            _timeScaleGroup.AddButton(new ToggleButton(Rectangle.Empty, $"{_global.TimeScaleMultiplier3}x", customDefaultTextColor: _global.Palette_Gray, customToggledTextColor: _global.Palette_BrightWhite));

            _timeScaleGroup.OnSelectionChanged += HandleTimeScaleChange;

            _pausePlayButton = new ToggleButton(Rectangle.Empty, "||");
            _pausePlayButton.OnClick += () => _gameState.TogglePause();

            _clockButton = new ImageButton(Rectangle.Empty);
            _clockButton.OnClick += () =>
            {
                if (_gameState.IsExecutingActions)
                {
                    _gameState.TogglePause();
                }
                else
                {
                    OnClockClicked?.Invoke();
                }
            };

            HandleTimeScaleChange(_timeScaleGroup.GetSelectedButton());
        }

        private void HandleTimeScaleChange(ToggleButton selectedButton)
        {
            if (selectedButton == null) return;

            float newTimeScale = 1.0f;
            if (selectedButton.Text == $"{_global.TimeScaleMultiplier1}x")
            {
                newTimeScale = _global.TimeScaleMultiplier1;
            }
            else if (selectedButton.Text == $"{_global.TimeScaleMultiplier2}x")
            {
                newTimeScale = _global.TimeScaleMultiplier2;
            }
            else if (selectedButton.Text == $"{_global.TimeScaleMultiplier3}x")
            {
                newTimeScale = _global.TimeScaleMultiplier3;
            }

            _worldClockManager.UpdateTimeScale(newTimeScale);
        }

        public void Update(GameTime gameTime)
        {
            var currentMouseState = Mouse.GetState();

            _clockButton.Update(currentMouseState);

            if (_clockButton.IsHovered)
            {
                Vector2 virtualMousePos = Core.TransformMouse(currentMouseState.Position);
                string tooltipText;
                if (_gameState.IsExecutingActions)
                {
                    tooltipText = _gameState.IsPaused ? "Click to resume" : "Click to pause";
                }
                else
                {
                    tooltipText = "Click to wait";
                }
                _tooltipManager.RequestTooltip(_clockButton, tooltipText.ToUpper(), virtualMousePos, Global.TOOLTIP_AVERAGE_POPUP_TIME);
            }

            _timeScaleGroup.Update(currentMouseState);
            _pausePlayButton.Update(currentMouseState);
        }

        public void DrawClock(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Vector2 position)
        {
            _mapRenderer ??= ServiceLocator.Get<MapRenderer>();
            var pixel = ServiceLocator.Get<Texture2D>();

            _clockPosition = position;

            _clockButton.Bounds = new Rectangle((int)_clockPosition.X, (int)_clockPosition.Y, CLOCK_SIZE, CLOCK_SIZE);

            Vector2 clockCenter = _clockPosition + new Vector2(CLOCK_SIZE / 2f, CLOCK_SIZE / 2f);

            // Draw hour marker dots
            const int DOT_RADIUS = CLOCK_SIZE / 2 - 3;
            const int DOT_SIZE = 2;
            for (int i = 1; i <= 12; i++)
            {
                float angle = (i / 12f) * MathHelper.TwoPi - MathHelper.PiOver2;
                Vector2 dotPosition = new Vector2(clockCenter.X + DOT_RADIUS * (float)Math.Cos(angle), clockCenter.Y + DOT_RADIUS * (float)Math.Sin(angle));
                spriteBatch.Draw(pixel, new Rectangle((int)(dotPosition.X - DOT_SIZE / 2f), (int)(dotPosition.Y - DOT_SIZE / 2f), DOT_SIZE, DOT_SIZE), _global.Palette_BrightWhite);
            }

            // Get current time with high precision
            var currentTime = _worldClockManager.CurrentTimeSpan;
            double totalHours = currentTime.TotalHours;
            double totalMinutes = currentTime.TotalMinutes;
            double totalSeconds = currentTime.TotalSeconds;

            // Calculate hand rotations in radians using high-precision total time values.
            float secondRotation = (float)(totalSeconds / 60.0 * MathHelper.TwoPi - MathHelper.PiOver2);
            float minuteRotation = (float)(totalMinutes / 60.0 * MathHelper.TwoPi - MathHelper.PiOver2);
            float hourRotation = (float)(totalHours / 12.0 * MathHelper.TwoPi - MathHelper.PiOver2);

            // Draw AM/PM text (still uses integer hour for simplicity)
            string period = _worldClockManager.CurrentHour >= 12 ? "PM" : "AM";
            Vector2 periodSize = font.MeasureString(period);
            Vector2 periodPosition = new Vector2(clockCenter.X - periodSize.X / 2, _clockPosition.Y + CLOCK_SIZE * 0.7f - periodSize.Y / 2);
            spriteBatch.DrawString(font, period, periodPosition, _global.Palette_BrightWhite);

            // Define hand properties
            Vector2 handOrigin = new Vector2(0, 0.5f);
            int hourHandLength = 16;
            int minuteHandLength = 24;
            int secondHandLength = 24;

            // Draw hands
            // Hour hand
            spriteBatch.Draw(pixel, clockCenter, null, _global.Palette_BrightWhite, hourRotation, handOrigin, new Vector2(hourHandLength, 2), SpriteEffects.None, 0);
            // Minute hand
            spriteBatch.Draw(pixel, clockCenter, null, _global.Palette_BrightWhite, minuteRotation, handOrigin, new Vector2(minuteHandLength, 2), SpriteEffects.None, 0);
            // Second hand
            spriteBatch.Draw(pixel, clockCenter, null, _global.Palette_Red, secondRotation, handOrigin, new Vector2(secondHandLength, 1), SpriteEffects.None, 0);

            _clockButton.Draw(spriteBatch, font, gameTime);

            // Set button positions and draw them
            int buttonWidth = 30;
            int buttonHeight = 18;
            int buttonSpacing = 2;

            // Pause/Play Button
            _pausePlayButton.IsEnabled = _gameState.IsExecutingActions;
            _pausePlayButton.Text = _gameState.IsPaused ? "►" : "▐▐";

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