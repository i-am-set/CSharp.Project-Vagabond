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
        private Slider _hourSlider;
        private Slider _minuteSlider;
        private Slider _secondSlider;

        private Button _confirmButton;
        private Button _cancelButton;

        private Action<int, int, int> _onConfirm;

        public WaitDialog(GameScene currentGameScene) : base(currentGameScene)
        {
        }

        public void Show(Action<int, int, int> onConfirm)
        {
            _currentGameScene?.ResetInputBlockTimer();
            _onConfirm = onConfirm;

            IsActive = true;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            Core.Instance.IsMouseVisible = true;

            int dialogWidth = 400;
            int dialogHeight = 280;

            _dialogBounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - dialogWidth) / 2,
                (Global.VIRTUAL_HEIGHT - dialogHeight) / 2,
                dialogWidth,
                dialogHeight
            );

            int sliderWidth = dialogWidth - 80;
            int sliderX = _dialogBounds.X + 40;
            int sliderHeight = 20;

            _hourSlider = new Slider(new Rectangle(sliderX, _dialogBounds.Y + 70, sliderWidth, sliderHeight), "Hours", 0, 24, 0, 1);
            _minuteSlider = new Slider(new Rectangle(sliderX, _dialogBounds.Y + 130, sliderWidth, sliderHeight), "Minutes", 0, 59, 0, 1);
            _secondSlider = new Slider(new Rectangle(sliderX, _dialogBounds.Y + 190, sliderWidth, sliderHeight), "Seconds", 0, 59, 0, 1);

            int buttonWidth = 100;
            int buttonHeight = 25;
            int buttonY = _dialogBounds.Bottom - buttonHeight - 20;
            int buttonGap = 20;

            var (cancelText, cancelColor) = ParseButtonTextAndColor("[red]Cancel");
            _cancelButton = new Button(new Rectangle(_dialogBounds.Center.X - buttonWidth - buttonGap / 2, buttonY, buttonWidth, buttonHeight), cancelText)
            {
                CustomDefaultTextColor = cancelColor
            };
            _cancelButton.OnClick += Hide;

            var (confirmText, confirmColor) = ParseButtonTextAndColor("[green]Confirm");
            _confirmButton = new Button(new Rectangle(_dialogBounds.Center.X + buttonGap / 2, buttonY, buttonWidth, buttonHeight), confirmText)
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

            string title = "Wait for how long?";
            Vector2 titleSize = font.MeasureString(title);
            spriteBatch.DrawString(font, title, new Vector2(_dialogBounds.Center.X - titleSize.X / 2, _dialogBounds.Y + 20), Global.Instance.Palette_BrightWhite);

            _hourSlider.Draw(spriteBatch, font);
            _minuteSlider.Draw(spriteBatch, font);
            _secondSlider.Draw(spriteBatch, font);

            int totalSeconds = (int)_hourSlider.CurrentValue * 3600 + (int)_minuteSlider.CurrentValue * 60 + (int)_secondSlider.CurrentValue;
            string timeString = Core.CurrentWorldClockManager.GetCommaFormattedTimeFromSeconds(totalSeconds);
            Vector2 timeStringSize = font.MeasureString(timeString);
            spriteBatch.DrawString(font, timeString, new Vector2(_dialogBounds.Center.X - timeStringSize.X / 2, _dialogBounds.Bottom - 75), Global.Instance.Palette_Yellow);

            _confirmButton.Draw(spriteBatch, font, gameTime);
            _cancelButton.Draw(spriteBatch, font, gameTime);

            spriteBatch.End();
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