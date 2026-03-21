using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Particles;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;

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
        private readonly TextureFactory _textureFactory;
        private readonly List<Button> _buttons = new();
        private readonly NavigationGroup _navigationGroup;
        private readonly Random _random = new Random();

        private float _inputDelay = 0.1f;
        private float _currentInputDelay = 0f;

        private ConfirmationDialog _confirmationDialog;
        private bool _uiInitialized = false;

        private const float BUTTON_STAGGER_DELAY = 0.15f;

        private float _logoWaveTimer1 = 0f;
        private float _logoWaveCooldown1 = 2f;
        private float _logoWaveTimer2 = 0f;
        private float _logoWaveCooldown2 = 3f;

        public MainMenuScene()
        {
            _sceneManager = ServiceLocator.Get<SceneManager>();
            _spriteManager = ServiceLocator.Get<SpriteManager>();
            _global = ServiceLocator.Get<Global>();
            _particleSystemManager = ServiceLocator.Get<ParticleSystemManager>();
            _transitionManager = ServiceLocator.Get<TransitionManager>();
            _hapticsManager = ServiceLocator.Get<HapticsManager>();
            _inputManager = ServiceLocator.Get<InputManager>();
            _textureFactory = ServiceLocator.Get<TextureFactory>();
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
            float currentY = 95f;
            int screenCenterX = Global.VIRTUAL_WIDTH / 2;

            string continueText = "CONTINUE";
            string newGameText = "NEW GAME";
            string settingsText = "SETTINGS";
            string exitText = "EXIT";

            // --- CONTINUE BUTTON ---
            Vector2 continueSize = secondaryFont.MeasureString(continueText);
            int continueWidth = (int)continueSize.X + horizontalPadding * 2;
            int continueHeight = (int)continueSize.Y + verticalPadding * 2;
            int continueX = screenCenterX - (continueWidth / 2);

            var continueButton = new Button(
                new Rectangle(continueX, (int)currentY, continueWidth, continueHeight),
                continueText,
                font: secondaryFont,
                alignLeft: false
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                EnableTextWave = false,
                AlwaysAnimateText = false,
                EnableHoverSway = false,
                IsEnabled = false, // Disabled for now
                UseTextOutline = true,
                TextOutlineColor = _global.Palette_Off
            };
            _buttons.Add(continueButton);
            // Note: Not adding disabled button to navigation group
            currentY += continueHeight + buttonYSpacing;

            // NEW GAME BUTTON
            Vector2 newGameSize = secondaryFont.MeasureString(newGameText);
            int newGameWidth = (int)newGameSize.X + horizontalPadding * 2;
            int newGameHeight = (int)newGameSize.Y + verticalPadding * 2;
            int newGameX = screenCenterX - (newGameWidth / 2);

            var newGameButton = new Button(
                new Rectangle(newGameX, (int)currentY, newGameWidth, newGameHeight),
                newGameText,
                font: secondaryFont,
                alignLeft: false
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                EnableTextWave = true,
                AlwaysAnimateText = true,
                WaveEffectType = TextEffectType.TypewriterPop,
                EnableHoverSway = false,
                UseTextOutline = true,
                TextOutlineColor = _global.Palette_Off
            };
            newGameButton.OnClick += () =>
            {
                _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                newGameButton.ResetAnimationState();
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().StopMusic(1.5f);
                _sceneManager.ChangeScene(GameSceneState.NewGameIntro, _transitionManager.GetRandomTransition(), _transitionManager.GetRandomTransition());
            };
            _buttons.Add(newGameButton);
            _navigationGroup.Add(newGameButton);
            currentY += newGameHeight + buttonYSpacing;

            // --- CATYCLOPAEDIA BUTTON ---
            string catyText = "CATYCLOPAEDIA";
            Vector2 catySize = secondaryFont.MeasureString(catyText);
            int catyWidth = (int)catySize.X + horizontalPadding * 2;
            int catyHeight = (int)catySize.Y + verticalPadding * 2;
            int catyX = screenCenterX - (catyWidth / 2);

            var catyButton = new Button(
                new Rectangle(catyX, (int)currentY, catyWidth, catyHeight),
                catyText,
                font: secondaryFont,
                alignLeft: false
            )
            {
                TextRenderOffset = new Vector2(0, -1),
                EnableTextWave = true,
                AlwaysAnimateText = true,
                WaveEffectType = TextEffectType.TypewriterPop,
                EnableHoverSway = false,
                UseTextOutline = true,
                TextOutlineColor = _global.Palette_Off
            };
            catyButton.OnClick += () =>
            {
                _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                catyButton.ResetAnimationState();
                _sceneManager.ChangeScene(GameSceneState.Catyclopaedia, _transitionManager.GetRandomTransition(), _transitionManager.GetRandomTransition());
            };
            _buttons.Add(catyButton);
            _navigationGroup.Add(catyButton);
            currentY += catyHeight + buttonYSpacing;

            // --- SETTINGS BUTTON ---
            Vector2 settingsSize = secondaryFont.MeasureString(settingsText);
            int settingsWidth = (int)settingsSize.X + horizontalPadding * 2;
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
                EnableHoverSway = false,
                UseTextOutline = true,
                TextOutlineColor = _global.Palette_Off
            };
            settingsButton.OnClick += () =>
            {
                _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                settingsButton.ResetAnimationState();
                _sceneManager.ShowModal(GameSceneState.Settings);
            };
            _buttons.Add(settingsButton);
            _navigationGroup.Add(settingsButton);
            currentY += settingsHeight + buttonYSpacing;

            // --- EXIT BUTTON ---
            Vector2 exitSize = secondaryFont.MeasureString(exitText);
            int exitWidth = (int)exitSize.X + horizontalPadding * 2;
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
                EnableHoverSway = false,
                UseTextOutline = true,
                TextOutlineColor = _global.Palette_Off
            };
            exitButton.OnClick += ConfirmExit;
            _buttons.Add(exitButton);
            _navigationGroup.Add(exitButton);

            _uiInitialized = true;
        }

        private void ConfirmExit()
        {
            _hapticsManager.TriggerZoomPulse(_global.HapticZoomPulseStrength, _global.HapticZoomPulseDuration);
            _confirmationDialog.Show(
                "Are you sure you want to exit?",
                new List<Tuple<string, Action>>
                {
                Tuple.Create("YES", new Action(() => { _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration); ServiceLocator.Get<Core>().ExitApplication(); })),
                Tuple.Create("[chighlight]NO", new Action(() => { _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration); _confirmationDialog.Hide(); }))
                }
            );
        }

        public override void Enter()
        {
            base.Enter();
            InitializeUI();
            ServiceLocator.Get<GeometricBackgroundManager>().Show(1.0f);

            var audio = ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>();
            audio.PlayMusic("music_main_menu", 2.0f);
            audio.SetMusicStemVolume("music_main_menu", 0, 1.0f);
            audio.SetMusicStemVolume("music_main_menu", 1, 0.0f);

            _currentInputDelay = _inputDelay;
            _previousKeyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].ResetAnimationState();
                _buttons[i].WaveEffectType = TextEffectType.SmallWave;
                _buttons[i].AlwaysAnimateText = false;
                _buttons[i].PlayEntrance(delay: i * BUTTON_STAGGER_DELAY);
            }

            if (_inputManager.CurrentInputDevice != InputDeviceType.Mouse && !firstTimeOpened)
            {
                _navigationGroup.SelectFirst();
            }
            else
            {
                _navigationGroup.DeselectAll();
            }

            _logoWaveTimer1 = 0f;
            _logoWaveCooldown1 = 2f + (float)_random.NextDouble() * 3f;
            _logoWaveTimer2 = 0f;
            _logoWaveCooldown2 = 2f + (float)_random.NextDouble() * 3f;

            firstTimeOpened = false;
        }

        public override void Exit()
        {
            base.Exit();
            ServiceLocator.Get<GeometricBackgroundManager>().Hide();
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
            // Return the first enabled button
            foreach (var button in _buttons)
            {
                if (button.IsEnabled) return button.Bounds;
            }
            return null;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _logoWaveTimer1 += dt;
            if (_logoWaveTimer1 > _logoWaveCooldown1 + 1.5f)
            {
                _logoWaveTimer1 = 0f;
                _logoWaveCooldown1 = 2f + (float)_random.NextDouble() * 4f;
            }

            _logoWaveTimer2 += dt;
            if (_logoWaveTimer2 > _logoWaveCooldown2 + 1.5f)
            {
                _logoWaveTimer2 = 0f;
                _logoWaveCooldown2 = 2f + (float)_random.NextDouble() * 4f;
            }

            if (_transitionManager.IsTransitioning)
            {
                return;
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
                _buttons[i].Update(currentMouseState);
            }

            if (_currentInputDelay <= 0)
            {
                if (_inputManager.CurrentInputDevice == InputDeviceType.Mouse)
                {
                    _navigationGroup.DeselectAll();
                }
                else
                {
                    _navigationGroup.UpdateInput(_inputManager);
                    if (_inputManager.Back) ConfirmExit();
                }
            }
        }

        protected override void DrawSceneContent(SpriteBatch spriteBatch, BitmapFont font, GameTime gameTime, Matrix transform)
        {
            int screenWidth = Global.VIRTUAL_WIDTH;
            var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
            var pixel = ServiceLocator.Get<Texture2D>();

            spriteBatch.Draw(pixel, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), _global.GameBg);

            spriteBatch.End();

            _particleSystemManager.Draw(spriteBatch, transform, 0); // Background
            _particleSystemManager.Draw(spriteBatch, transform, 1); // Foreground

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

            if (_spriteManager.TitleLogoSpriteSheet != null)
            {
                float time = (float)gameTime.TotalGameTime.TotalSeconds;
                float scale = 1.0f;
                int frameSize = 40;
                float scaledFrameSize = frameSize * scale;
                Vector2 origin = new Vector2(frameSize / 2f, frameSize / 2f);

                int[] rowLengths = { 3, 6, 6, 3 };
                int[] centerToCenterSpacings = { 16, 24, 16, 24 };
                float[] rowShifts = { -25f, 0f, 12f, 46f };
                float rowVerticalSpacing = scaledFrameSize * 0.55f;
                float baseY = 0f;

                for (int row = 0; row < 4; row++)
                {
                    int wordLength = rowLengths[row];
                    int spacing = centerToCenterSpacings[row];

                    float rowWidthCenterToCenter = (wordLength - 1) * spacing;
                    float startCenterX = (screenWidth - rowWidthCenterToCenter) / 2f + rowShifts[row];
                    float centerY = baseY + (row * rowVerticalSpacing) + (scaledFrameSize / 2f);

                    for (int col = 0; col < wordLength; col++)
                    {
                        float swayX = 0f;
                        float swayY = 0f;

                        if (row == 1 || row == 3)
                        {
                            float timer = (row == 1) ? _logoWaveTimer1 : _logoWaveTimer2;
                            float cooldown = (row == 1) ? _logoWaveCooldown1 : _logoWaveCooldown2;

                            if (timer > cooldown)
                            {
                                float charWaveTime = (timer - cooldown) - (col * 0.1f);
                                if (charWaveTime > 0 && charWaveTime < 0.15f)
                                {
                                    swayY = -1f;
                                }
                            }
                        }
                        else
                        {
                            float phase = (row * 7.3f) + (col * 3.1f);
                            swayX = MathF.Sin(time * 0.65f + phase) * 1.4f;
                            swayY = MathF.Cos(time * 0.85f + phase) * 1.4f;
                        }

                        float finalX = MathF.Round(startCenterX + (col * spacing) + swayX);
                        float finalY = MathF.Round(centerY + swayY);
                        Vector2 pos = new Vector2(finalX, finalY);

                        Rectangle sourceRect = new Rectangle(col * frameSize, row * frameSize, frameSize, frameSize);

                        spriteBatch.DrawSnapped(_spriteManager.TitleLogoSpriteSheet, pos, sourceRect, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            else
            {
                spriteBatch.DrawSnapped(_spriteManager.LogoSprite, new Vector2(screenWidth / 2 - _spriteManager.LogoSprite.Width / 2, 25), Color.White);
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                var button = _buttons[i];

                button.Draw(spriteBatch, font, gameTime, transform);

                if (button.IsSelected || button.IsHovered || button.IsPressed)
                {
                    if (button.Plink.IsActive && button.Plink.Scale < 0.95f) continue;

                    var bounds = button.Bounds;
                    var color = button.IsPressed ? _global.Palette_Fruit : _global.ButtonHoverColor;
                    var fontToUse = button.Font ?? secondaryFont;

                    string leftArrow = ">";
                    var arrowSize = fontToUse.MeasureString(leftArrow);

                    float pressOffset = button.IsPressed ? 2f : 0f;
                    float liftOffset = button.HoverAnimator.CurrentOffset;
                    var leftPos = new Vector2(bounds.Left - arrowSize.Width - 4 + pressOffset, bounds.Center.Y - arrowSize.Height / 2f + button.TextRenderOffset.Y + liftOffset);

                    spriteBatch.DrawStringOutlinedSnapped(fontToUse, leftArrow, leftPos, color, _global.Palette_Off);
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