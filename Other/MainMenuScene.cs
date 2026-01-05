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
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace ProjectVagabond.Scenes
{
    public class MainMenuScene : GameScene
    {
        private readonly SceneManager _sceneManager;
        private readonly SpriteManager _spriteManager;
        private readonly Global _global;
        private readonly ParticleSystemManager _particleSystemManager;
        private readonly TransitionManager _transitionManager;
        private readonly HapticsManager _hapticsManager;

        private readonly List<Button> _buttons = new();
        private readonly List<UIAnimator> _buttonAnimators = new();
        private int _selectedButtonIndex = -1;

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private ConfirmationDialog _confirmationDialog;
        private bool _uiInitialized = false;

        // Tuning
        private const float BUTTON_STAGGER_DELAY = 0.15f; // Time between each button starting
        private const float BUTTON_ANIM_DURATION = 0.5f; // How long the pop-in takes

        public MainMenuScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
        }

        public override Rectangle GetAnimatedBounds()
        {
            return new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        public override void Initialize()
        {
            _confirmationDialog = new ConfirmationDialog(this);
        }

        private void InitializeUI()
        {
            if (_uiInitialized) return;

            _buttons.Clear();

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            const int horizontalPadding = 4;
            const int verticalPadding = 2;
            const int buttonYSpacing = 0;
            float currentY = 90f;

            string playText = "PLAY";
            string settingsText = "SETTINGS";
            string exitText = "EXIT";

            Vector2 playSize = secondaryFont.MeasureString(playText);
            Vector2 settingsSize = secondaryFont.MeasureString(settingsText);
            Vector2 exitSize = secondaryFont.MeasureString(exitText);

            // Calculate widths independently so buttons don't affect each other
            int playWidth = (int)playSize.X + horizontalPadding * 2;
            int settingsWidth = (int)settingsSize.X + horizontalPadding * 2;
            int exitWidth = (int)exitSize.X + horizontalPadding * 2;

            // Use a fixed X coordinate to anchor the left side.
            // This value (48) matches the original visual position for the default text length.
            // Original Calc: ((320 - ~60) / 2) - 83 ~= 47/48 pixels.
            int buttonX = 48;

            int playHeight = (int)playSize.Y + verticalPadding * 2;
            var playButton = new Button(
                new Rectangle(buttonX, (int)currentY, playWidth, playHeight),
                playText,
                font: secondaryFont,
                alignLeft: true
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                HoverAnimation = HoverAnimationType.SlideAndHold,
                WaveEffectType = TextEffectType.LeftAlignedSmallWave
            };
            playButton.OnClick += () =>
            {
                _hapticsManager.TriggerCompoundShake(0.5f);
                var core = ServiceLocator.Get<Core>();
                var spriteManager = ServiceLocator.Get<SpriteManager>();
                var archetypeManager = ServiceLocator.Get<ArchetypeManager>();
                var gameState = ServiceLocator.Get<GameState>();
                var loadingScreen = ServiceLocator.Get<LoadingScreen>();

                var loadingTasks = new List<LoadingTask>
                {
                    new GenericTask("Loading game assets...", () => spriteManager.LoadGameContent()),
                    new GenericTask("Loading archetypes...", () => archetypeManager.LoadArchetypes("Content/Data/Archetypes.json")),
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
                    core.SetGameLoaded(true);
                    // Use None Out (Instant) -> Fade In (Smooth)
                    _sceneManager.ChangeScene(GameSceneState.Split, TransitionType.None, TransitionType.Diamonds);
                };

                loadingScreen.Start();
            };
            _buttons.Add(playButton);
            currentY += playHeight + buttonYSpacing;

            int settingsHeight = (int)settingsSize.Y + verticalPadding * 2;
            var settingsButton = new Button(
                new Rectangle(buttonX, (int)currentY, settingsWidth, settingsHeight),
                settingsText,
                font: secondaryFont,
                alignLeft: true
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                HoverAnimation = HoverAnimationType.SlideAndHold,
                WaveEffectType = TextEffectType.LeftAlignedSmallWave
            };
            settingsButton.OnClick += () =>
            {
                _hapticsManager.TriggerCompoundShake(0.5f);
                _sceneManager.ShowModal(GameSceneState.Settings);
            };
            _buttons.Add(settingsButton);
            currentY += settingsHeight + buttonYSpacing;

            int exitHeight = (int)exitSize.Y + verticalPadding * 2;
            var exitButton = new Button(
                new Rectangle(buttonX, (int)currentY, exitWidth, exitHeight),
                exitText,
                font: secondaryFont,
                alignLeft: true
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                HoverAnimation = HoverAnimationType.SlideAndHold,
                WaveEffectType = TextEffectType.LeftAlignedSmallWave
            };
            exitButton.OnClick += ConfirmExit;
            _buttons.Add(exitButton);

            _uiInitialized = true;
        }

        private void ConfirmExit()
        {
            _hapticsManager.TriggerCompoundShake(0.5f);
            _confirmationDialog.Show(
                "Are you sure you want to exit?",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("[gray]YES", new Action(() => { _hapticsManager.TriggerCompoundShake(0.5f); ServiceLocator.Get<Core>().ExitApplication(); })),
                    Tuple.Create("NO", new Action(() => { _hapticsManager.TriggerCompoundShake(0.5f); _confirmationDialog.Hide(); }))
                }
            );
        }

        public override void Enter()
        {
            base.Enter();
            InitializeUI();

            _currentInputDelay = _inputDelay;
            _previousKeyboardState = Keyboard.GetState();

            // Reset Animation State
            _buttonAnimators.Clear();
            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].ResetAnimationState();

                var animator = new UIAnimator
                {
                    Style = EntryExitStyle.PopJiggle,
                    Duration = BUTTON_ANIM_DURATION
                };
                // Stagger the start of each button
                animator.Show(delay: i * BUTTON_STAGGER_DELAY);
                _buttonAnimators.Add(animator);
            }

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

        public override void Exit()
        {
            base.Exit();
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

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Block all input and logic if the transition is still active.
            if (_transitionManager.IsTransitioning)
            {
                return;
            }

            // Update Animators
            foreach (var animator in _buttonAnimators)
            {
                animator.Update(dt);
            }

            var currentMouseState = Mouse.GetState();
            var virtualMousePos = Core.TransformMouse(currentMouseState.Position);

            if (IsInputBlocked)
            {
                return;
            }

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Update(gameTime);
                return;
            }

            var currentKeyboardState = Keyboard.GetState();

            if (currentMouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            {
                _sceneManager.LastInputDevice = InputDevice.Mouse;
            }

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= dt;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                // Only allow interaction if the button is mostly visible (Scale > 0.8)
                var visualState = _buttonAnimators[i].GetCurrentState();
                if (visualState.IsVisible && visualState.Scale.X > 0.8f)
                {
                    _buttons[i].Update(currentMouseState);
                    if (_buttons[i].IsHovered)
                    {
                        _selectedButtonIndex = i;
                    }
                }
            }

            if (_currentInputDelay <= 0)
            {
                bool upPressed = KeyPressed(Keys.Up, currentKeyboardState, _previousKeyboardState);
                bool downPressed = KeyPressed(Keys.Down, currentKeyboardState, _previousKeyboardState);

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

                        keyboardNavigatedLastFrame = true;
                    }
                    else
                    {
                        Point screenPos = Core.TransformVirtualToScreen(selectedButton.Bounds.Center);
                        Mouse.SetPosition(screenPos.X, screenPos.Y);

                        keyboardNavigatedLastFrame = true;
                    }
                }

                if (KeyPressed(Keys.Enter, currentKeyboardState, _previousKeyboardState))
                {
                    if (_selectedButtonIndex <= -1) _selectedButtonIndex = 0;

                    _sceneManager.LastInputDevice = InputDevice.Keyboard;
                    var selectedButton = _buttons[_selectedButtonIndex];

                    // Only trigger if animation is mostly done
                    var visualState = _buttonAnimators[_selectedButtonIndex].GetCurrentState();
                    if (visualState.IsVisible && visualState.Scale.X > 0.8f)
                    {
                        if (selectedButton.IsHovered)
                        {
                            selectedButton.TriggerClick();
                        }
                        else
                        {
                            Point screenPos = Core.TransformVirtualToScreen(selectedButton.Bounds.Center);
                            Mouse.SetPosition(screenPos.X, screenPos.Y);

                            keyboardNavigatedLastFrame = true;
                        }
                    }
                }

                if (KeyPressed(Keys.Escape, currentKeyboardState, _previousKeyboardState))
                {
                    ConfirmExit();
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            spriteBatch.DrawSnapped(_spriteManager.LogoSprite, new Vector2(screenWidth / 2 - _spriteManager.LogoSprite.Width / 2, 25), Color.White);

            // End the main batch to allow individual button transforms
            spriteBatch.End();

            for (int i = 0; i < _buttons.Count; i++)
            {
                var state = _buttonAnimators[i].GetCurrentState();
                if (!state.IsVisible) continue;

                // Create local transformation matrix for this button
                Vector2 center = _buttons[i].Bounds.Center.ToVector2();
                Matrix animMatrix = Matrix.CreateTranslation(-center.X, -center.Y, 0) *
                                    Matrix.CreateRotationZ(state.Rotation) *
                                    Matrix.CreateScale(state.Scale.X, state.Scale.Y, 1.0f) *
                                    Matrix.CreateTranslation(center.X, center.Y, 0) *
                                    Matrix.CreateTranslation(state.Offset.X, state.Offset.Y, 0);

                // Combine with global transform
                Matrix finalTransform = animMatrix * transform;

                // Start a new batch for this specific button with its unique transform
                spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform);

                bool forceHover = (i == _selectedButtonIndex) && (_sceneManager.LastInputDevice == InputDevice.Keyboard || keyboardNavigatedLastFrame);
                _buttons[i].Draw(spriteBatch, font, gameTime, Matrix.Identity, forceHover);

                spriteBatch.End();
            }

            // Restart the main batch for the rest of the UI (Arrows, Dialogs)
            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            if (_selectedButtonIndex >= 0 && _selectedButtonIndex < _buttons.Count)
            {
                // Only draw arrow if button is fully visible
                var state = _buttonAnimators[_selectedButtonIndex].GetCurrentState();
                if (state.IsVisible && state.Scale.X >= 0.95f)
                {
                    var selectedButton = _buttons[_selectedButtonIndex];
                    if (selectedButton.IsHovered)
                    {
                        var bounds = selectedButton.Bounds;
                        var color = _global.ButtonHoverColor;
                        var fontToUse = selectedButton.Font ?? secondaryFont;

                        string leftArrow = ">";
                        var arrowSize = fontToUse.MeasureString(leftArrow);

                        float pressOffset = selectedButton.IsPressed ? 2f : 0f;

                        var leftPos = new Vector2(bounds.Left - arrowSize.Width - 4 + pressOffset, bounds.Center.Y - arrowSize.Height / 2f + selectedButton.TextRenderOffset.Y);

                        spriteBatch.DrawStringSnapped(fontToUse, leftArrow, leftPos, color);
                    }
                }
            }

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawContent(spriteBatch, font, gameTime, transform);
            }
        }

        public override void DrawFullscreenUI(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
        }

        public override void DrawUnderlay(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime)
        {
            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.DrawOverlay(spriteBatch);
            }
        }
    }
}