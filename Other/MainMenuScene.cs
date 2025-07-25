﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Scenes
{
    public class MainMenuScene : GameScene
    {
        private readonly SceneManager _sceneManager;
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;

        private readonly List<Button> _buttons = new();
        private int _selectedButtonIndex = -1;
        private KeyboardState _previousKeyboardState;

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private ConfirmationDialog _confirmationDialog;

        public MainMenuScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
        }

        public override void Initialize()
        {
            _confirmationDialog = new ConfirmationDialog(this);

            int screenWidth = Global.VIRTUAL_WIDTH;
            int buttonWidth = 200;
            int buttonHeight = 20;

            var playButton = new Button(new Rectangle(screenWidth / 2 - buttonWidth / 2, 260, buttonWidth, buttonHeight), "PLAY");
            playButton.OnClick += () => _sceneManager.ChangeScene(GameSceneState.TerminalMap);

            var settingsButton = new Button(new Rectangle(screenWidth / 2 - buttonWidth / 2, 280, buttonWidth, buttonHeight), "SETTINGS");
            settingsButton.OnClick += () => _sceneManager.ChangeScene(GameSceneState.Settings);

            var exitButton = new Button(new Rectangle(screenWidth / 2 - buttonWidth / 2, 300, buttonWidth, buttonHeight), "EXIT");
            exitButton.OnClick += ConfirmExit;

            _buttons.Add(playButton);
            _buttons.Add(settingsButton);
            _buttons.Add(exitButton);
        }

        private void ConfirmExit()
        {
            _confirmationDialog.Show(
                "Are you sure you want to exit?",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("YES", new Action(() => _core.ExitApplication())),
                    Tuple.Create("[gray]NO", new Action(() => _confirmationDialog.Hide()))
                }
            );
        }

        public override void Enter()
        {
            base.Enter();
            _currentInputDelay = _inputDelay;
            _previousKeyboardState = Keyboard.GetState();

            if (this.LastUsedInputForNav == InputDevice.Keyboard && !firstTimeOpened)
            {
                _selectedButtonIndex = 0;
                PositionMouseOnFirstSelectable();

                var firstButtonBounds = GetFirstSelectableElementBounds();
                if (firstButtonBounds.HasValue)
                {
                    Point screenPos = Core.TransformVirtualToScreen(firstButtonBounds.Value.Center);
                    var fakeMouseState = new MouseState(screenPos.X, screenPos.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    Vector2 virtualPos = Core.TransformMouse(fakeMouseState.Position);

                    foreach (var button in _buttons)
                    {
                        button.UpdateHoverState(virtualPos);
                    }
                }
            }
            else
            {
                _selectedButtonIndex = -1;
            }

            firstTimeOpened = false;
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            if (_buttons.Count > 0)
            {
                return _buttons[0].Bounds;
            }
            return null;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (IsInputBlocked)
            {
                _previousKeyboardState = Keyboard.GetState();
                previousMouseState = Mouse.GetState();
                return;
            }

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Update(gameTime);
                return;
            }

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            if (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                _sceneManager.LastInputDevice = InputDevice.Mouse;
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
                    if (_selectedButtonIndex <= -1) { _selectedButtonIndex = 0; }

                    _sceneManager.LastInputDevice = InputDevice.Keyboard;
                    var selectedButton = _buttons[_selectedButtonIndex];
                    if (selectedButton.IsHovered)
                    {
                        if (upPressed) _selectedButtonIndex = (_selectedButtonIndex - 1 + _buttons.Count) % _buttons.Count;
                        else _selectedButtonIndex = (_selectedButtonIndex + 1) % _buttons.Count;

                        Point screenPos = Core.TransformVirtualToScreen(_buttons[_selectedButtonIndex].Bounds.Center);
                        Mouse.SetPosition(screenPos.X, screenPos.Y);

                        _core.IsMouseVisible = false;
                        keyboardNavigatedLastFrame = true;
                    }
                    else
                    {
                        Point screenPos = Core.TransformVirtualToScreen(selectedButton.Bounds.Center);
                        Mouse.SetPosition(screenPos.X, screenPos.Y);

                        _core.IsMouseVisible = false;
                        keyboardNavigatedLastFrame = true;
                    }
                }

                if (currentKeyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                {
                    if (_selectedButtonIndex <= -1) _selectedButtonIndex = 0;

                    _sceneManager.LastInputDevice = InputDevice.Keyboard;
                    var selectedButton = _buttons[_selectedButtonIndex];
                    if (selectedButton.IsHovered)
                    {
                        selectedButton.TriggerClick();
                    }
                    else
                    {
                        Point screenPos = Core.TransformVirtualToScreen(selectedButton.Bounds.Center);
                        Mouse.SetPosition(screenPos.X, screenPos.Y);

                        _core.IsMouseVisible = false;
                        keyboardNavigatedLastFrame = true;
                    }
                }

                if (currentKeyboardState.IsKeyDown(Keys.Escape))
                {
                    ConfirmExit();
                }
            }

            _previousKeyboardState = currentKeyboardState;
            previousMouseState = currentMouseState;
        }

        public override void Draw(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            Texture2D pixel = ServiceLocator.Get<Texture2D>();

            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);

            spriteBatch.Draw(_spriteManager.LogoSprite, new Vector2(screenWidth / 2 - _spriteManager.LogoSprite.Width / 2, 150), Color.White);

            foreach (var button in _buttons)
            {
                button.Draw(spriteBatch, font, gameTime);
            }

            if (_selectedButtonIndex >= 0 && _selectedButtonIndex < _buttons.Count)
            {
                var selectedButton = _buttons[_selectedButtonIndex];

                if (selectedButton.IsHovered || keyboardNavigatedLastFrame)
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
                    DrawRectangleBorder(spriteBatch, pixel, highlightRect, 1, _global.ButtonHoverColor);
                }
            }

            spriteBatch.End();

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Draw(spriteBatch, font, gameTime);
            }
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Draw(spriteBatch, font, gameTime);
            }
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