using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
using System;

namespace ProjectVagabond.UI
{
    public class WaitDialog : Dialog
    {
        // Overall Dialog Dimensions
        private const int DialogWidth = 400;
        private const int DialogHeight = 240;

        // Padding and Margins
        private const int DialogHorizontalPadding = 20;
        private const int TitleTopMargin = 10;
        private const int ButtonBottomMargin = 10;
        private const int TimeStringBottomMargin = 50;

        // Sliders
        private const int FirstSliderTopMargin = 40;
        private const int SliderHeight = 20;
        private const int SliderVerticalSpacing = 50;

        // Tick Marks
        private const int MinorTickMarkHeight = 6;
        private const int MajorTickMarkHeight = 12;
        private const int TickMarkWidth = 1;
        private const int HourMajorTickInterval = 6;
        private const int MinuteSecondMajorTickInterval = 5;

        // Buttons
        private const int ButtonWidth = 100;
        private const int ButtonHeight = 25;
        private const int ButtonGap = 20;

        private Slider _hourSlider;
        private Slider _minuteSlider;
        private Slider _secondSlider;

        private Button _confirmButton;
        private Button _cancelButton;

        private Action<int, int, int> _onConfirm;

        public WaitDialog(GameScene currentGameScene) : base(currentGameScene) {}

        public void Show(Action<int, int, int> onConfirm)
        {
            _currentGameScene?.ResetInputBlockTimer();
            _onConfirm = onConfirm;

            IsActive = true;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            Core.Instance.IsMouseVisible = true;

            // Dialog Bounds 
            _dialogBounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - DialogWidth) / 2,
                (Global.VIRTUAL_HEIGHT - DialogHeight) / 2,
                DialogWidth,
                DialogHeight
            );

            // Slider Layout 
            int sliderWidth = DialogWidth - (DialogHorizontalPadding * 2);
            int sliderX = _dialogBounds.X + DialogHorizontalPadding;
            int sliderStartY = _dialogBounds.Y + FirstSliderTopMargin;

            _hourSlider = new Slider(new Rectangle(sliderX, sliderStartY, sliderWidth, SliderHeight), "Hours", 0, 24, 0, 1);
            _minuteSlider = new Slider(new Rectangle(sliderX, sliderStartY + SliderVerticalSpacing, sliderWidth, SliderHeight), "Minutes", 0, 59, 0, 1);
            _secondSlider = new Slider(new Rectangle(sliderX, sliderStartY + (SliderVerticalSpacing * 2), sliderWidth, SliderHeight), "Seconds", 0, 59, 0, 1);

            // Button Layout 
            int buttonY = _dialogBounds.Bottom - ButtonHeight - ButtonBottomMargin;
            int buttonCenterX = _dialogBounds.Center.X;
            int halfButtonGap = ButtonGap / 2;

            var (cancelText, cancelColor) = ParseButtonTextAndColor("[gray]Cancel");
            _cancelButton = new Button(new Rectangle(buttonCenterX - ButtonWidth - halfButtonGap, buttonY, ButtonWidth, ButtonHeight), cancelText)
            {
                CustomDefaultTextColor = cancelColor
            };
            _cancelButton.OnClick += Hide;

            var (confirmText, confirmColor) = ParseButtonTextAndColor("Confirm");
            _confirmButton = new Button(new Rectangle(buttonCenterX + halfButtonGap, buttonY, ButtonWidth, ButtonHeight), confirmText, customDisabledTextColor: Global.Instance.Palette_Gray)
            {
                CustomDefaultTextColor = confirmColor
            };
            _confirmButton.OnClick += () =>
            {
                _onConfirm?.Invoke((int)_hourSlider.CurrentValue, (int)_minuteSlider.CurrentValue, (int)_secondSlider.CurrentValue);
                Hide();
            };
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            _hourSlider.Update(currentMouseState, _previousMouseState);
            _minuteSlider.Update(currentMouseState, _previousMouseState);
            _secondSlider.Update(currentMouseState, _previousMouseState);

            _confirmButton.IsEnabled = true ? ((int)_hourSlider.CurrentValue > 0 || (int)_minuteSlider.CurrentValue > 0 || (int)_secondSlider.CurrentValue > 0) : false;
            _confirmButton.Update(currentMouseState);
            _cancelButton.Update(currentMouseState);

            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                Hide();
            }

            if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
            {
                _confirmButton.TriggerClick();
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        protected override void DrawContent(GameTime gameTime)
        {
            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            var font = Global.Instance.DefaultFont;
            var pixel = Core.Pixel;

            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            spriteBatch.Draw(pixel, _dialogBounds, Global.Instance.Palette_DarkGray);
            DrawRectangleBorder(spriteBatch, pixel, _dialogBounds, 1, Global.Instance.Palette_LightGray);

            string title = "How long would you like to wait?";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePosition = new Vector2(_dialogBounds.Center.X - titleSize.X / 2, _dialogBounds.Y + TitleTopMargin);
            spriteBatch.DrawString(font, title, titlePosition, Global.Instance.Palette_BrightWhite);

            // Draw sliders and their tick marks
            DrawSliderTickMarks(spriteBatch, pixel, _hourSlider, HourMajorTickInterval);
            _hourSlider.Draw(spriteBatch, font);

            DrawSliderTickMarks(spriteBatch, pixel, _minuteSlider, MinuteSecondMajorTickInterval);
            _minuteSlider.Draw(spriteBatch, font);

            DrawSliderTickMarks(spriteBatch, pixel, _secondSlider, MinuteSecondMajorTickInterval);
            _secondSlider.Draw(spriteBatch, font);

            // Draw total time string
            int totalSeconds = (int)_hourSlider.CurrentValue * 3600 + (int)_minuteSlider.CurrentValue * 60 + (int)_secondSlider.CurrentValue;
            string timeString = Core.CurrentWorldClockManager.GetCommaFormattedTimeFromSeconds(totalSeconds);
            Vector2 timeStringSize = font.MeasureString(timeString);
            Vector2 timeStringPosition = new Vector2(_dialogBounds.Center.X - timeStringSize.X / 2, _dialogBounds.Bottom - TimeStringBottomMargin);
            if (totalSeconds > 0) spriteBatch.DrawString(font, timeString, timeStringPosition, Global.Instance.Palette_Yellow);

            _confirmButton.Draw(spriteBatch, font, gameTime);
            _cancelButton.Draw(spriteBatch, font, gameTime);

            spriteBatch.End();
        }

        private void DrawSliderTickMarks(SpriteBatch spriteBatch, Texture2D pixel, Slider slider, int majorTickInterval)
        {
            float valueRange = slider.MaxValue - slider.MinValue;
            if (valueRange <= 0) return;

            float pixelsPerUnit = (float)(slider.Bounds.Width - 1) / valueRange;

            int tickStartY = slider.Bounds.Bottom - 10;
            Color tickColor = Global.Instance.Palette_Gray;

            for (int i = 0; i <= valueRange; i++)
            {
                int currentValue = (int)slider.MinValue + i;

                bool isMajorTick = (currentValue % majorTickInterval == 0);
                int tickHeight = isMajorTick ? MajorTickMarkHeight : MinorTickMarkHeight;

                int tickX = slider.Bounds.X + (int)Math.Round(i * pixelsPerUnit);

                spriteBatch.Draw(pixel, new Rectangle(tickX, tickStartY, TickMarkWidth, tickHeight), tickColor);
            }
        }

        private (string text, Color? color) ParseButtonTextAndColor(string taggedText)
        {
            if (taggedText.StartsWith("[") && taggedText.Contains("]"))
            {
                int closingBracketIndex = taggedText.IndexOf(']');
                if (closingBracketIndex == -1) return (taggedText, null);

                string colorName = taggedText.Substring(1, closingBracketIndex - 1).ToLowerInvariant();
                string text = taggedText.Substring(closingBracketIndex + 1);

                Color color;
                switch (colorName)
                {
                    case "red": color = Global.Instance.Palette_Red; break;
                    case "green": color = Global.Instance.Palette_LightGreen; break;
                    case "yellow": color = Global.Instance.Palette_Yellow; break;
                    case "gray": color = Global.Instance.Palette_LightGray; break;
                    case "grey": color = Global.Instance.Palette_LightGray; break;
                    default:
                        return (taggedText, null);
                }
                return (text, color);
            }
            return (taggedText, null);
        }
    }
}