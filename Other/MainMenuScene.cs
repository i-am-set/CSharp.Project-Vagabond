#nullable enable
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
        private const float LOGO_SHADOW_OPACITY = 0.75f;

        private float _logoWaveTimer1 = 0f;
        private float _logoWaveCooldown1 = 2f;
        private float _logoWaveTimer2 = 0f;
        private float _logoWaveCooldown2 = 3f;

        private BasicEffect _logoEffect;
        private VertexPositionColorTexture[] _logoVertices;
        private VertexPositionColorTexture[] _shadowVertices;
        private short[] _logoIndices;

        // Physics state for the interactive logo letters
        private Vector2[] _logoLetterOffsets = new Vector2[18];
        private Vector2[] _logoLetterVelocities = new Vector2[18];
        private bool[] _logoLetterDisplaced = new bool[18];

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

            var gd = ServiceLocator.Get<GraphicsDevice>();
            _logoEffect = new BasicEffect(gd)
            {
                TextureEnabled = true,
                VertexColorEnabled = true
            };

            _logoVertices = new VertexPositionColorTexture[72];
            _shadowVertices = new VertexPositionColorTexture[72];
            _logoIndices = new short[108];

            for (int i = 0; i < 18; i++)
            {
                _logoIndices[i * 6 + 0] = (short)(i * 4 + 0);
                _logoIndices[i * 6 + 1] = (short)(i * 4 + 1);
                _logoIndices[i * 6 + 2] = (short)(i * 4 + 2);
                _logoIndices[i * 6 + 3] = (short)(i * 4 + 1);
                _logoIndices[i * 6 + 4] = (short)(i * 4 + 3);
                _logoIndices[i * 6 + 5] = (short)(i * 4 + 2);
            }
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
            float currentY = 105f;
            int buttonX = Global.VIRTUAL_WIDTH / 2 - 130;

            string arenaText = "START MATCH";
            string settingsText = "SETTINGS";
            string exitText = "EXIT";

            Vector2 arenaSize = secondaryFont.MeasureString(arenaText);
            int arenaWidth = (int)arenaSize.X + horizontalPadding * 2;
            int arenaHeight = (int)arenaSize.Y + verticalPadding * 2;

            var arenaButton = new Button(
                new Rectangle(buttonX, (int)currentY, arenaWidth, arenaHeight),
                arenaText,
                font: secondaryFont,
                alignLeft: true
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
            arenaButton.OnClick += () =>
            {
                _hapticsManager.TriggerZoomPulse(_global.LightHapticZoomPulseStrength, _global.HapticZoomPulseDuration);
                arenaButton.ResetAnimationState();
                ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().StopMusic(1.5f);
                _sceneManager.ChangeScene(GameSceneState.NewGameIntro, _transitionManager.GetRandomTransition(), _transitionManager.GetRandomTransition());
            };
            _buttons.Add(arenaButton);
            _navigationGroup.Add(arenaButton);

            currentY += arenaHeight + buttonYSpacing + 12;

            string catyText = "CATYLOPEDIA";
            Vector2 catySize = secondaryFont.MeasureString(catyText);
            int catyWidth = (int)catySize.X + horizontalPadding * 2;
            int catyHeight = (int)catySize.Y + verticalPadding * 2;

            var catyButton = new Button(
                new Rectangle(buttonX, (int)currentY, catyWidth, catyHeight),
                catyText,
                font: secondaryFont,
                alignLeft: true
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
                _sceneManager.ChangeScene(GameSceneState.Catyclopedia, _transitionManager.GetRandomTransition(), _transitionManager.GetRandomTransition());
            };
            _buttons.Add(catyButton);
            _navigationGroup.Add(catyButton);
            currentY += catyHeight + buttonYSpacing;

            Vector2 settingsSize = secondaryFont.MeasureString(settingsText);
            int settingsWidth = (int)settingsSize.X + horizontalPadding * 2;
            int settingsHeight = (int)settingsSize.Y + verticalPadding * 2;

            var settingsButton = new Button(
                new Rectangle(buttonX, (int)currentY, settingsWidth, settingsHeight),
                settingsText,
                font: secondaryFont,
                alignLeft: true
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

            Vector2 exitSize = secondaryFont.MeasureString(exitText);
            int exitWidth = (int)exitSize.X + horizontalPadding * 2;
            int exitHeight = (int)exitSize.Y + verticalPadding * 2;

            var exitButton = new Button(
                new Rectangle(buttonX, (int)currentY, exitWidth, exitHeight),
                exitText,
                font: secondaryFont,
                alignLeft: true
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
            audio.PlayMusic("music_main_menu_pt1", 1.0f);
            audio.SetCurrentMusicStemVolume(0, 1.0f);
            audio.SetCurrentMusicStemVolume(1, 0.0f);

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

            for (int i = 0; i < 18; i++)
            {
                _logoLetterOffsets[i] = Vector2.Zero;
                _logoLetterVelocities[i] = Vector2.Zero;
                _logoLetterDisplaced[i] = false;
            }

            firstTimeOpened = false;
        }

        public override void Exit()
        {
            base.Exit();
            ServiceLocator.Get<GeometricBackgroundManager>().Hide();
        }

        protected override Rectangle? GetFirstSelectableElementBounds()
        {
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

            _particleSystemManager.Draw(spriteBatch, transform, 0);
            _particleSystemManager.Draw(spriteBatch, transform, 1);

            if (_spriteManager.TitleLogoSpriteSheet != null)
            {
                Draw3DLogo(gameTime);
            }
            else
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);
                spriteBatch.DrawSnapped(_spriteManager.LogoSprite, new Vector2(screenWidth / 2 - _spriteManager.LogoSprite.Width / 2, 25), Color.White);
                spriteBatch.End();
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, transform);

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

        private void Draw3DLogo(GameTime gameTime)
        {
            var gd = ServiceLocator.Get<GraphicsDevice>();
            var core = ServiceLocator.Get<Core>();

            Viewport oldViewport = gd.Viewport;
            gd.Viewport = new Viewport(core.FinalRenderRectangle);

            float fov = MathHelper.PiOver4;
            float cameraZ = (Global.VIRTUAL_HEIGHT / 2f) / MathF.Tan(fov / 2f);

            // Negating cameraZ fixes the horizontal mirroring by aligning the 3D view matrix with 2D screen space
            _logoEffect.View = Matrix.CreateLookAt(
                new Vector3(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f, -cameraZ),
                new Vector3(Global.VIRTUAL_WIDTH / 2f, Global.VIRTUAL_HEIGHT / 2f, 0),
                Vector3.Down);

            _logoEffect.Projection = Matrix.CreatePerspectiveFieldOfView(fov, (float)Global.VIRTUAL_WIDTH / Global.VIRTUAL_HEIGHT, 1f, 1000f);
            _logoEffect.World = Matrix.Identity;

            Texture2D tex = _spriteManager.TitleLogoSpriteSheet;
            _logoEffect.Texture = tex;

            int vertIndex = 0;
            float time = (float)gameTime.TotalGameTime.TotalSeconds;

            // Clamp dt to prevent physics explosions during lag spikes
            float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 0.05f);
            Vector2 mousePos = Core.TransformMouse(_inputManager.GetEffectiveMouseState().Position);

            int frameSize = 40;
            float halfSize = frameSize / 2f;

            int[] rowLengths = { 3, 6, 6, 3 };
            int[] centerToCenterSpacings = { 16, 24, 16, 24 };
            float[] rowShifts = { -25f, 0f, 10f, 26f };
            float rowVerticalSpacing = frameSize * 0.60f;
            float baseY = 10f;
            float baseX = 55f;

            int letterIndex = 0;
            Color shadowColor = _global.Palette_Off * LOGO_SHADOW_OPACITY;

            for (int row = 0; row < 4; row++)
            {
                int wordLength = rowLengths[row];
                int spacing = centerToCenterSpacings[row];

                float rowWidthCenterToCenter = (wordLength - 1) * spacing;
                float startCenterX = (Global.VIRTUAL_WIDTH - rowWidthCenterToCenter) / 2f + rowShifts[row] + baseX;
                float centerY = baseY + (row * rowVerticalSpacing) + halfSize;

                for (int col = 0; col < wordLength; col++)
                {
                    float swayX = 0f;
                    float swayY = 0f;
                    float pitch = 0f;
                    float yaw = 0f;

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
                                pitch = -0.2f;
                            }
                        }
                    }
                    else
                    {
                        float phase = (row * 7.3f) + (col * 3.1f);
                        swayX = MathF.Sin(time * 0.65f + phase) * 1.4f;
                        swayY = MathF.Cos(time * 0.85f + phase) * 1.4f;
                    }

                    // --- Interactive Physics ---
                    Vector2 basePos = new Vector2(startCenterX + (col * spacing), centerY);
                    Vector2 currentPos = basePos + _logoLetterOffsets[letterIndex];

                    Vector2 toMouse = currentPos - mousePos;
                    float dist = toMouse.Length();
                    float repelRadius = 35f;

                    // Apply repulsion force if mouse is close
                    if (dist < repelRadius && dist > 0.001f)
                    {
                        float force = (repelRadius - dist) / repelRadius;
                        Vector2 repelDir = toMouse / dist;
                        _logoLetterVelocities[letterIndex] += repelDir * force * 2500f * dt;
                    }

                    // Apply spring force to pull back to center (0,0 offset)
                    Vector2 springForce = -_logoLetterOffsets[letterIndex] * 150f;

                    // Apply damping to prevent infinite bouncing
                    Vector2 dampingForce = -_logoLetterVelocities[letterIndex] * 12f;

                    // Integrate velocity and position
                    _logoLetterVelocities[letterIndex] += (springForce + dampingForce) * dt;
                    _logoLetterOffsets[letterIndex] += _logoLetterVelocities[letterIndex] * dt;

                    float offsetSq = _logoLetterOffsets[letterIndex].LengthSquared();
                    if (offsetSq > 9f)
                    {
                        if (!_logoLetterDisplaced[letterIndex])
                        {
                            _logoLetterDisplaced[letterIndex] = true;
                            ServiceLocator.Get<ProjectVagabond.Audio.AudioManager>().PlayUi("ui_hover");
                        }
                    }
                    else if (offsetSq < 0.25f)
                    {
                        _logoLetterDisplaced[letterIndex] = false;
                    }

                    // Calculate final snapped position
                    float finalX = MathF.Round(basePos.X + swayX + _logoLetterOffsets[letterIndex].X);
                    float finalY = MathF.Round(basePos.Y + swayY + _logoLetterOffsets[letterIndex].Y);

                    Matrix rot = Matrix.CreateRotationX(pitch) * Matrix.CreateRotationY(yaw);

                    float uMin = (col * frameSize) / (float)tex.Width;
                    float uMax = ((col + 1) * frameSize) / (float)tex.Width;
                    float vMin = (row * frameSize) / (float)tex.Height;
                    float vMax = ((row + 1) * frameSize) / (float)tex.Height;

                    Vector3[] corners = new Vector3[4];
                    corners[0] = new Vector3(-halfSize, -halfSize, 0);
                    corners[1] = new Vector3(halfSize, -halfSize, 0);
                    corners[2] = new Vector3(-halfSize, halfSize, 0);
                    corners[3] = new Vector3(halfSize, halfSize, 0);

                    Vector2[] uvs = new Vector2[4];
                    uvs[0] = new Vector2(uMin, vMin);
                    uvs[1] = new Vector2(uMax, vMin);
                    uvs[2] = new Vector2(uMin, vMax);
                    uvs[3] = new Vector2(uMax, vMax);

                    for (int v = 0; v < 4; v++)
                    {
                        Vector3 transformed = Vector3.Transform(corners[v], rot);

                        // Adjusted divisor to keep shading visible with the smaller rotation magnitude
                        float depthFactor = Math.Clamp(1.0f - (transformed.Z / 12f), 0.4f, 1.0f);
                        Color vertColor = new Color(depthFactor, depthFactor, depthFactor, 1.0f);

                        _shadowVertices[vertIndex + v] = new VertexPositionColorTexture(
                            new Vector3(finalX + transformed.X + 2, finalY + transformed.Y + 2, transformed.Z),
                            shadowColor,
                            uvs[v]
                        );

                        _logoVertices[vertIndex + v] = new VertexPositionColorTexture(
                            new Vector3(finalX + transformed.X, finalY + transformed.Y, transformed.Z),
                            vertColor,
                            uvs[v]
                        );
                    }
                    vertIndex += 4;
                    letterIndex++;
                }
            }

            gd.BlendState = BlendState.AlphaBlend;
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.PointClamp;

            int[] rowStartIndex = { 0, 18, 54, 90 };
            int[] rowPrimitiveCount = { 6, 12, 12, 6 };

            foreach (EffectPass pass in _logoEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                for (int row = 0; row < 4; row++)
                {
                    // Draw shadows first for this row
                    gd.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _shadowVertices, 0, 72,
                        _logoIndices, rowStartIndex[row], rowPrimitiveCount[row]
                    );

                    // Draw main logo for this row
                    gd.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _logoVertices, 0, 72,
                        _logoIndices, rowStartIndex[row], rowPrimitiveCount[row]
                    );
                }
            }

            gd.Viewport = oldViewport;
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