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

        // --- Intro Animation State ---
        private enum IntroState
        {
            Waiting,
            TitleAnimating,
            Button1Animating,
            Button2Animating,
            Done
        }
        private IntroState _introState = IntroState.Waiting;
        private float _stateTimer = 0f;

        // Individual animation timers (0.0 to 1.0 progress)
        private float _titleAnimTimer = 0f;
        private float _btn1AnimTimer = 0f;
        private float _btn2AnimTimer = 0f;

        // Tuning
        private const float INITIAL_DELAY = 0.5f;
        private const float STAGGER_DELAY = 0.3f;
        private const float POP_DURATION = 0.5f;

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

            // Reset Animation State
            _introState = IntroState.Waiting;
            _stateTimer = 0f;
            _titleAnimTimer = 0f;
            _btn1AnimTimer = 0f;
            _btn2AnimTimer = 0f;

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

            // --- Update Intro Sequence ---
            _stateTimer += dt;

            // Always update individual animation timers if they have started
            if (_introState >= IntroState.TitleAnimating) _titleAnimTimer += dt;
            if (_introState >= IntroState.Button1Animating) _btn1AnimTimer += dt;
            if (_introState >= IntroState.Button2Animating) _btn2AnimTimer += dt;

            // State Machine for triggering the next element
            switch (_introState)
            {
                case IntroState.Waiting:
                    if (_stateTimer >= INITIAL_DELAY)
                    {
                        _introState = IntroState.TitleAnimating;
                        _stateTimer = 0f;
                    }
                    break;
                case IntroState.TitleAnimating:
                    if (_stateTimer >= STAGGER_DELAY)
                    {
                        _introState = IntroState.Button1Animating;
                        _stateTimer = 0f;
                    }
                    break;
                case IntroState.Button1Animating:
                    if (_stateTimer >= STAGGER_DELAY)
                    {
                        _introState = IntroState.Button2Animating;
                        _stateTimer = 0f;
                    }
                    break;
                case IntroState.Button2Animating:
                    if (_stateTimer >= STAGGER_DELAY)
                    {
                        _introState = IntroState.Done;
                    }
                    break;
                case IntroState.Done:
                    // Animation sequence finished, allow input
                    break;
            }

            // Block input until animations are done
            if (_introState != IntroState.Done) return;

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

            // Draw Background (Always visible)
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black);

            // --- Draw Title ---
            if (_introState >= IntroState.TitleAnimating)
            {
                string title = _gameOverText;
                Vector2 titleSize = font.MeasureString(title);
                Vector2 origin = titleSize / 2f;

                float time = (float)gameTime.TotalGameTime.TotalSeconds;
                float bobOffset = MathF.Sin(time * 4f) > 0 ? -1f : 0f;

                Vector2 titlePos = new Vector2(
                    (Global.VIRTUAL_WIDTH) / 2,
                    (Global.VIRTUAL_HEIGHT / 3) + bobOffset
                );

                // Calculate Pop Scale
                float progress = Math.Clamp(_titleAnimTimer / POP_DURATION, 0f, 1f);
                float scale = Easing.EaseOutBack(progress);

                spriteBatch.DrawStringSnapped(font, title, titlePos, _global.Palette_Red, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            // --- Draw Buttons ---
            // We need to break the batch to apply individual scaling matrices for the buttons
            spriteBatch.End();

            for (int i = 0; i < _buttons.Count; i++)
            {
                float animTimer = (i == 0) ? _btn1AnimTimer : _btn2AnimTimer;
                bool shouldDraw = (i == 0 && _introState >= IntroState.Button1Animating) ||
                                  (i == 1 && _introState >= IntroState.Button2Animating);

                if (shouldDraw)
                {
                    float progress = Math.Clamp(animTimer / POP_DURATION, 0f, 1f);
                    float scale = Easing.EaseOutBack(progress);

                    // Create a transform matrix for this specific button to scale from its center
                    Vector2 center = _buttons[i].Bounds.Center.ToVector2();
                    Matrix buttonTransform = Matrix.CreateTranslation(-center.X, -center.Y, 0) *
                                             Matrix.CreateScale(scale) *
                                             Matrix.CreateTranslation(center.X, center.Y, 0) *
                                             transform; // Combine with global transform

                    spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: buttonTransform);

                    bool forceHover = (i == _selectedButtonIndex) && _sceneManager.LastInputDevice == InputDevice.Keyboard;
                    _buttons[i].Draw(spriteBatch, tertiaryFont, gameTime, Matrix.Identity, forceHover);

                    spriteBatch.End();
                }
            }

            // Resume main batch for grid (if enabled)
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            if (_global.ShowSplitMapGrid)
            {
                // Debug drawing...
            }
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            // No fade overlay needed anymore
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            if (_buttons.Count > 0) return _buttons[0].Bounds;
            return null;
        }
    }
}