#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProjectVagabond.Scenes
{
    public class GameOverScene : GameScene
    {
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly Global _global;
        private readonly SpriteManager _spriteManager;

        private readonly List<Button> _buttons = new();
        private int _selectedButtonIndex = -1;

        private float _inputDelay = 0.5f; // Longer delay to prevent accidental skips
        private float _currentInputDelay = 0f;

        private string _killerName = "";
        private string _gameOverText = "GAME OVER";

        public GameOverScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            base.Initialize();
            InitializeUI();
        }

        private void InitializeUI()
        {
            _buttons.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            const int buttonWidth = 100;
            const int buttonHeight = 20;
            const int buttonSpacing = 5;
            int startY = Global.VIRTUAL_HEIGHT / 2 + 20;
            int centerX = (Global.VIRTUAL_WIDTH - buttonWidth) / 2;

            // --- TRY AGAIN Button ---
            var tryAgainButton = new Button(
                new Rectangle(centerX, startY, buttonWidth, buttonHeight),
                "TRY AGAIN",
                font: secondaryFont
            )
            {
                HoverAnimation = HoverAnimationType.Hop
            };
            tryAgainButton.OnClick += RestartGame;
            _buttons.Add(tryAgainButton);

            // --- MAIN MENU Button ---
            var menuButton = new Button(
                new Rectangle(centerX, startY + buttonHeight + buttonSpacing, buttonWidth, buttonHeight),
                "MAIN MENU",
                font: secondaryFont
            )
            {
                HoverAnimation = HoverAnimationType.Hop
            };
            menuButton.OnClick += GoToMainMenu;
            _buttons.Add(menuButton);
        }

        public override void Enter()
        {
            base.Enter();
            _currentInputDelay = _inputDelay;
            _killerName = _gameState.LastRunKiller;

            // Reset button states
            foreach (var button in _buttons)
            {
                button.ResetAnimationState();
            }

            if (this.LastUsedInputForNav == InputDevice.Keyboard)
            {
                _selectedButtonIndex = 0;
                PositionMouseOnFirstSelectable();
            }
            else
            {
                _selectedButtonIndex = -1;
            }
        }

        private void RestartGame()
        {
            var core = ServiceLocator.Get<Core>();
            core.ResetGame(); // Clears state

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var gameState = ServiceLocator.Get<GameState>();
            var loadingScreen = ServiceLocator.Get<LoadingScreen>();

            // Re-run initialization tasks
            var loadingTasks = new List<LoadingTask>
            {
                new GenericTask("Initializing world...", () =>
                {
                    gameState.InitializeWorld();
                    gameState.InitializeRenderableEntities();
                }),
                new DiceWarmupTask()
            };

            loadingScreen.Clear();
            foreach (var task in loadingTasks)
            {
                loadingScreen.AddTask(task);
            }

            loadingScreen.OnComplete += () =>
            {
                _sceneManager.ChangeScene(GameSceneState.Split);
            };

            loadingScreen.Start();
        }

        private void GoToMainMenu()
        {
            var core = ServiceLocator.Get<Core>();
            core.ResetGame();
            _sceneManager.ChangeScene(GameSceneState.MainMenu);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                return; // Block input during delay
            }

            var currentMouseState = Mouse.GetState();
            var currentKeyboardState = Keyboard.GetState();

            if (currentMouseState.Position != previousMouseState.Position || (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released))
            {
                _sceneManager.LastInputDevice = InputDevice.Mouse;
                _selectedButtonIndex = -1;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Update(currentMouseState);
                if (_buttons[i].IsHovered)
                {
                    _selectedButtonIndex = i;
                }
            }

            // Keyboard Navigation
            if (KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState))
            {
                _sceneManager.LastInputDevice = InputDevice.Keyboard;
                if (_selectedButtonIndex == -1) _selectedButtonIndex = 0;
                else _selectedButtonIndex = (_selectedButtonIndex - 1 + _buttons.Count) % _buttons.Count;
                SnapMouseToSelection();
            }
            else if (KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState))
            {
                _sceneManager.LastInputDevice = InputDevice.Keyboard;
                if (_selectedButtonIndex == -1) _selectedButtonIndex = 0;
                else _selectedButtonIndex = (_selectedButtonIndex + 1) % _buttons.Count;
                SnapMouseToSelection();
            }

            if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
            {
                if (_selectedButtonIndex != -1)
                {
                    _buttons[_selectedButtonIndex].TriggerClick();
                }
            }

            _previousKeyboardState = currentKeyboardState;
            previousMouseState = currentMouseState;
        }

        private void SnapMouseToSelection()
        {
            if (_selectedButtonIndex != -1)
            {
                Point screenPos = Core.TransformVirtualToScreen(_buttons[_selectedButtonIndex].Bounds.Center);
                Mouse.SetPosition(screenPos.X, screenPos.Y);
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            var pixel = ServiceLocator.Get<Texture2D>();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            // Draw Background (Solid Black)
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Black);

            // Draw "GAME OVER"
            string title = _gameOverText;
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2(
                (Global.VIRTUAL_WIDTH - titleSize.X) / 2,
                Global.VIRTUAL_HEIGHT / 3 - titleSize.Y
            );
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_Red);

            // Draw "Slain by..."
            string killerText = $"SLAIN BY {_killerName.ToUpper()}";
            Vector2 killerSize = secondaryFont.MeasureString(killerText);
            Vector2 killerPos = new Vector2(
                (Global.VIRTUAL_WIDTH - killerSize.X) / 2,
                titlePos.Y + titleSize.Y + 10
            );
            spriteBatch.DrawStringSnapped(secondaryFont, killerText, killerPos, _global.Palette_Gray);

            // Draw Buttons
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool forceHover = (i == _selectedButtonIndex) && _sceneManager.LastInputDevice == InputDevice.Keyboard;
                _buttons[i].Draw(spriteBatch, font, gameTime, transform, forceHover);
            }
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            if (_buttons.Count > 0) return _buttons[0].Bounds;
            return null;
        }
    }
}
#nullable restore
﻿