using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes;
using ProjectVagabond.Utils;
using System;
using System.Text;

namespace ProjectVagabond.UI
{
    public class RevertDialog : Dialog
    {
        private string _prompt;
        private Button _confirmButton;
        private Button _revertButton;
        private Action _onConfirm;
        private Action _onRevert;

        private float _countdownTimer;
        private readonly StringBuilder _stringBuilder = new StringBuilder();

        public RevertDialog(GameScene currentGameScene) : base(currentGameScene) { }

        public void Show(string prompt, Action onConfirm, Action onRevert, float countdownDuration)
        {
            _currentGameScene?.ResetInputBlockTimer();
            IsActive = true;

            _prompt = prompt;
            _onConfirm = onConfirm;
            _onRevert = onRevert;
            _countdownTimer = countdownDuration;

            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _core.IsMouseVisible = true;

            // Layout
            int dialogWidth = 450;
            int dialogHeight = 150;
            _dialogBounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - dialogWidth) / 2,
                (Global.VIRTUAL_HEIGHT - dialogHeight) / 2,
                dialogWidth,
                dialogHeight
            );

            int buttonWidth = 120;
            int buttonHeight = 25;
            int buttonY = _dialogBounds.Bottom - buttonHeight - 20;
            int buttonGap = 20;

            _confirmButton = new Button(new Rectangle(_dialogBounds.Center.X - buttonWidth - buttonGap / 2, buttonY, buttonWidth, buttonHeight), "Confirm");
            _confirmButton.OnClick += () => {
                _onConfirm?.Invoke();
                Hide();
            };

            _revertButton = new Button(new Rectangle(_dialogBounds.Center.X + buttonGap / 2, buttonY, buttonWidth, buttonHeight), "Revert");
            _revertButton.OnClick += () => {
                _onRevert?.Invoke();
                Hide();
            };
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            _countdownTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_countdownTimer <= 0)
            {
                _onRevert?.Invoke();
                Hide();
                return;
            }

            _confirmButton.Update(currentMouseState);
            _revertButton.Update(currentMouseState);

            if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
            {
                _confirmButton.TriggerClick();
            }
            if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
            {
                _revertButton.TriggerClick();
            }

            _previousMouseState = currentMouseState;
            _previousKeyboardState = currentKeyboardState;
        }

        public override void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (!IsActive) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.DrawSnapped(pixel, _dialogBounds, _global.Palette_DarkGray);
            DrawRectangleBorder(spriteBatch, pixel, _dialogBounds, 1, _global.Palette_LightGray);

            // Draw Prompt
            Vector2 promptSize = font.MeasureString(_prompt);
            Vector2 promptPosition = new Vector2(_dialogBounds.Center.X - promptSize.X / 2, _dialogBounds.Y + 20);
            spriteBatch.DrawStringSnapped(font, _prompt, promptPosition, _global.Palette_BrightWhite);

            // Draw Countdown Timer
            _stringBuilder.Clear();
            _stringBuilder.Append("Reverting in ").Append((int)Math.Ceiling(_countdownTimer)).Append(" seconds...");
            string timerString = _stringBuilder.ToString();
            Vector2 timerSize = font.MeasureString(timerString);
            Vector2 timerPosition = new Vector2(_dialogBounds.Center.X - timerSize.X / 2, promptPosition.Y + promptSize.Y + 15);
            spriteBatch.DrawStringSnapped(font, timerString, timerPosition, _global.Palette_Yellow);

            // Draw Buttons
            _confirmButton.Draw(spriteBatch, font, gameTime);
            _revertButton.Draw(spriteBatch, font, gameTime);
        }
    }
}