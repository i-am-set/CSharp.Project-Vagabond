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
        private readonly SpriteManager _spriteManager;
        private MapRenderer _mapRenderer;

        private Vector2 _clockPosition;
        private const float CLOCK_SCALE = 0.75f;
        private const int BASE_CLOCK_SIZE = 64;
        private readonly int _clockSize;
        public int ClockSize => _clockSize;

        private readonly ToggleButton _pausePlayButton;
        private readonly ImageButton _clockButton;

        public event Action OnClockClicked;

        public ClockRenderer()
        {
            _gameState = ServiceLocator.Get<GameState>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _tooltipManager = ServiceLocator.Get<TooltipManager>();
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();

            _clockSize = (int)(BASE_CLOCK_SIZE * CLOCK_SCALE);

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

            _pausePlayButton.Update(currentMouseState);
        }

        public void DrawClock(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Vector2 position)
        {
            _mapRenderer ??= ServiceLocator.Get<MapRenderer>();
            var pixel = ServiceLocator.Get<Texture2D>();

            _clockPosition = position;

            _clockButton.Bounds = new Rectangle((int)_clockPosition.X, (int)_clockPosition.Y, _clockSize, _clockSize);

            // Draw the white background circle
            var backgroundCircleRect = new Rectangle((int)_clockPosition.X, (int)_clockPosition.Y, _clockSize, _clockSize);
            spriteBatch.Draw(_spriteManager.CircleTextureSprite, backgroundCircleRect, _global.Palette_BrightWhite);

            Vector2 clockCenter = _clockPosition + new Vector2(_clockSize / 2f, _clockSize / 2f);

            // Draw hour marker dots
            int dotRadius = _clockSize / 2 - (int)Math.Max(1, 3 * CLOCK_SCALE);
            int dotSize = (int)Math.Max(1, 2 * CLOCK_SCALE);
            for (int i = 1; i <= 12; i++)
            {
                float angle = (i / 12f) * MathHelper.TwoPi - MathHelper.PiOver2;
                Vector2 dotPosition = new Vector2(clockCenter.X + dotRadius * (float)Math.Cos(angle), clockCenter.Y + dotRadius * (float)Math.Sin(angle));
                spriteBatch.Draw(pixel, new Rectangle((int)(dotPosition.X - dotSize / 2f), (int)(dotPosition.Y - dotSize / 2f), dotSize, dotSize), _global.Palette_Black);
            }

            // Get current time with high precision from the VISUAL time span
            var currentTime = _worldClockManager.VisualTimeSpan;
            double totalHours = currentTime.TotalHours;
            double totalMinutes = currentTime.TotalMinutes;
            double totalSeconds = currentTime.TotalSeconds;

            // Calculate hand rotations in radians using high-precision total time values.
            float secondRotation = (float)(totalSeconds / 60.0 * MathHelper.TwoPi - MathHelper.PiOver2);
            float minuteRotation = (float)(totalMinutes / 60.0 * MathHelper.TwoPi - MathHelper.PiOver2);
            float hourRotation = (float)(totalHours / 12.0 * MathHelper.TwoPi - MathHelper.PiOver2);

            // Draw AM/PM text (uses logical hour for accuracy)
            string period = _worldClockManager.CurrentHour >= 12 ? "PM" : "AM";
            Vector2 periodSize = font.MeasureString(period);
            Vector2 periodPosition = new Vector2(clockCenter.X - periodSize.X / 2, _clockPosition.Y + _clockSize * 0.7f - periodSize.Y / 2);
            spriteBatch.DrawString(font, period, periodPosition, _global.Palette_Black);

            // Define hand properties
            Vector2 handOrigin = new Vector2(0, 0.5f);
            int hourHandLength = (int)(16 * CLOCK_SCALE);
            int minuteHandLength = (int)(24 * CLOCK_SCALE);
            int secondHandLength = (int)(24 * CLOCK_SCALE);

            // Draw hands
            // Hour hand
            spriteBatch.Draw(pixel, clockCenter, null, _global.Palette_Black, hourRotation, handOrigin, new Vector2(hourHandLength, 1), SpriteEffects.None, 0);
            // Minute hand
            spriteBatch.Draw(pixel, clockCenter, null, _global.Palette_Black, minuteRotation, handOrigin, new Vector2(minuteHandLength, 1), SpriteEffects.None, 0);
            // Second hand
            spriteBatch.Draw(pixel, clockCenter, null, _global.Palette_Red, secondRotation, handOrigin, new Vector2(secondHandLength, 1), SpriteEffects.None, 0);

            _clockButton.Draw(spriteBatch, font, gameTime);

            // Set button positions and draw them
            int buttonWidth = (int)(30 * CLOCK_SCALE);
            int buttonHeight = (int)(18 * CLOCK_SCALE);

            // Pause/Play Button
            _pausePlayButton.IsEnabled = _gameState.IsExecutingActions;
            _pausePlayButton.Text = _gameState.IsPaused ? "►" : "▐▐";

            // Draw Pause/Play button if active
            if (_pausePlayButton.IsEnabled)
            {
                Vector2 buttonPosition = new Vector2(clockCenter.X - (buttonWidth / 2f), _clockPosition.Y + _clockSize + (int)Math.Max(2, 5 * CLOCK_SCALE));
                _pausePlayButton.Bounds = new Rectangle((int)buttonPosition.X, (int)buttonPosition.Y, buttonWidth, buttonHeight);
                _pausePlayButton.Draw(spriteBatch, font, gameTime);
            }
        }
    }
}