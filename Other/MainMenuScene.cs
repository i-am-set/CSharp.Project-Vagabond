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
        private int _selectedButtonIndex = -1;

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private ConfirmationDialog _confirmationDialog;
        private bool _uiInitialized = false;

        // --- Intro Animation State ---
        private bool _introSequenceStarted = false;
        private float _staggerTimer = 0f;
        private int _animatingButtonIndex = 0;
        private List<float> _buttonAnimTimers = new List<float>();
        private float _safetyTimer = 0f; // Watchdog timer for intro sequence

        // Tuning
        private const float BUTTON_STAGGER_DELAY = 0.15f; // Time between each button starting
        private const float BUTTON_ANIM_DURATION = 0.5f; // How long the pop-in takes
        private const float JIGGLE_FREQUENCY = 20f;
        private const float JIGGLE_MAGNITUDE = 0.15f; // Radians (approx 8 degrees)

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

            int maxTextWidth = (int)Math.Max(playSize.X, Math.Max(settingsSize.X, exitSize.X));
            int buttonWidth = maxTextWidth + horizontalPadding * 2;

            int buttonX = ((Global.VIRTUAL_WIDTH - buttonWidth) / 2) - 3 - 80;

            int playHeight = (int)playSize.Y + verticalPadding * 2;
            var playButton = new Button(
                new Rectangle(buttonX, (int)currentY, buttonWidth, playHeight),
                playText,
                font: secondaryFont,
                alignLeft: true
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                HoverAnimation = HoverAnimationType.SlideAndHold
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
                new Rectangle(buttonX, (int)currentY, buttonWidth, settingsHeight),
                settingsText,
                font: secondaryFont,
                alignLeft: true
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                HoverAnimation = HoverAnimationType.SlideAndHold
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
                new Rectangle(buttonX, (int)currentY, buttonWidth, exitHeight),
                exitText,
                font: secondaryFont,
                alignLeft: true
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                HoverAnimation = HoverAnimationType.SlideAndHold
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
            _introSequenceStarted = false;
            _animatingButtonIndex = 0;
            _staggerTimer = 0f;
            _safetyTimer = 0f; // Reset watchdog
            _buttonAnimTimers.Clear();
            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].ResetAnimationState();
                _buttonAnimTimers.Add(-1f); // -1 indicates animation hasn't started
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

            // 1. CRITICAL FIX: Block all input and logic if the transition is still active.
            if (_transitionManager.IsTransitioning)
            {
                return;
            }

            // 2. Start Intro Sequence once transition is done
            if (!_introSequenceStarted)
            {
                _introSequenceStarted = true;
                _staggerTimer = 0f;
            }

            // --- WATCHDOG SAFETY ---
            // If for any reason the intro sequence stalls (e.g. logic error, frame skip),
            // force all buttons to be visible after 2 seconds.
            _safetyTimer += dt;
            if (_safetyTimer > 2.0f)
            {
                for (int i = 0; i < _buttonAnimTimers.Count; i++)
                {
                    if (_buttonAnimTimers[i] < BUTTON_ANIM_DURATION)
                    {
                        _buttonAnimTimers[i] = BUTTON_ANIM_DURATION;
                    }
                }
            }

            // 3. Update Stagger Timer to trigger next button
            if (_animatingButtonIndex < _buttons.Count)
            {
                _staggerTimer += dt;
                if (_staggerTimer >= BUTTON_STAGGER_DELAY)
                {
                    _buttonAnimTimers[_animatingButtonIndex] = 0f; // Start this button
                    _animatingButtonIndex++;
                    _staggerTimer = 0f;
                }
            }

            // 4. Update Individual Button Animation Timers
            for (int i = 0; i < _buttonAnimTimers.Count; i++)
            {
                if (_buttonAnimTimers[i] >= 0f)
                {
                    _buttonAnimTimers[i] += dt;
                }
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
                // Only allow interaction if the button's entrance animation is mostly complete
                if (_buttonAnimTimers[i] >= BUTTON_ANIM_DURATION * 0.8f)
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

                    // Only trigger if animation is done
                    if (_buttonAnimTimers[_selectedButtonIndex] >= BUTTON_ANIM_DURATION * 0.8f)
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
                // Skip drawing if animation hasn't started
                if (_buttonAnimTimers[i] < 0f) continue;

                float timer = _buttonAnimTimers[i];
                float progress = Math.Clamp(timer / BUTTON_ANIM_DURATION, 0f, 1f);

                // Scale: EaseOutBack for overshoot
                float scale = Easing.EaseOutBack(progress);

                // Rotation: Damped Sine Wave (Jiggle)
                float rotation = 0f;
                if (progress < 1.0f)
                {
                    float decay = 1.0f - progress;
                    rotation = MathF.Sin(timer * JIGGLE_FREQUENCY) * JIGGLE_MAGNITUDE * decay;
                }

                // Create local transformation matrix for this button
                Vector2 center = _buttons[i].Bounds.Center.ToVector2();
                Matrix animMatrix = Matrix.CreateTranslation(-center.X, -center.Y, 0) *
                                    Matrix.CreateRotationZ(rotation) *
                                    Matrix.CreateScale(scale) *
                                    Matrix.CreateTranslation(center.X, center.Y, 0);

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
                if (_buttonAnimTimers[_selectedButtonIndex] >= BUTTON_ANIM_DURATION)
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