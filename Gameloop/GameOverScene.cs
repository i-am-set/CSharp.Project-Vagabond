using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        private readonly UIAnimator _titleAnimator;
        private readonly UIAnimator _retryBtnAnimator;
        private readonly UIAnimator _menuBtnAnimator;

        private const float INITIAL_DELAY = 0.5f;
        private const float STAGGER_DELAY = 0.2f;
        private const float POP_DURATION = 0.5f;

        public GameOverScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _gameState = ServiceLocator.Get<GameState>();
            _global = ServiceLocator.Get<Global>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();

            _titleAnimator = new UIAnimator
            {
                EntryStyle = EntryExitStyle.Pop,
                ExitStyle = EntryExitStyle.Pop,
                DurationIn = POP_DURATION,
                DurationOut = POP_DURATION
            };

            _retryBtnAnimator = new UIAnimator
            {
                EntryStyle = EntryExitStyle.Pop,
                ExitStyle = EntryExitStyle.Pop,
                DurationIn = POP_DURATION,
                DurationOut = POP_DURATION
            };

            _menuBtnAnimator = new UIAnimator
            {
                EntryStyle = EntryExitStyle.Pop,
                ExitStyle = EntryExitStyle.Pop,
                DurationIn = POP_DURATION,
                DurationOut = POP_DURATION
            };
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

            _titleAnimator.Reset();
            _retryBtnAnimator.Reset();
            _menuBtnAnimator.Reset();

            _titleAnimator.Show(delay: INITIAL_DELAY);
            _retryBtnAnimator.Show(delay: INITIAL_DELAY + STAGGER_DELAY);
            _menuBtnAnimator.Show(delay: INITIAL_DELAY + (STAGGER_DELAY * 2));

            foreach (var button in _buttons)
            {
                button.ResetAnimationState();
            }

            if (this.LastInputDevice == InputDevice.Keyboard)
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

            var gameState = ServiceLocator.Get<GameState>();

            var loadingTasks = new List<LoadingTask>
            {
                new GenericTask("Initializing world...", () =>
                {
                    gameState.InitializeWorld();
                })
            };

            var transitionOut = _transitionManager.GetRandomTransition();
            var transitionIn = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(GameSceneState.Split, transitionOut, transitionIn, 0f, loadingTasks);
        }

        private void GoToMainMenu()
        {
            var core = ServiceLocator.Get<Core>();
            core.ResetGame();
            var transition = _transitionManager.GetRandomTransition();
            _sceneManager.ChangeScene(GameSceneState.MainMenu, transition, transition);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _titleAnimator.Update(dt);
            _retryBtnAnimator.Update(dt);
            _menuBtnAnimator.Update(dt);

            if (!_menuBtnAnimator.IsVisible) return;

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
                var animator = (i == 0) ? _retryBtnAnimator : _menuBtnAnimator;
                if (animator.IsVisible)
                {
                    _buttons[i].Update(currentMouseState);
                    if (_buttons[i].IsHovered)
                    {
                        _selectedButtonIndex = i;
                    }
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

            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.Black);

            var titleState = _titleAnimator.GetVisualState();
            if (titleState.IsVisible)
            {
                string title = _gameOverText;
                Vector2 titleSize = font.MeasureString(title);
                Vector2 origin = titleSize / 2f;

                float time = (float)gameTime.TotalGameTime.TotalSeconds;
                float bobOffset = MathF.Sin(time * 4f) > 0 ? -1f : 0f;

                Vector2 titlePos = new Vector2(
                    (Global.VIRTUAL_WIDTH) / 2,
                    (Global.VIRTUAL_HEIGHT / 3) + bobOffset
                ) + titleState.Offset;

                Color drawColor = _global.Palette_Rust * titleState.Opacity;

                spriteBatch.DrawStringSnapped(font, title, titlePos, drawColor, 0f, origin, titleState.Scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();

            for (int i = 0; i < _buttons.Count; i++)
            {
                var animator = (i == 0) ? _retryBtnAnimator : _menuBtnAnimator;
                var state = animator.GetVisualState();

                if (state.IsVisible)
                {
                    Vector2 center = _buttons[i].Bounds.Center.ToVector2();

                    Matrix buttonTransform = Matrix.CreateTranslation(-center.X, -center.Y, 0) *
                                             Matrix.CreateScale(state.Scale.X, state.Scale.Y, 1.0f) *
                                             Matrix.CreateTranslation(center.X, center.Y, 0) *
                                             Matrix.CreateTranslation(state.Offset.X, state.Offset.Y, 0) *
                                             transform;

                    spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: buttonTransform);

                    bool forceHover = (i == _selectedButtonIndex) && _sceneManager.LastInputDevice == InputDevice.Keyboard;

                    _buttons[i].Draw(spriteBatch, tertiaryFont, gameTime, Matrix.Identity, forceHover);

                    spriteBatch.End();
                }
            }

            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            if (_global.ShowSplitMapGrid)
            {
            }
        }

        public override void DrawOverlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            if (_buttons.Count > 0) return _buttons[0].Bounds;
            return null;
        }
    }
}