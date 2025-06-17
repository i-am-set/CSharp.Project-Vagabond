﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System.Collections.Generic;

namespace ProjectVagabond.Scenes
{
    public class MainMenuScene : GameScene
    {
        private readonly List<Button> _buttons = new();
        private int _selectedButtonIndex = 0;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private bool _keyboardNavigatedLastFrame = false;
        
        private float _inputDelay = 0.1f; // 1.0f would be a 1 second delay
        private float _currentInputDelay = 0f;

        public override void Initialize()
        {
            int screenWidth = Global.VIRTUAL_WIDTH; // Use virtual width for positioning
            int buttonWidth = 200;
            int buttonHeight = 20;

            var playButton = new Button(new Rectangle(screenWidth / 2 - buttonWidth / 2, 260, buttonWidth, buttonHeight), "Play");
            playButton.OnClick += () => Core.CurrentSceneManager.ChangeScene(GameSceneState.TerminalMap);

            var settingsButton = new Button(new Rectangle(screenWidth / 2 - buttonWidth / 2, 280, buttonWidth, buttonHeight), "Settings");
            settingsButton.OnClick += () => Core.CurrentSceneManager.ChangeScene(GameSceneState.Settings);

            var exitButton = new Button(new Rectangle(screenWidth / 2 - buttonWidth / 2, 300, buttonWidth, buttonHeight), "Exit");
            exitButton.OnClick += () => Core.Instance.ExitApplication();

            _buttons.Add(playButton);
            _buttons.Add(settingsButton);
            _buttons.Add(exitButton);
        }

        public override void Enter()
        {
            base.Enter();
            _currentInputDelay = _inputDelay;
            _previousMouseState = Mouse.GetState();
            Core.Instance.IsMouseVisible = true;
        }

        public override void Update(GameTime gameTime)
        {
            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            if (_keyboardNavigatedLastFrame)
            {
                _keyboardNavigatedLastFrame = false;
            }
            else if (currentMouseState.Position != _previousMouseState.Position)
            {
                Core.Instance.IsMouseVisible = true;
            }

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Update(currentMouseState);
                if (_buttons[i].IsHovered)
                {
                    _selectedButtonIndex = i;
                }
            }

            if (_currentInputDelay <= 0)
            {
                bool upPressed = currentKeyboardState.IsKeyDown(Keys.Up) && !_previousKeyboardState.IsKeyDown(Keys.Up);
                bool downPressed = currentKeyboardState.IsKeyDown(Keys.Down) && !_previousKeyboardState.IsKeyDown(Keys.Down);

                if (upPressed || downPressed)
                {
                    if (upPressed)
                    {
                        _selectedButtonIndex = (_selectedButtonIndex - 1 + _buttons.Count) % _buttons.Count;
                    }
                    else // downPressed
                    {
                        _selectedButtonIndex = (_selectedButtonIndex + 1) % _buttons.Count;
                    }

                    Point screenPos = Core.TransformVirtualToScreen(_buttons[_selectedButtonIndex].Bounds.Center);
                    Mouse.SetPosition(screenPos.X, screenPos.Y);
                    
                    Core.Instance.IsMouseVisible = false;
                    _keyboardNavigatedLastFrame = true;
                }

                if (currentKeyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                {
                    _buttons[_selectedButtonIndex].TriggerClick();
                }
            }

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = Global.Instance.CurrentSpriteBatch;
            var font = Global.Instance.DefaultFont;
            int screenWidth = Global.VIRTUAL_WIDTH;

            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);
            Core.Pixel.SetData(new[] { Color.White });

            spriteBatch.Draw(Core.CurrentSpriteManager.LogoSprite, new Vector2(screenWidth / 2 - Core.CurrentSpriteManager.LogoSprite.Width / 2, 150), Color.White);

            foreach (var button in _buttons)
            {
                button.Draw(spriteBatch, font);
            }

            var selectedButton = _buttons[_selectedButtonIndex];

            if (selectedButton.IsHovered || _keyboardNavigatedLastFrame)
            {
                Vector2 textSize = font.MeasureString(selectedButton.Text);

                int horizontalPadding = 8;
                int verticalPadding = 4;

                Rectangle highlightRect = new Rectangle(
                    (int)(selectedButton.Bounds.X + (selectedButton.Bounds.Width - textSize.X) * 0.5f - horizontalPadding),
                    (int)(selectedButton.Bounds.Y + (selectedButton.Bounds.Height - textSize.Y) * 0.5f - verticalPadding),
                    (int)(textSize.X + horizontalPadding * 2),
                    (int)(textSize.Y + verticalPadding * 2)
                );
                DrawRectangleBorder(spriteBatch, Core.Pixel, highlightRect, 1, Global.Instance.OptionHoverColor);
            }
            
            spriteBatch.End();
        }

        private static void DrawRectangleBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }
    }
}