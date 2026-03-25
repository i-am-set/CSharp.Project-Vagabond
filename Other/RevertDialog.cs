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

            _previousMouseState = Mouse.GetState();
            _core.IsMouseVisible = true;

            int dialogWidth = 280;
            int dialogHeight = 100;
            _dialogBounds = new Rectangle(
                (Global.VIRTUAL_WIDTH - dialogWidth) / 2,
                (Global.VIRTUAL_HEIGHT - dialogHeight) / 2,
                dialogWidth,
                dialogHeight
            );

            var font = ServiceLocator.Get<BitmapFont>();
            int confirmW = (int)font.MeasureString("Confirm").Width + 8;
            int revertW = (int)font.MeasureString("Revert").Width + 8;
            int buttonHeight = 12;
            int buttonY = _dialogBounds.Bottom - buttonHeight - 10;
            int buttonGap = 10;

            _confirmButton = new Button(new Rectangle(_dialogBounds.Center.X - confirmW - buttonGap / 2, buttonY, confirmW, buttonHeight), "Confirm")
            {
                TextRenderOffset = new Vector2(0, 1)
            };
            _confirmButton.OnClick += () => {
                _onConfirm?.Invoke();
                Hide();
            };

            _revertButton = new Button(new Rectangle(_dialogBounds.Center.X + buttonGap / 2, buttonY, revertW, buttonHeight), "Revert")
            {
                TextRenderOffset = new Vector2(0, 1)
            };
            _revertButton.OnClick += () => {
                _onRevert?.Invoke();
                Hide();
            };
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            var inputManager = ServiceLocator.Get<InputManager>();
            var currentMouseState = inputManager.GetEffectiveMouseState();

            _countdownTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_countdownTimer <= 0)
            {
                _onRevert?.Invoke();
                Hide();
                return;
            }

            _confirmButton.Update(currentMouseState);
            _revertButton.Update(currentMouseState);

            if (inputManager.Confirm)
            {
                _confirmButton.TriggerClick();
            }
            if (inputManager.Back)
            {
                _revertButton.TriggerClick();
            }

            _previousMouseState = currentMouseState;
        }

        public override void DrawContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            if (!IsActive) return;

            var pixel = ServiceLocator.Get<Texture2D>();
            spriteBatch.DrawSnapped(pixel, _dialogBounds, _global.Palette_DarkShadow);
            DrawRectangleBorder(spriteBatch, pixel, _dialogBounds, 1, _global.Palette_Shadow);

            Vector2 promptSize = font.MeasureString(_prompt);
            Vector2 promptPosition = new Vector2(MathF.Round(_dialogBounds.Center.X - promptSize.X / 2f), MathF.Round(_dialogBounds.Y + 10));
            spriteBatch.DrawStringSnapped(font, _prompt, promptPosition, _global.Palette_Sun);

            _stringBuilder.Clear();
            _stringBuilder.Append("Reverting in ").Append((int)Math.Ceiling(_countdownTimer)).Append(" seconds...");
            string timerString = _stringBuilder.ToString();
            Vector2 timerSize = font.MeasureString(timerString);
            Vector2 timerPosition = new Vector2(MathF.Round(_dialogBounds.Center.X - timerSize.X / 2f), MathF.Round(promptPosition.Y + promptSize.Y + 8));
            spriteBatch.DrawStringSnapped(font, timerString, timerPosition, _global.Palette_DarkSun);

            _confirmButton.Draw(spriteBatch, font, gameTime, transform);
            _revertButton.Draw(spriteBatch, font, gameTime, transform);
        }
    }
}