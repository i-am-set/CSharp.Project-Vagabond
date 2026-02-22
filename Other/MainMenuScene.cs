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
        private readonly InputManager _inputManager;

        private readonly List<Button> _buttons = new();
        private readonly List<UIAnimator> _buttonAnimators = new();
        private readonly NavigationGroup _navigationGroup;

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private ConfirmationDialog _confirmationDialog;
        private bool _uiInitialized = false;

        private const float BUTTON_STAGGER_DELAY = 0.15f;

        public MainMenuScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _navigationGroup = new NavigationGroup(wrapNavigation: true);
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
            _navigationGroup.Clear();

            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            const int horizontalPadding = 4;
            const int verticalPadding = 2;
            const int buttonYSpacing = 0;
            float currentY = 90f;
            int screenCenterX = Global.VIRTUAL_WIDTH / 2;

            string playText = "PLAY";
            string settingsText = "SETTINGS";
            string exitText = "EXIT";

            Vector2 playSize = secondaryFont.MeasureString(playText);
            Vector2 settingsSize = secondaryFont.MeasureString(settingsText);
            Vector2 exitSize = secondaryFont.MeasureString(exitText);

            int playWidth = (int)playSize.X + horizontalPadding * 2;
            int settingsWidth = (int)settingsSize.X + horizontalPadding * 2;
            int exitWidth = (int)exitSize.X + horizontalPadding * 2;

            int playHeight = (int)playSize.Y + verticalPadding * 2;
            int playX = screenCenterX - (playWidth / 2);
            var playButton = new Button(
                new Rectangle(playX, (int)currentY, playWidth, playHeight),
                playText,
                font: secondaryFont,
                alignLeft: false
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                EnableTextWave = true,
                AlwaysAnimateText = true,
                WaveEffectType = TextEffectType.TypewriterPop,
                EnableHoverSway = false
            };
            playButton.OnClick += () =>
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                playButton.ResetAnimationState();

                var core = ServiceLocator.Get<Core>();
                var gameState = ServiceLocator.Get<GameState>();

                var loadingTasks = new List<LoadingTask>
                {
                    new GenericTask("Initializing world...", () =>
                    {
                        gameState.InitializeWorld();
                    })
                };

                core.SetGameLoaded(true);

                var transitionOut = _transitionManager.GetRandomTransition();
                var transitionIn = _transitionManager.GetRandomTransition();
                _sceneManager.ChangeScene(GameSceneState.Split, transitionOut, transitionIn, 0f, loadingTasks);
            };
            _buttons.Add(playButton);
            _navigationGroup.Add(playButton);
            currentY += playHeight + buttonYSpacing;

            int settingsHeight = (int)settingsSize.Y + verticalPadding * 2;
            int settingsX = screenCenterX - (settingsWidth / 2);
            var settingsButton = new Button(
                new Rectangle(settingsX, (int)currentY, settingsWidth, settingsHeight),
                settingsText,
                font: secondaryFont,
                alignLeft: false
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                EnableTextWave = true,
                AlwaysAnimateText = true,
                WaveEffectType = TextEffectType.TypewriterPop,
                EnableHoverSway = false
            };
            settingsButton.OnClick += () =>
            {
                _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
                settingsButton.ResetAnimationState();
                _sceneManager.ShowModal(GameSceneState.Settings);
            };
            _buttons.Add(settingsButton);
            _navigationGroup.Add(settingsButton);
            currentY += settingsHeight + buttonYSpacing;

            int exitHeight = (int)exitSize.Y + verticalPadding * 2;
            int exitX = screenCenterX - (exitWidth / 2);
            var exitButton = new Button(
                new Rectangle(exitX, (int)currentY, exitWidth, exitHeight),
                exitText,
                font: secondaryFont,
                alignLeft: false
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                EnableTextWave = true,
                AlwaysAnimateText = true,
                WaveEffectType = TextEffectType.TypewriterPop,
                EnableHoverSway = false
            };
            exitButton.OnClick += ConfirmExit;
            _buttons.Add(exitButton);
            _navigationGroup.Add(exitButton);

            _uiInitialized = true;
        }

        private void ConfirmExit()
        {
            _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength);
            _confirmationDialog.Show(
                "Are you sure you want to exit?",
                new List<Tuple<string, Action>>
                {
                    Tuple.Create("YES", new Action(() => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); ServiceLocator.Get<Core>().ExitApplication(); })),
                    Tuple.Create("[chighlight]NO", new Action(() => { _hapticsManager.TriggerUICompoundShake(_global.ButtonHapticStrength); _confirmationDialog.Hide(); }))
                }
            );
        }

        public override void Enter()
        {
            base.Enter();
            InitializeUI();

            _currentInputDelay = _inputDelay;
            _previousKeyboardState = Keyboard.GetState();

            _buttonAnimators.Clear();
            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].ResetAnimationState();

                _buttons[i].WaveEffectType = TextEffectType.TypewriterPop;
                _buttons[i].AlwaysAnimateText = true;

                float textDuration = (_buttons[i].Text.Length * TextAnimationSettings.TypewriterDelay) + TextAnimationSettings.TypewriterDuration + 0.1f;

                var animator = new UIAnimator
                {
                    EntryStyle = EntryExitStyle.SwoopLeft,
                    ExitStyle = EntryExitStyle.Pop,
                    DurationIn = textDuration,
                    DurationOut = 0.5f
                };

                int index = i;
                animator.OnInComplete += () => {
                    _buttons[index].AlwaysAnimateText = false;
                    _buttons[index].WaveEffectType = TextEffectType.SmallWave;
                };

                animator.Show(delay: i * BUTTON_STAGGER_DELAY);
                _buttonAnimators.Add(animator);
            }

            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse && !firstTimeOpened)
            {
                _navigationGroup.SelectFirst();
            }
            else
            {
                _navigationGroup.DeselectAll();
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

            if (_transitionManager.IsTransitioning)
            {
                return;
            }

            foreach (var animator in _buttonAnimators)
            {
                animator.Update(dt);
            }

            // Use effective mouse state to disable hovering when using keyboard
            var currentMouseState = _inputManager.GetEffectiveMouseState();

            if (IsInputBlocked)
            {
                return;
            }

            if (_confirmationDialog.IsActive)
            {
                _confirmationDialog.Update(gameTime);
                return;
            }

            if (_currentInputDelay > 0)
            {
                _currentInputDelay -= dt;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttonAnimators[i].IsInteractive)
                {
                    _buttons[i].Update(currentMouseState);
                }
                else
                {
                    _buttons[i].IsHovered = false;
                }
            }

            if (_currentInputDelay <= 0)
            {
                if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                {
                    _navigationGroup.DeselectAll();
                }
                else
                {
                    if (_inputManager.NavigateUp) _navigationGroup.Navigate(-1);
                    if (_inputManager.NavigateDown) _navigationGroup.Navigate(1);
                    if (_inputManager.Confirm) _navigationGroup.SubmitCurrent();
                    if (_inputManager.Back) ConfirmExit();
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;

            spriteBatch.DrawSnapped(_spriteManager.LogoSprite, new Vector2(screenWidth / 2 - _spriteManager.LogoSprite.Width / 2, 25), Color.White);

            spriteBatch.End();

            for (int i = 0; i < _buttons.Count; i++)
            {
                var state = _buttonAnimators[i].GetVisualState();
                if (!state.IsVisible) continue;

                Vector2 center = _buttons[i].Bounds.Center.ToVector2();
                Matrix animMatrix = Matrix.CreateTranslation(-center.X, -center.Y, 0) *
                                    Matrix.CreateRotationZ(state.Rotation) *
                                    Matrix.CreateScale(state.Scale.X, state.Scale.Y, 1.0f) *
                                    Matrix.CreateTranslation(center.X, center.Y, 0) *
                                    Matrix.CreateTranslation(state.Offset.X, state.Offset.Y, 0);

                Matrix finalTransform = animMatrix * transform;

                spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: finalTransform);

                _buttons[i].Draw(spriteBatch, font, gameTime, Matrix.Identity);

                spriteBatch.End();
            }

            spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: transform);

            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];
                if (button.IsSelected || button.IsHovered || button.IsPressed)
                {
                    var state = _buttonAnimators[i].GetVisualState();
                    if (state.IsVisible && state.Scale.X >= 0.95f)
                    {
                        var bounds = button.Bounds;
                        var color = button.IsPressed ? _global.Palette_Fruit : _global.ButtonHoverColor;
                        var fontToUse = button.Font ?? secondaryFont;

                        string leftArrow = ">";
                        var arrowSize = fontToUse.MeasureString(leftArrow);

                        float pressOffset = button.IsPressed ? 2f : 0f;
                        float liftOffset = button.HoverAnimator.CurrentOffset;
                        var leftPos = new Vector2(bounds.Left - arrowSize.Width - 4 + pressOffset, bounds.Center.Y - arrowSize.Height / 2f + button.TextRenderOffset.Y + liftOffset);

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