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

        private string _gameOverText = "GAME OVER";

        // Fade In State
        private float _fadeInTimer = 0f;

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
            // Removed InitializeUI() from here. Fonts are not loaded yet.
        }

        private void InitializeUI()
        {
            _buttons.Clear();
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            const int buttonPaddingX = 10;
            const int buttonPaddingY = 4;
            const int buttonSpacing = 0; // No gap as requested

            // --- Layout Calculation ---
            // Text at roughly 1/3 down the screen
            int textY = Global.VIRTUAL_HEIGHT / 3;

            // --- TRY AGAIN Button ---
            string text1 = "TRY AGAIN";
            Vector2 size1 = secondaryFont.MeasureString(text1);
            int w1 = (int)size1.X + buttonPaddingX * 2;
            int h1 = (int)size1.Y + buttonPaddingY * 2;
            int x1 = (Global.VIRTUAL_WIDTH - w1) / 2;

            // Position buttons below text with some padding
            int buttonStartY = textY + 20;

            var tryAgainButton = new Button(
                new Rectangle(x1, buttonStartY, w1, h1),
                text1,
                font: secondaryFont
            )
            {
                HoverAnimation = HoverAnimationType.Hop
            };
            tryAgainButton.OnClick += RestartGame;
            _buttons.Add(tryAgainButton);

            // --- MAIN MENU Button ---
            string text2 = "MAIN MENU";
            Vector2 size2 = secondaryFont.MeasureString(text2);
            int w2 = (int)size2.X + buttonPaddingX * 2;
            int h2 = (int)size2.Y + buttonPaddingY * 2;
            int x2 = (Global.VIRTUAL_WIDTH - w2) / 2;
            int y2 = buttonStartY + h1 + buttonSpacing;

            var menuButton = new Button(
                new Rectangle(x2, y2, w2, h2),
                text2,
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

            // Initialize UI here to ensure fonts are loaded
            InitializeUI();

            _currentInputDelay = _inputDelay;
            _fadeInTimer = 0f; // Start fade in

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

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update Fade In
            if (_fadeInTimer < Global.UniversalSlowFadeDuration)
            {
                _fadeInTimer += dt;
            }

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= dt;
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

            // Bobbing Animation
            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            float bobOffset = MathF.Sin(time * 4f) > 0 ? -1f : 0f;

            // Position text at 1/3 height
            Vector2 titlePos = new Vector2(
                (Global.VIRTUAL_WIDTH - titleSize.X) / 2,
                (Global.VIRTUAL_HEIGHT / 3) - (titleSize.Y / 2) + bobOffset
            );
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_Red);

            // Draw Buttons
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool forceHover = (i == _selectedButtonIndex) && _sceneManager.LastInputDevice == InputDevice.Keyboard;
                // Force secondary font
                _buttons[i].Draw(spriteBatch, secondaryFont, gameTime, transform, forceHover);
            }

            // --- DEBUG DRAWING (F1) ---
            if (_global.ShowSplitMapGrid)
            {
                // Title Bounds
                var titleRect = new Rectangle((int)titlePos.X, (int)titlePos.Y, (int)titleSize.X, (int)titleSize.Y);
                spriteBatch.DrawSnapped(pixel, titleRect, Color.Magenta * 0.5f);

                // Button Bounds
                foreach (var button in _buttons)
                {
                    spriteBatch.DrawSnapped(pixel, button.Bounds, Color.Cyan * 0.5f);
                }
            }
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // Draw Fade In Overlay
            if (_fadeInTimer < Global.UniversalSlowFadeDuration)
            {
                float alpha = 1.0f - Math.Clamp(_fadeInTimer / Global.UniversalSlowFadeDuration, 0f, 1f);
                var pixel = ServiceLocator.Get<Texture2D>();
                var graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
                var screenBounds = new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
                spriteBatch.Draw(pixel, screenBounds, Color.Black * alpha);
            }
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            if (_buttons.Count > 0) return _buttons[0].Bounds;
            return null;
        }
    }
}