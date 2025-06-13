using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectVagabond.UI;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVagabond.Scenes
{
    public class SettingsScene : GameScene
    {
        private readonly List<ISettingControl> _settings = new();
        private readonly List<Button> _buttons = new();
        private int _selectedControlIndex = 0;
        private string _confirmationMessage = "";
        private float _confirmationTimer = 0f;

        private KeyboardState _previousKeyboardState;

        public override void Enter()
        {
            // Re-initialize settings every time we enter to get current values
            _settings.Clear();
            _buttons.Clear();

            _settings.Add(new BoolSettingControl(
                "Use 24-Hour Clock",
                () => Global.Instance.Use24HourClock,
                (value) => Global.Instance.Use24HourClock = value
            ));
            _settings.Add(new BoolSettingControl(
                "Use Imperial Units",
                () => Global.Instance.UseImperialUnits,
                (value) => Global.Instance.UseImperialUnits = value
            ));

            int screenWidth = Global.Instance.CurrentGraphics.PreferredBackBufferWidth;
            int buttonY = 200 + _settings.Count * 40;
            var applyButton = new Button(new Rectangle(screenWidth / 2 - 210, buttonY, 200, 50), "Apply");
            applyButton.OnClick += ApplySettings;
            _buttons.Add(applyButton);

            var backButton = new Button(new Rectangle(screenWidth / 2 + 10, buttonY, 200, 50), "Back");
            backButton.OnClick += BackToPrevious;
            _buttons.Add(backButton);

            _selectedControlIndex = 0;
            CheckIfDirty();
        }

        private void ApplySettings()
        {
            if (!_buttons[0].IsEnabled) return;

            foreach (var setting in _settings)
            {
                setting.Apply();
            }
            _confirmationMessage = "Settings Applied!";
            _confirmationTimer = 3f;
            CheckIfDirty();
        }

        private void BackToPrevious()
        {
            foreach (var setting in _settings)
            {
                setting.Revert();
            }
            // This could be smarter, remembering the previous scene, but for now, it goes to MainMenu or TerminalMap
            Core.CurrentSceneManager.ChangeScene(GameSceneState.MainMenu);
        }

        private void CheckIfDirty()
        {
            bool isDirty = _settings.Any(s => s.IsDirty);
            _buttons[0].IsEnabled = isDirty; // Enable/disable Apply button
        }

        public override void Update(GameTime gameTime)
        {
            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            if (_confirmationTimer > 0)
            {
                _confirmationTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Update(currentMouseState);
                if (_buttons[i].IsHovered)
                {
                    _selectedControlIndex = i + _settings.Count;
                }
            }

            if (currentKeyboardState.IsKeyDown(Keys.Down) && !_previousKeyboardState.IsKeyDown(Keys.Down))
            {
                _selectedControlIndex = (_selectedControlIndex + 1) % (_settings.Count + _buttons.Count);
            }
            if (currentKeyboardState.IsKeyDown(Keys.Up) && !_previousKeyboardState.IsKeyDown(Keys.Up))
            {
                _selectedControlIndex = (_selectedControlIndex - 1 + _settings.Count + _buttons.Count) % (_settings.Count + _buttons.Count);
            }

            if (_selectedControlIndex < _settings.Count)
            {
                if (currentKeyboardState.IsKeyDown(Keys.Left) && !_previousKeyboardState.IsKeyDown(Keys.Left) ||
                    currentKeyboardState.IsKeyDown(Keys.Right) && !_previousKeyboardState.IsKeyDown(Keys.Right))
                {
                    _settings[_selectedControlIndex].HandleInput(Keys.Left); // Input key doesn't matter for bool toggle
                    CheckIfDirty();
                }
            }
            else
            {
                if (currentKeyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                {
                    _buttons[_selectedControlIndex - _settings.Count].TriggerClick();
                }
            }

            if (currentKeyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                BackToPrevious();
            }

            _previousKeyboardState = currentKeyboardState;
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            var font = Global.Instance.DefaultFont;
            int screenWidth = Global.Instance.CurrentGraphics.PreferredBackBufferWidth;

            spriteBatch.Begin();
            using (var pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1))
            {
                pixel.SetData(new[] { Color.White });

                string title = "Settings";
                Vector2 titleSize = font.MeasureString(title) * 1.5f;
                spriteBatch.DrawString(font, title, new Vector2(screenWidth / 2 - titleSize.X / 2, 50), Global.Instance.palette_BrightWhite, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);

                for (int i = 0; i < _settings.Count; i++)
                {
                    bool isSelected = (i == _selectedControlIndex);
                    _settings[i].Draw(spriteBatch, font, pixel, new Vector2(screenWidth / 2 - 200, 150 + i * 40), isSelected);
                }

                for (int i = 0; i < _buttons.Count; i++)
                {
                    _buttons[i].Draw(spriteBatch, font, pixel);
                    if (i + _settings.Count == _selectedControlIndex)
                    {
                        Rectangle highlightRect = _buttons[i].Bounds;
                        highlightRect.Inflate(4, 4);
                        DrawRectangleBorder(spriteBatch, pixel, highlightRect, 2, Global.Instance.palette_Yellow);
                    }
                }

                if (_confirmationTimer > 0)
                {
                    Vector2 msgSize = font.MeasureString(_confirmationMessage);
                    int buttonY = 200 + _settings.Count * 40;
                    spriteBatch.DrawString(font, _confirmationMessage, new Vector2(screenWidth / 2 - msgSize.X / 2, buttonY + 60), Global.Instance.palette_Teal);
                }
            }
            spriteBatch.End();
        }

        private void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}