using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Graphics;
using ProjectVagabond;
using ProjectVagabond.Battle;
using ProjectVagabond.Dice;
using ProjectVagabond.Particles;
using ProjectVagabond.Progression;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using ProjectVagabond.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ProjectVagabond
{
    public class Core : Game
    {
        // Graphics and Content
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private BitmapFont _defaultFont;
        private BitmapFont _secondaryFont;
        private Texture2D _pixel;
        private Rectangle _finalRenderRectangle;
        private Matrix _mouseTransformMatrix;
        private Point _previousResolution;
        private float _finalScale = 1f;
        // --- Post-Processing Members ---
        private RenderTarget2D _sceneRenderTarget;
        private RenderTarget2D _finalCompositeTarget;
        private Effect _crtEffect;
        private float _flashTimer;
        private float _flashDuration;
        private Color _flashColor;
        private float _glitchTimer;
        private float _glitchDuration;
        private BlendState _cursorInvertBlendState;
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


        public Matrix MouseTransformMatrix => _mouseTransformMatrix;
        public BitmapFont SecondaryFont => _secondaryFont;
        public float FinalScale => _finalScale;

        // Managers & Systems
        private Global _global;
        private GameSettings _settings;
        private SceneManager _sceneManager;
        private HapticsManager _hapticsManager;
        private SystemManager _systemManager;
        private TooltipManager _tooltipManager;
        private ParticleSystemManager _particleSystemManager;
        private GameState _gameState;
        private ActionExecutionSystem _actionExecutionSystem;
        private MoveAcquisitionSystem _moveAcquisitionSystem;
        private RelicAcquisitionSystem _relicAcquisitionSystem;
        private SpriteManager _spriteManager;
        private DiceRollingSystem _diceRollingSystem;
        private BackgroundManager _backgroundManager;
        private LoadingScreen _loadingScreen;
        private AnimationManager _animationManager;
        private DebugConsole _debugConsole;
        private ProgressionManager _progressionManager;
        private CursorManager _cursorManager;

        // Input State
        private KeyboardState _previousKeyboardState;
        private bool _drawMouseDebugDot = false;

        // Physics Timestep
        private float _physicsTimeAccumulator = 0f;

        // Loading State
        private bool _isGameLoaded = false;

        // Custom Resolution Save State
        private float _customResolutionSaveTimer = 0f;
        private bool _isCustomResolutionSavePending = false;
        private readonly Random _random = new Random();

        // --- Manual Frame Limiter State ---
        private Stopwatch _frameStopwatch;
        private TimeSpan _targetElapsedTimeSpan;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        // NATIVE METHODS FOR TIMER RESOLUTION
        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        private void SetHighPrecisionTimer()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try { TimeBeginPeriod(1); } catch { }
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public Core()
        {
            // --- CRASH SAFETY HOOK ---
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

            // CRITICAL FIX: Disable MonoGame's built-in fixed step.
            // We will handle timing manually to prevent windowed mode stutter.
            IsFixedTimeStep = false;
            _graphics.SynchronizeWithVerticalRetrace = false; // We will manage this via settings
        }

        protected override void Initialize()
        {
            ConsoleRedirection.Initialize();
            SetHighPrecisionTimer();

            // Initialize Frame Limiter
            _frameStopwatch = new Stopwatch();
            _frameStopwatch.Start();

            // Phase 1: Register Core Services & Settings
            ServiceLocator.Register<Core>(this);
            ServiceLocator.Register<GraphicsDeviceManager>(_graphics);
            ServiceLocator.Register<GameWindow>(Window);
            ServiceLocator.Register<Global>(Global.Instance);
            _settings = SettingsManager.LoadSettings();
            ServiceLocator.Register<GameSettings>(_settings);

            _global = ServiceLocator.Get<Global>();

            // Phase 2: GraphicsDevice Registration
            ServiceLocator.Register<GraphicsDevice>(GraphicsDevice);

            _sceneRenderTarget = new RenderTarget2D(
                GraphicsDevice,
                Global.VIRTUAL_WIDTH,
                Global.VIRTUAL_HEIGHT,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);

            // Phase 3: Instantiate and Register Managers
            var entityManager = new EntityManager();
            ServiceLocator.Register<EntityManager>(entityManager);

            var componentStore = new ComponentStore();
            ServiceLocator.Register<ComponentStore>(componentStore);

            var chunkManager = new ChunkManager();
            ServiceLocator.Register<ChunkManager>(chunkManager);

            _systemManager = new SystemManager();
            ServiceLocator.Register<SystemManager>(_systemManager);

            var archetypeManager = new ArchetypeManager();
            ServiceLocator.Register<ArchetypeManager>(archetypeManager);

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

            _animationManager = new AnimationManager();
            ServiceLocator.Register<AnimationManager>(_animationManager);

            _tooltipManager = new TooltipManager();
            ServiceLocator.Register<TooltipManager>(_tooltipManager);

            _particleSystemManager = new ParticleSystemManager();
            ServiceLocator.Register<ParticleSystemManager>(_particleSystemManager);

            ServiceLocator.Register<DicePhysicsController>(new DicePhysicsController());
            ServiceLocator.Register<DiceSceneRenderer>(new DiceSceneRenderer());
            ServiceLocator.Register<DiceAnimationController>(new DiceAnimationController());

            _diceRollingSystem = new DiceRollingSystem();
            ServiceLocator.Register<DiceRollingSystem>(_diceRollingSystem);

            _backgroundManager = new BackgroundManager();
            ServiceLocator.Register<BackgroundManager>(_backgroundManager);

            _gameState = new GameState(noiseManager, componentStore, chunkManager, _global, _spriteManager);
            ServiceLocator.Register<GameState>(_gameState);

            var playerInputSystem = new PlayerInputSystem();
            ServiceLocator.Register<PlayerInputSystem>(playerInputSystem);

            _actionExecutionSystem = new ActionExecutionSystem();
            ServiceLocator.Register<ActionExecutionSystem>(_actionExecutionSystem);

            _moveAcquisitionSystem = new MoveAcquisitionSystem();
            ServiceLocator.Register<MoveAcquisitionSystem>(_moveAcquisitionSystem);

            _relicAcquisitionSystem = new RelicAcquisitionSystem();
            ServiceLocator.Register<RelicAcquisitionSystem>(_relicAcquisitionSystem);

            var terminalRenderer = new TerminalRenderer();
            ServiceLocator.Register<TerminalRenderer>(terminalRenderer);

            var mapRenderer = new MapRenderer();
            ServiceLocator.Register<MapRenderer>(mapRenderer);

            var mapInputHandler = new MapInputHandler(mapRenderer.MapContextMenu, mapRenderer);
            ServiceLocator.Register<MapInputHandler>(mapInputHandler);

            var autoCompleteManager = new AutoCompleteManager();
            ServiceLocator.Register<AutoCompleteManager>(autoCompleteManager);

            var commandProcessor = new CommandProcessor(playerInputSystem);
            ServiceLocator.Register<CommandProcessor>(commandProcessor);

            _progressionManager = new ProgressionManager();
            ServiceLocator.Register<ProgressionManager>(_progressionManager);

            _sceneManager = new SceneManager();
            ServiceLocator.Register<SceneManager>(_sceneManager);
            _sceneManager.AddScene(GameSceneState.Transition, new TransitionScene());

            _debugConsole = new DebugConsole();
            ServiceLocator.Register<DebugConsole>(_debugConsole);

            _cursorManager = new CursorManager();
            ServiceLocator.Register<CursorManager>(_cursorManager);

            // Phase 4: Final Setup
            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

            _settings.ApplyGraphicsSettings(_graphics, this);
            _settings.ApplyGameSettings();

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            ServiceLocator.Register<Texture2D>(_pixel);

            _systemManager.RegisterSystem(_actionExecutionSystem, 0f);
            _systemManager.RegisterSystem(_moveAcquisitionSystem, 0f);
            _systemManager.RegisterSystem(_relicAcquisitionSystem, 0f);

            _sceneManager.AddScene(GameSceneState.MainMenu, new MainMenuScene());
            _sceneManager.AddScene(GameSceneState.TerminalMap, new GameMapScene());
            _sceneManager.AddScene(GameSceneState.Settings, new SettingsScene());
            _sceneManager.AddScene(GameSceneState.Battle, new BattleScene());
            _sceneManager.AddScene(GameSceneState.ChoiceMenu, new ChoiceMenuScene());
            _sceneManager.AddScene(GameSceneState.Split, new SplitMapScene());

            _previousResolution = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
            OnResize(null, null);

            _finalCompositeTarget = new RenderTarget2D(
                GraphicsDevice,
                Window.ClientBounds.Width,
                Window.ClientBounds.Height,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);

            _cursorInvertBlendState = new BlendState
            {
                ColorBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.InverseDestinationColor,
                ColorDestinationBlend = Blend.InverseSourceAlpha,
                AlphaBlendFunction = BlendFunction.Add,
                AlphaSourceBlend = Blend.Zero,
                AlphaDestinationBlend = Blend.One
            };

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
            catch { Debug.WriteLine("[WARNING] Could not load secondary font 'Fonts/5x5_pixel'. Using default font as fallback."); _secondaryFont = _defaultFont; }

            _spriteManager.LoadEssentialContent();
            _backgroundManager.LoadContent();
            BattleDataCache.LoadData(Content);
            _progressionManager.LoadSplits();
            _diceRollingSystem.Initialize(GraphicsDevice, Content);
            _sceneManager.ChangeScene(GameSceneState.MainMenu);
        }

        public void SetGameLoaded(bool isLoaded) => _isGameLoaded = isLoaded;
        public void TriggerFullscreenFlash(Color color, float duration) { _flashColor = color; _flashDuration = duration; _flashTimer = duration; }
        public void TriggerScreenFlashSequence(Color color) { _screenFlashState = new ScreenFlashState { Timer = 0f, FlashesRemaining = ScreenFlashState.TOTAL_FLASHES, IsCurrentlyWhite = true, FlashColor = color }; }
        public void TriggerFullscreenGlitch(float duration) { _glitchDuration = duration; _glitchTimer = duration; }

        protected override void Update(GameTime gameTime)
        {
            // --- MANUAL FRAME LIMITER ---
            // This replaces MonoGame's IsFixedTimeStep to prevent windowed mode stutter.
            if (_settings.IsFrameLimiterEnabled)
            {
                // Calculate how much time we *should* have spent to hit the target FPS
                _targetElapsedTimeSpan = TimeSpan.FromSeconds(1.0 / _settings.TargetFramerate);

                // If we are running faster than the target, sleep off the difference
                while (_frameStopwatch.Elapsed < _targetElapsedTimeSpan)
                {
                    // Use a spin-wait for the last millisecond for precision, sleep for the rest
                    var remaining = _targetElapsedTimeSpan - _frameStopwatch.Elapsed;
                    if (remaining.TotalMilliseconds > 1)
                    {
                        Thread.Sleep(1);
                    }
                }
            }
            _frameStopwatch.Restart();

            IsMouseVisible = _debugConsole.IsVisible;

            if (!IsActive) return;

            // Update timers
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

            // Debug Toggles
            if (currentKeyboardState.IsKeyDown(Keys.OemTilde) && _previousKeyboardState.IsKeyUp(Keys.OemTilde))
            {
                if (_debugConsole.IsVisible) _debugConsole.Hide(); else _debugConsole.Show();
            }
            _global.ShowSplitMapGrid = currentKeyboardState.IsKeyDown(Keys.F1);
            _drawMouseDebugDot = currentKeyboardState.IsKeyDown(Keys.F2);
            _global.ShowDebugOverlays = currentKeyboardState.IsKeyDown(Keys.F3);
            _diceRollingSystem.DebugShowColliders = _global.ShowDebugOverlays;

            // Debug Shortcuts
            if (KeyPressed(Keys.F5, currentKeyboardState, _previousKeyboardState)) SoftResetGame();
            if (KeyPressed(Keys.F9, currentKeyboardState, _previousKeyboardState)) BattleDebugHelper.RunDamageCalculationTestSuite();
            if (KeyPressed(Keys.F12, currentKeyboardState, _previousKeyboardState)) _sceneManager.ChangeScene(GameSceneState.AnimationEditor);

            _previousKeyboardState = currentKeyboardState;

            if (_isCustomResolutionSavePending)
            {
                _customResolutionSaveTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_customResolutionSaveTimer <= 0f)
                {
                    _isCustomResolutionSavePending = false;
                    _settings.Resolution = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
                    SettingsManager.SaveSettings(_settings);
                    Debug.WriteLine($"[Settings] Custom resolution {_settings.Resolution} saved.");
                }
            }

            // --- PHYSICS UPDATE ---
            // Since IsFixedTimeStep is false, ElapsedGameTime is the REAL delta time.
            // We accumulate this real time and step physics at a fixed rate.
            float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Cap huge delta times (e.g. after dragging window) to prevent spiral of death
            if (elapsedSeconds > 0.1f) elapsedSeconds = 0.1f;

            _physicsTimeAccumulator += elapsedSeconds * _global.DiceSimulationSpeedMultiplier;

            while (_physicsTimeAccumulator >= Global.FIXED_PHYSICS_TIMESTEP)
            {
                _diceRollingSystem.PhysicsStep(Global.FIXED_PHYSICS_TIMESTEP);
                _particleSystemManager.Update(Global.FIXED_PHYSICS_TIMESTEP);
                _physicsTimeAccumulator -= Global.FIXED_PHYSICS_TIMESTEP;
            }

            if (_loadingScreen.IsActive)
            {
                _loadingScreen.Update(gameTime);
                _sceneManager.Update(gameTime);
                if (!_isGameLoaded) _diceRollingSystem.Update(gameTime);
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

            _sceneManager.Update(gameTime);
            _diceRollingSystem.Update(gameTime);
            _animationManager.Update(gameTime);
            _cursorManager.Update(gameTime);

            if (!_gameState.IsPaused)
            {
                if (_sceneManager.CurrentActiveScene is GameMapScene)
                {
                    _gameState.UpdateActiveEntities();
                    _systemManager.Update(gameTime);
                }
            }

            _hapticsManager.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_loadingScreen.IsActive)
            {
                GraphicsDevice.Clear(Color.Black);
                Matrix virtualToScreenTransform = Matrix.Invert(_mouseTransformMatrix);
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, virtualToScreenTransform);
                _loadingScreen.Draw(_spriteBatch, _secondaryFont);
                _spriteBatch.End();
                base.Draw(gameTime);
                return;
            }

            if (_sceneManager.CurrentActiveScene?.GetType() != typeof(TransitionScene))
            {
                GraphicsDevice.SetRenderTarget(_sceneRenderTarget);
                GraphicsDevice.Clear(Color.Transparent);
                _sceneManager.Draw(_spriteBatch, _defaultFont, gameTime, Matrix.Identity);
            }

            var diceRenderTarget = _diceRollingSystem.Draw(_defaultFont);

            GraphicsDevice.SetRenderTarget(_finalCompositeTarget);
            GraphicsDevice.Clear(_global.GameBg);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            if (!_sceneManager.IsLoadingBetweenScenes && !_sceneManager.IsHoldingBlack)
            {
                _spriteBatch.Draw(_sceneRenderTarget, _finalRenderRectangle, Color.White);
                if (diceRenderTarget != null) _spriteBatch.Draw(diceRenderTarget, _finalRenderRectangle, Color.White);
            }
            _spriteBatch.End();

            Matrix screenScaleMatrix = Matrix.Invert(_mouseTransformMatrix);
            _sceneManager.CurrentActiveScene?.DrawFullscreenUI(_spriteBatch, _defaultFont, gameTime, screenScaleMatrix);

            _spriteBatch.Begin(blendState: _cursorInvertBlendState, samplerState: SamplerState.PointClamp);
            _cursorManager.Draw(_spriteBatch, Mouse.GetState().Position.ToVector2(), _finalScale);
            _spriteBatch.End();

            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(_global.GameBg);

            Matrix shakeMatrix = _hapticsManager.GetHapticsMatrix();
            shakeMatrix.M41 = MathF.Round(shakeMatrix.M41);
            shakeMatrix.M42 = MathF.Round(shakeMatrix.M42);

            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, null, null, null, shakeMatrix);

            if (_crtEffect != null)
            {
                _crtEffect.Parameters["Time"]?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);
                _crtEffect.Parameters["ScreenResolution"]?.SetValue(new Vector2(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight));
                _crtEffect.Parameters["Gamma"]?.SetValue(_settings.Gamma);

                float flashIntensity = 0f;
                Color flashColor = Color.Transparent;

                if (_screenFlashState != null && _screenFlashState.IsCurrentlyWhite)
                {
                    flashIntensity = 1.0f;
                    flashColor = _screenFlashState.FlashColor;
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

            int backBufferWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int backBufferHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
            int targetWidth = _finalCompositeTarget.Width;
            int targetHeight = _finalCompositeTarget.Height;
            int drawX = (backBufferWidth - targetWidth) / 2;
            int drawY = (backBufferHeight - targetHeight) / 2;

            _spriteBatch.Draw(_finalCompositeTarget, new Rectangle(drawX, drawY, targetWidth, targetHeight), Color.White);
            _spriteBatch.End();

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _sceneManager.DrawOverlay(_spriteBatch, _defaultFont, gameTime);
            if (_debugConsole.IsVisible) _debugConsole.Draw(_spriteBatch, _defaultFont, gameTime);

            if (_defaultFont != null)
            {
                string versionText = $"v{Global.GAME_VERSION}";
                var screenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
                _spriteBatch.DrawStringSnapped(_defaultFont, versionText, new Vector2(5f, screenHeight - _defaultFont.LineHeight - 5f), _global.Palette_DarkGray);
            }
            _spriteBatch.End();

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

            bool isStandard = SettingsManager.GetResolutions().Any(r => r.Value == newResolution);
            if (!isStandard)
            {
                _isCustomResolutionSavePending = true;
                _customResolutionSaveTimer = 0.5f;
            }
            else _isCustomResolutionSavePending = false;

            _settings.Resolution = newResolution;
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
            _finalCompositeTarget = new RenderTarget2D(GraphicsDevice, Window.ClientBounds.Width, Window.ClientBounds.Height, false, GraphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.Depth24);
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

        private void SoftResetGame()
        {
            _diceRollingSystem.ClearRoll();
            _actionExecutionSystem.HandleInterruption();
            _particleSystemManager.ClearAllEmitters();
            _hapticsManager.StopAll();
            _tooltipManager.Hide();
            _loadingScreen.Clear();
            _debugConsole.ClearHistory();
            _progressionManager.ClearCurrentSplitMap();
            var entityManager = ServiceLocator.Get<EntityManager>();
            var componentStore = ServiceLocator.Get<ComponentStore>();
            entityManager.Clear();
            componentStore.Clear();
            _gameState.Reset();
            _sceneManager.ChangeScene(GameSceneState.MainMenu);
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}