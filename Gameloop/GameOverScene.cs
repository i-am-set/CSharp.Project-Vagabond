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
using ProjectVagabond.Transitions;
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
        private readonly TransitionManager _transitionManager;
        private readonly List<Button> _buttons = new();
        private int _selectedButtonIndex = -1;

        private float _inputDelay = 0.5f;
        private float _currentInputDelay = 0f;

        private string _gameOverText = "GAME OVER";

        private float _fadeInTimer = 0f;

        public GameOverScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        private void InitializeUI()
        {
            _buttons.Clear();
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            const int buttonPaddingX = 10;
            const int buttonPaddingY = 4;
            const int buttonSpacing = 0;

            int textY = Global.VIRTUAL_HEIGHT / 3;

            string text1 = "TRY AGAIN";
            Vector2 size1 = tertiaryFont.MeasureString(text1);
            int w1 = (int)size1.X + buttonPaddingX * 2;
            int h1 = (int)size1.Y + buttonPaddingY * 2;
            int x1 = (Global.VIRTUAL_WIDTH - w1) / 2;

            int buttonStartY = textY + 20;

            var tryAgainButton = new Button(
                new Rectangle(x1, buttonStartY, w1, h1),
                text1,
                font: tertiaryFont
            )
            {
                HoverAnimation = HoverAnimationType.Hop
            };
            tryAgainButton.OnClick += RestartGame;
            _buttons.Add(tryAgainButton);

            string text2 = "MAIN MENU";
            Vector2 size2 = tertiaryFont.MeasureString(text2);
            int w2 = (int)size2.X + buttonPaddingX * 2;
            int h2 = (int)size2.Y + buttonPaddingY * 2;
            int x2 = (Global.VIRTUAL_WIDTH - w2) / 2;
            int y2 = buttonStartY + h1 + buttonSpacing;

            var menuButton = new Button(
                new Rectangle(x2, y2, w2, h2),
                text2,
                font: tertiaryFont
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

            InitializeUI();

            _currentInputDelay = _inputDelay;
            _fadeInTimer = 0f;

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
            core.ResetGame();

            var spriteManager = ServiceLocator.Get<SpriteManager>();
            var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
            var gameState = ServiceLocator.Get<GameState>();
            var loadingScreen = ServiceLocator.Get<LoadingScreen>();

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
                // Use random transition
                var transition = _transitionManager.GetRandomTransition();
                _sceneManager.ChangeScene(GameSceneState.Split, transition, transition);
            };

            loadingScreen.Start();
        }

        private void GoToMainMenu()
        {
            var core = ServiceLocator.Get<Core>();
            core.ResetGame();
            // Use random transition
            var transition = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(GameSceneState.MainMenu, transition, transition);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_fadeInTimer < Global.UniversalSlowFadeDuration)
            {
                _fadeInTimer += dt;
            }

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= dt;
                return;
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
            var tertiaryFont = ServiceLocator.Get<Core>().TertiaryFont;

            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.Palette_Black);

            string title = _gameOverText;
            Vector2 titleSize = font.MeasureString(title);

            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            float bobOffset = MathF.Sin(time * 4f) > 0 ? -1f : 0f;

            Vector2 titlePos = new Vector2(
                (Global.VIRTUAL_WIDTH - titleSize.X) / 2,
                (Global.VIRTUAL_HEIGHT / 3) - (titleSize.Y / 2) + bobOffset
            );
            spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_Red);

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool forceHover = (i == _selectedButtonIndex) && _sceneManager.LastInputDevice == InputDevice.Keyboard;
                _buttons[i].Draw(spriteBatch, tertiaryFont, gameTime, transform, forceHover);
            }

            if (_global.ShowSplitMapGrid)
            {
                var titleRect = new Rectangle((int)titlePos.X, (int)titlePos.Y, (int)titleSize.X, (int)titleSize.Y);
                spriteBatch.DrawSnapped(pixel, titleRect, Color.Magenta * 0.5f);

                foreach (var button in _buttons)
                {
                    spriteBatch.DrawSnapped(pixel, button.Bounds, Color.Cyan * 0.5f);
                }
            }
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
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