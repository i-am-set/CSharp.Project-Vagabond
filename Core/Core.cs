using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Graphics;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Battle.Abilities;
using ProjectVagabond.Battle.UI;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.Systems;
using ProjectVagabond.Transitions;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProjectVagabond
{
    public class Core : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private BitmapFont _defaultFont;
        private BitmapFont _secondaryFont;
        private BitmapFont _tertiaryFont;
        private Texture2D _pixel;

        private Rectangle _finalRenderRectangle;
        private Matrix _mouseTransformMatrix;
        private Point _previousResolution;
        private float _finalScale = 1f;

        private RenderTarget2D _transitionRenderTarget;
        private RenderTarget2D _finalCompositeTarget;
        private Effect _crtEffect;
        private BlendState _cursorInvertBlendState;

        private float _flashTimer;
        private float _flashDuration;
        private Color _flashColor;
        private float _glitchTimer;
        private float _glitchDuration;

        private class ScreenFlashState
        {
            public float Timer;
            public int FlashesRemaining;
            public bool IsCurrentlyWhite;
            public Color FlashColor;

            public const int TOTAL_FLASHES = 2;
            public const float FLASH_ON_DURATION = 0.05f;
            public const float FLASH_OFF_DURATION = 0.05f;
            public const float TOTAL_FLASH_CYCLE_DURATION = FLASH_ON_DURATION + FLASH_OFF_DURATION;
        }
        private ScreenFlashState _screenFlashState;

        private readonly List<Action<SpriteBatch, Matrix>> _fullscreenOverlays = new List<Action<SpriteBatch, Matrix>>();

        public Matrix MouseTransformMatrix => _mouseTransformMatrix;
        public BitmapFont DefaultFont => _defaultFont;
        public BitmapFont SecondaryFont => _secondaryFont;
        public BitmapFont TertiaryFont => _tertiaryFont;
        public float FinalScale => _finalScale;

        private Global _global;
        private GameSettings _settings;
        private SceneManager _sceneManager;
        private HapticsManager _hapticsManager;
        private TooltipManager _tooltipManager;
        private ParticleSystemManager _particleSystemManager;
        private GameState _gameState;

        private MoveAcquisitionSystem _moveAcquisitionSystem;
        private RelicAcquisitionSystem _relicAcquisitionSystem;

        private SpriteManager _spriteManager;
        private LoadingScreen _loadingScreen;
        private DebugConsole _debugConsole;
        private ProgressionManager _progressionManager;
        private CursorManager _cursorManager;
        private TransitionManager _transitionManager;
        private HitstopManager _hitstopManager;
        private BackgroundNoiseRenderer _backgroundNoiseRenderer;
        private LootManager _lootManager;
        private ItemTooltipRenderer _itemTooltipRenderer;

        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private bool _drawMouseDebugDot = false;
        private bool _isGameLoaded = false;
        private float _customResolutionSaveTimer = 0f;
        private bool _isCustomResolutionSavePending = false;
        private readonly Random _random = new Random();
        private int _startupMaximizeCheckFrames = 0;
        private Stopwatch _frameStopwatch;
        private TimeSpan _targetElapsedTimeSpan;

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_MaximizeWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_MAXIMIZE = 3;

        private void SetHighPrecisionTimer()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try { TimeBeginPeriod(1); } catch { }
            }
        }

        private void MaximizeWindow()
        {
            try { SDL_MaximizeWindow(Window.Handle); return; } catch { }
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try { ShowWindow(Window.Handle, SW_MAXIMIZE); } catch (Exception ex) { Debug.WriteLine($"[Core] Failed to maximize window via User32: {ex.Message}"); }
            }
        }

        public Core()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    GameLogger.Log(LogSeverity.Critical, $"UNHANDLED EXCEPTION: {ex.Message}");
                    GameLogger.Log(LogSeverity.Critical, ex.StackTrace);
                    GameLogger.Close();
                }
            };

            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = false;
            _graphics.GraphicsProfile = GraphicsProfile.HiDef;
            _graphics.PreferredBackBufferWidth = Global.VIRTUAL_WIDTH;
            _graphics.PreferredBackBufferHeight = Global.VIRTUAL_HEIGHT;
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnResize;
            IsFixedTimeStep = false;
            _graphics.SynchronizeWithVerticalRetrace = false;
        }

        protected override void Initialize()
        {
            ConsoleRedirection.Initialize();
            SetHighPrecisionTimer();
            _frameStopwatch = new Stopwatch();
            _frameStopwatch.Start();

            ServiceLocator.Register<Core>(this);
            ServiceLocator.Register<GraphicsDeviceManager>(_graphics);
            ServiceLocator.Register<GameWindow>(Window);
            ServiceLocator.Register<Global>(Global.Instance);
            _settings = SettingsManager.LoadSettings();
            ServiceLocator.Register<GameSettings>(_settings);
            _global = ServiceLocator.Get<Global>();
            ServiceLocator.Register<GraphicsDevice>(GraphicsDevice);

            // Removed ECS Managers (EntityManager, ComponentStore, ArchetypeManager)

            var dataManager = new DataManager();
            ServiceLocator.Register<DataManager>(dataManager);

            _loadingScreen = new LoadingScreen();
            ServiceLocator.Register<LoadingScreen>(_loadingScreen);
            var noiseManager = new NoiseMapManager();
            ServiceLocator.Register<NoiseMapManager>(noiseManager);
            var textureFactory = new TextureFactory();
            ServiceLocator.Register<TextureFactory>(textureFactory);
            _spriteManager = new SpriteManager();
            ServiceLocator.Register<SpriteManager>(_spriteManager);
            _hapticsManager = new HapticsManager();
            ServiceLocator.Register<HapticsManager>(_hapticsManager);
            _tooltipManager = new TooltipManager();
            ServiceLocator.Register<TooltipManager>(_tooltipManager);
            _particleSystemManager = new ParticleSystemManager();
            ServiceLocator.Register<ParticleSystemManager>(_particleSystemManager);

            // Updated GameState constructor (removed ComponentStore)
            _gameState = new GameState(noiseManager, _global, _spriteManager);
            ServiceLocator.Register<GameState>(_gameState);

            _moveAcquisitionSystem = new MoveAcquisitionSystem();
            _relicAcquisitionSystem = new RelicAcquisitionSystem();
            var terminalRenderer = new TerminalRenderer();
            ServiceLocator.Register<TerminalRenderer>(terminalRenderer);
            var autoCompleteManager = new AutoCompleteManager();
            ServiceLocator.Register<AutoCompleteManager>(autoCompleteManager);
            var commandProcessor = new CommandProcessor();
            ServiceLocator.Register<CommandProcessor>(commandProcessor);
            _progressionManager = new ProgressionManager();
            ServiceLocator.Register<ProgressionManager>(_progressionManager);
            _transitionManager = new TransitionManager();
            ServiceLocator.Register<TransitionManager>(_transitionManager);
            _sceneManager = new SceneManager();
            ServiceLocator.Register<SceneManager>(_sceneManager);
            _sceneManager.AddScene(GameSceneState.Transition, new TransitionScene());
            _debugConsole = new DebugConsole();
            ServiceLocator.Register<DebugConsole>(_debugConsole);
            _cursorManager = new CursorManager();
            ServiceLocator.Register<CursorManager>(_cursorManager);
            _hitstopManager = new HitstopManager();
            ServiceLocator.Register<HitstopManager>(_hitstopManager);
            _backgroundNoiseRenderer = new BackgroundNoiseRenderer();
            ServiceLocator.Register<BackgroundNoiseRenderer>(_backgroundNoiseRenderer);
            _itemTooltipRenderer = new ItemTooltipRenderer();
            ServiceLocator.Register<ItemTooltipRenderer>(_itemTooltipRenderer);

            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            _settings.ApplyGraphicsSettings(_graphics, this);
            _settings.ApplyGameSettings();

            if (_settings.Mode == WindowMode.Windowed)
            {
                var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                float widthRatio = (float)_settings.Resolution.X / displayMode.Width;
                float heightRatio = (float)_settings.Resolution.Y / displayMode.Height;
                if (widthRatio >= 0.8f || heightRatio >= 0.8f)
                {
                    _settings.Resolution = new Point(displayMode.Width, displayMode.Height);
                    _settings.ApplyGraphicsSettings(_graphics, this);
                    MaximizeWindow();
                    _startupMaximizeCheckFrames = 15;
                }
            }

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            ServiceLocator.Register<Texture2D>(_pixel);

            _sceneManager.AddScene(GameSceneState.Startup, new StartupScene());
            _sceneManager.AddScene(GameSceneState.MainMenu, new MainMenuScene());
            _sceneManager.AddScene(GameSceneState.Settings, new SettingsScene());
            _sceneManager.AddScene(GameSceneState.Battle, new BattleScene());
            _sceneManager.AddScene(GameSceneState.Split, new SplitMapScene());
            _sceneManager.AddScene(GameSceneState.GameOver, new GameOverScene());

            _previousResolution = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
            OnResize(null, null);

            _finalCompositeTarget = new RenderTarget2D(
                GraphicsDevice,
                Window.ClientBounds.Width,
                Window.ClientBounds.Height,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24,
                0,
                RenderTargetUsage.PreserveContents);

            _cursorInvertBlendState = new BlendState
            {
                ColorBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.InverseDestinationColor,
                ColorDestinationBlend = Blend.InverseSourceAlpha,
                AlphaBlendFunction = BlendFunction.Add,
                AlphaSourceBlend = Blend.Zero,
                AlphaDestinationBlend = Blend.One
            };

            _previousMouseState = Mouse.GetState();
            base.Initialize();
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            GameLogger.Close();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try { TimeEndPeriod(1); } catch { }
            }
            base.OnExiting(sender, args);
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            ServiceLocator.Register<SpriteBatch>(_spriteBatch);

            try { _crtEffect = Content.Load<Effect>("Shaders/CRTShader"); }
            catch (Exception ex) { Debug.WriteLine($"[FATAL ERROR] Could not load CRT Shader: {ex.Message}"); _crtEffect = null; }

            try { _defaultFont = Content.Load<BitmapFont>("Fonts/Px437_IBM_BIOS"); ServiceLocator.Register<BitmapFont>(_defaultFont); }
            catch { throw new Exception("Please add a BitmapFont to your 'Content/Fonts' folder"); }

            try { _secondaryFont = Content.Load<BitmapFont>("Fonts/5x5_pixel"); }
            catch { _secondaryFont = _defaultFont; }

            try { _tertiaryFont = Content.Load<BitmapFont>("Fonts/3x4_SimpleOddHeight"); }
            catch { _tertiaryFont = _secondaryFont; }

            _spriteManager.LoadEssentialContent();
            _spriteManager.LoadGameContent();
            _backgroundNoiseRenderer.LoadContent();
            BattleDataCache.LoadData(Content);
            _progressionManager.LoadSplits();

            var dataManager = ServiceLocator.Get<DataManager>();
            dataManager.LoadData(Content.RootDirectory);

            _lootManager = new LootManager();
            _lootManager.BuildLootTables();
            ServiceLocator.Register<LootManager>(_lootManager);

            _sceneManager.ChangeScene(GameSceneState.Startup, TransitionType.None, TransitionType.None);
        }

        public void SetGameLoaded(bool isLoaded) => _isGameLoaded = isLoaded;
        public void TriggerFullscreenFlash(Color color, float duration) { _flashColor = color; _flashDuration = duration; _flashTimer = duration; }
        public void TriggerScreenFlashSequence(Color color) { _screenFlashState = new ScreenFlashState { Timer = 0f, FlashesRemaining = ScreenFlashState.TOTAL_FLASHES, IsCurrentlyWhite = true, FlashColor = color }; }
        public void TriggerFullscreenGlitch(float duration) { _glitchDuration = duration; _glitchTimer = duration; }
        public void RequestFullscreenOverlay(Action<SpriteBatch, Matrix> drawAction) { _fullscreenOverlays.Add(drawAction); }

        public void ResetGame()
        {
            _particleSystemManager.ClearAllEmitters();
            _hapticsManager.StopAll();
            _tooltipManager.Hide();
            _loadingScreen.Clear();
            _debugConsole.ClearHistory();
            _progressionManager.ClearCurrentSplitMap();
            _hitstopManager.Reset();
            _transitionManager.Reset();

            _gameState.Reset();
        }

        protected override void Update(GameTime gameTime)
        {
            _fullscreenOverlays.Clear();

            if (_settings.IsFrameLimiterEnabled)
            {
                _targetElapsedTimeSpan = TimeSpan.FromSeconds(1.0 / _settings.TargetFramerate);
                while (_frameStopwatch.Elapsed < _targetElapsedTimeSpan)
                {
                    var remaining = _targetElapsedTimeSpan - _frameStopwatch.Elapsed;
                    if (remaining.TotalMilliseconds > 1) Thread.Sleep(1);
                }
            }
            _frameStopwatch.Restart();

            if (_startupMaximizeCheckFrames > 0)
            {
                _startupMaximizeCheckFrames--;
                if (_startupMaximizeCheckFrames == 0)
                {
                    var currentClientBounds = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
                    if (_settings.Resolution != currentClientBounds)
                    {
                        _settings.Resolution = currentClientBounds;
                        SettingsManager.SaveSettings(_settings);
                        OnResize(null, null);
                    }
                }
            }

            IsMouseVisible = _debugConsole.IsVisible;
            if (!IsActive) return;

            if (_flashTimer > 0) _flashTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_glitchTimer > 0) _glitchTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_screenFlashState != null)
            {
                _screenFlashState.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_screenFlashState.Timer >= ScreenFlashState.TOTAL_FLASH_CYCLE_DURATION)
                {
                    _screenFlashState.Timer -= ScreenFlashState.TOTAL_FLASH_CYCLE_DURATION;
                    _screenFlashState.FlashesRemaining--;
                    if (_screenFlashState.FlashesRemaining <= 0) _screenFlashState = null;
                }
                if (_screenFlashState != null) _screenFlashState.IsCurrentlyWhite = _screenFlashState.Timer < ScreenFlashState.FLASH_ON_DURATION;
            }

            KeyboardState currentKeyboardState = Keyboard.GetState();
            MouseState currentMouseState = Mouse.GetState();

            if (currentKeyboardState.IsKeyDown(Keys.OemTilde) && _previousKeyboardState.IsKeyUp(Keys.OemTilde))
            {
                if (_debugConsole.IsVisible) _debugConsole.Hide(); else _debugConsole.Show();
            }
            _global.ShowSplitMapGrid = currentKeyboardState.IsKeyDown(Keys.F1);
            _drawMouseDebugDot = currentKeyboardState.IsKeyDown(Keys.F2);
            _global.ShowDebugOverlays = currentKeyboardState.IsKeyDown(Keys.F3);

            if (KeyPressed(Keys.F5, currentKeyboardState, _previousKeyboardState)) { ResetGame(); _sceneManager.ChangeScene(GameSceneState.MainMenu, TransitionType.None, TransitionType.None); }
            if (KeyPressed(Keys.F9, currentKeyboardState, _previousKeyboardState)) BattleDebugHelper.RunDamageCalculationTestSuite();
            if (KeyPressed(Keys.F10, currentKeyboardState, _previousKeyboardState)) MaximizeWindow();
            if (KeyPressed(Keys.F12, currentKeyboardState, _previousKeyboardState)) _sceneManager.ChangeScene(GameSceneState.AnimationEditor);

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;

            if (_isCustomResolutionSavePending)
            {
                _customResolutionSaveTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_customResolutionSaveTimer <= 0f)
                {
                    _isCustomResolutionSavePending = false;
                    _settings.Resolution = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
                    SettingsManager.SaveSettings(_settings);
                }
            }

            float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (elapsedSeconds > 0.1f) elapsedSeconds = 0.1f;

            float timeScale = _hitstopManager.Update(elapsedSeconds);

            _transitionManager.Update(gameTime);
            _backgroundNoiseRenderer.Update(gameTime);

            if (_loadingScreen.IsActive)
            {
                _loadingScreen.Update(gameTime);
                _sceneManager.Update(gameTime);
                return;
            }

            if (_debugConsole.IsVisible)
            {
                UIInputManager.Update(gameTime);
                _debugConsole.Update(gameTime);
                _hapticsManager.Update(gameTime);
                base.Update(gameTime);
                return;
            }

            if (!_transitionManager.IsScreenObscured)
            {
                GameTime scaledGameTime = new GameTime(gameTime.TotalGameTime, TimeSpan.FromSeconds(elapsedSeconds * timeScale));
                _sceneManager.Update(scaledGameTime);
                _cursorManager.Update(gameTime);
            }

            _hapticsManager.Update(gameTime);
            _particleSystemManager.Update(elapsedSeconds);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(_finalCompositeTarget);

            bool forceBlackBg = _sceneManager.CurrentActiveScene is StartupScene;
            Color letterboxColor = forceBlackBg ? Color.Black : _global.GameBg;
            GraphicsDevice.Clear(letterboxColor);

            var (shakeOffset, shakeRotation, shakeScale) = _hapticsManager.GetTotalShakeParams();
            Vector2 screenCenter = _finalRenderRectangle.Center.ToVector2();

            Matrix baseTransform = Matrix.CreateScale(_finalScale, _finalScale, 1.0f) *
                                   Matrix.CreateTranslation(_finalRenderRectangle.X, _finalRenderRectangle.Y, 0);

            Matrix shakeMatrix =
                Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y, 0) *
                Matrix.CreateScale(shakeScale, shakeScale, 1.0f) *
                Matrix.CreateRotationZ(shakeRotation) *
                Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0) *
                Matrix.CreateTranslation(shakeOffset.X * _finalScale, shakeOffset.Y * _finalScale, 0);

            Matrix finalSceneTransform = baseTransform * shakeMatrix;

            finalSceneTransform.M41 = MathF.Round(finalSceneTransform.M41);
            finalSceneTransform.M42 = MathF.Round(finalSceneTransform.M42);

            if (!_loadingScreen.IsActive && !_sceneManager.IsLoadingBetweenScenes && !_sceneManager.IsHoldingBlack)
            {
                _sceneManager.Draw(_spriteBatch, _defaultFont, gameTime, finalSceneTransform);
            }

            if (_transitionManager.IsTransitioning)
            {
                GraphicsDevice.SetRenderTarget(_transitionRenderTarget);
                GraphicsDevice.Clear(Color.Transparent);
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                var transitionSize = new Vector2(_transitionRenderTarget.Width, _transitionRenderTarget.Height);
                _transitionManager.Draw(_spriteBatch, transitionSize, 1.0f);
                _spriteBatch.End();

                GraphicsDevice.SetRenderTarget(_finalCompositeTarget);
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _spriteBatch.Draw(_transitionRenderTarget, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, _finalScale, SpriteEffects.None, 0f);
                _spriteBatch.End();
            }

            if (_loadingScreen.IsActive)
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, finalSceneTransform);
                _loadingScreen.Draw(_spriteBatch, _tertiaryFont);
                _spriteBatch.End();
            }

            if (!_loadingScreen.IsActive)
            {
                _sceneManager.CurrentActiveScene?.DrawFullscreenUI(_spriteBatch, _defaultFont, gameTime, finalSceneTransform);

                if (_fullscreenOverlays.Count > 0)
                {
                    foreach (var action in _fullscreenOverlays)
                    {
                        action(_spriteBatch, finalSceneTransform);
                    }
                }
            }

            _spriteBatch.Begin(blendState: _cursorInvertBlendState, samplerState: SamplerState.PointClamp, transformMatrix: Matrix.Identity);
            _cursorManager.Draw(_spriteBatch, Mouse.GetState().Position.ToVector2(), _finalScale);
            _spriteBatch.End();

            _backgroundNoiseRenderer.Apply(GraphicsDevice, _finalCompositeTarget, gameTime, _finalScale);

            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(letterboxColor);

            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, null, null, null, Matrix.Identity);

            if (_crtEffect != null)
            {
                _crtEffect.Parameters["Time"]?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);
                _crtEffect.Parameters["ScreenResolution"]?.SetValue(new Vector2(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight));
                _crtEffect.Parameters["VirtualResolution"]?.SetValue(new Vector2(Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT));
                _crtEffect.Parameters["Gamma"]?.SetValue(_settings.Gamma);
                _crtEffect.Parameters["Saturation"]?.SetValue(_global.CrtSaturation);
                _crtEffect.Parameters["Vibrance"]?.SetValue(_global.CrtVibrance);

                float flashIntensity = 0f;
                Color flashColor = Color.Transparent;

                if (_screenFlashState != null && _screenFlashState.IsCurrentlyWhite)
                {
                    flashIntensity = 1.0f;
                    flashColor = (_screenFlashState.FlashesRemaining == ScreenFlashState.TOTAL_FLASHES) ? Color.White : _screenFlashState.FlashColor;
                }
                else if (_flashTimer > 0 && _flashDuration > 0)
                {
                    flashIntensity = Easing.EaseOutCubic(_flashTimer / _flashDuration);
                    flashColor = _flashColor;
                }

                _crtEffect.Parameters["FlashColor"]?.SetValue(flashColor.ToVector3());
                _crtEffect.Parameters["FlashIntensity"]?.SetValue(flashIntensity * 0.25f);

                float glitchIntensity = 0f;
                if (_glitchTimer > 0 && _glitchDuration > 0) glitchIntensity = Easing.EaseOutCubic(_glitchTimer / _glitchDuration);
                _crtEffect.Parameters["ImpactGlitchIntensity"]?.SetValue(glitchIntensity);

                _crtEffect.CurrentTechnique.Passes[0].Apply();
            }

            if (_backgroundNoiseRenderer.Texture != null)
            {
                _spriteBatch.Draw(_backgroundNoiseRenderer.Texture, new Rectangle(0, 0, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight), Color.White);
            }

            _spriteBatch.End();

            if (!_loadingScreen.IsActive)
            {
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _sceneManager.DrawOverlay(_spriteBatch, _defaultFont, gameTime);
                if (_debugConsole.IsVisible) _debugConsole.Draw(_spriteBatch, _secondaryFont, gameTime);

                if (_defaultFont != null)
                {
                    string versionText = $"v{Global.GAME_VERSION}";
                    var screenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
                    _spriteBatch.DrawStringSnapped(_defaultFont, versionText, new Vector2(5f, screenHeight - _defaultFont.LineHeight - 5f), _global.Palette_DarkShadow);
                }
                _spriteBatch.End();
            }

            if (_drawMouseDebugDot)
            {
                _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: Matrix.Invert(_mouseTransformMatrix));
                var virtualMousePos = TransformMouse(Mouse.GetState().Position);
                _spriteBatch.Draw(_pixel, virtualMousePos, Color.Red);
                var secondaryFont = ServiceLocator.Get<Core>().SecondaryFont;
                _spriteBatch.DrawStringSnapped(secondaryFont, $"({virtualMousePos.X}, {virtualMousePos.Y})", virtualMousePos + new Vector2(3, -secondaryFont.LineHeight / 2), Color.Red);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        public void OnResize(object sender, EventArgs e)
        {
            if (GraphicsDevice == null) return;

            var newResolution = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
            if (newResolution != _previousResolution)
            {
                EventBus.Publish(new GameEvents.UIThemeOrResolutionChanged());
                _previousResolution = newResolution;
            }

            if (_settings.Mode == WindowMode.Windowed)
            {
                if (_settings.Resolution != newResolution)
                {
                    _settings.Resolution = newResolution;
                    _isCustomResolutionSavePending = true;
                    _customResolutionSaveTimer = 0.5f;
                }
            }

            bool isStandard = SettingsManager.GetResolutions().Any(r => r.Value == newResolution);
            if (!isStandard)
            {
                _isCustomResolutionSavePending = true;
                _customResolutionSaveTimer = 0.5f;
            }

            if (_sceneManager.CurrentActiveScene is SettingsScene settingsScene) settingsScene.RefreshUIFromSettings();

            var screenWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var screenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;

            float scaleX = (float)screenWidth / Global.VIRTUAL_WIDTH;
            float scaleY = (float)screenHeight / Global.VIRTUAL_HEIGHT;

            if (screenWidth < Global.VIRTUAL_WIDTH || screenHeight < Global.VIRTUAL_HEIGHT) _finalScale = Math.Min(scaleX, scaleY);
            else
            {
                int integerScale = (int)Math.Min(scaleX, scaleY);
                if (_settings.SmallerUi) integerScale--;
                _finalScale = Math.Max(1, integerScale);
            }

            int destWidth = (int)(Global.VIRTUAL_WIDTH * _finalScale);
            int destHeight = (int)(Global.VIRTUAL_HEIGHT * _finalScale);
            int destX = (screenWidth - destWidth) / 2;
            int destY = (screenHeight - destHeight) / 2;

            _finalRenderRectangle = new Rectangle(destX, destY, destWidth, destHeight);
            _mouseTransformMatrix = Matrix.CreateTranslation(-destX, -destY, 0) * Matrix.CreateScale(1.0f / _finalScale);

            _finalCompositeTarget?.Dispose();
            _finalCompositeTarget = new RenderTarget2D(
                GraphicsDevice,
                Window.ClientBounds.Width,
                Window.ClientBounds.Height,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24,
                0,
                RenderTargetUsage.PreserveContents);

            _transitionRenderTarget?.Dispose();
            int transWidth = (int)Math.Ceiling((float)screenWidth / _finalScale);
            int transHeight = (int)Math.Ceiling((float)screenHeight / _finalScale);
            _transitionRenderTarget = new RenderTarget2D(
                GraphicsDevice,
                transWidth,
                transHeight,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24,
                0,
                RenderTargetUsage.PreserveContents);
        }

        public static Vector2 TransformMouse(Point screenPoint)
        {
            var coreInstance = ServiceLocator.Get<Core>();
            var transformed = Vector2.Transform(screenPoint.ToVector2(), coreInstance.MouseTransformMatrix);
            return new Vector2(MathF.Round(transformed.X), MathF.Round(transformed.Y));
        }

        public static Point TransformVirtualToScreen(Point virtualPoint)
        {
            var coreInstance = ServiceLocator.Get<Core>();
            var toScreenMatrix = Matrix.Invert(coreInstance.MouseTransformMatrix);
            var screenVector = Vector2.Transform(virtualPoint.ToVector2(), toScreenMatrix);
            return new Point((int)screenVector.X, (int)screenVector.Y);
        }

        public Rectangle GetActualScreenVirtualBounds()
        {
            Vector2 topLeftVirtual = TransformMouse(new Point(0, 0));
            Vector2 bottomRightVirtual = TransformMouse(new Point(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight));
            return new Rectangle((int)topLeftVirtual.X, (int)topLeftVirtual.Y, (int)(bottomRightVirtual.X - topLeftVirtual.X), (int)(bottomRightVirtual.Y - topLeftVirtual.Y));
        }

        public void ExitApplication() => Exit();
        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}
