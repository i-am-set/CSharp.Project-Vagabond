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
using System.Text;

// TODO: generate different noise maps to generate different map things
// TODO: add a way to generate different map elements based on the noise map
// TODO: make the map generation more complex, e.g. add rivers, lakes, etc.
// TODO: player customization; backgrounds, stats, bodyfat, muscle (both of which effect stat spread as well as gives buffs and needs at their extremes)
// TODO: Ctrl-Z undo previous path queued
// TODO: Brainstorm a way to add POIs (think rust, darkwood, the long dark, tarkov)
// TODO: Add a way to save and load the game state
// TODO: Finish entity implimentation
// TODO: Add a 1d8, 1d10, 1d12, and 1d20 to the dice rolling system; one at a time since its complex to impliment
// TODO: Make hit marker number that appears on map entity

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
        private MoveLearningSystem _moveLearningSystem;
        private AbilityLearningSystem _abilityLearningSystem;
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

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = false;

            _graphics.PreferredBackBufferWidth = Global.VIRTUAL_WIDTH;
            _graphics.PreferredBackBufferHeight = Global.VIRTUAL_HEIGHT;
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnResize;
        }

        protected override void Initialize()
        {
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

            // Initialize the virtual resolution render target
            _sceneRenderTarget = new RenderTarget2D(
                GraphicsDevice,
                Global.VIRTUAL_WIDTH,
                Global.VIRTUAL_HEIGHT,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);


            // Phase 3: Instantiate and Register Managers & Systems in Dependency Order
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

            // Instantiate and register the individual dice controllers first.
            ServiceLocator.Register<DicePhysicsController>(new DicePhysicsController());
            ServiceLocator.Register<DiceSceneRenderer>(new DiceSceneRenderer());
            ServiceLocator.Register<DiceAnimationController>(new DiceAnimationController());

            // Now the DiceRollingSystem can be created and it will fetch its dependencies.
            _diceRollingSystem = new DiceRollingSystem();
            ServiceLocator.Register<DiceRollingSystem>(_diceRollingSystem);

            _backgroundManager = new BackgroundManager();
            ServiceLocator.Register<BackgroundManager>(_backgroundManager);

            // GameState must be registered before systems that depend on it in their constructor.
            _gameState = new GameState(noiseManager, componentStore, chunkManager, _global, _spriteManager);
            ServiceLocator.Register<GameState>(_gameState);

            var playerInputSystem = new PlayerInputSystem();
            ServiceLocator.Register<PlayerInputSystem>(playerInputSystem);

            _actionExecutionSystem = new ActionExecutionSystem();
            ServiceLocator.Register<ActionExecutionSystem>(_actionExecutionSystem);

            _moveLearningSystem = new MoveLearningSystem();
            ServiceLocator.Register<MoveLearningSystem>(_moveLearningSystem);

            _abilityLearningSystem = new AbilityLearningSystem();
            ServiceLocator.Register<AbilityLearningSystem>(_abilityLearningSystem);

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
            _systemManager.RegisterSystem(_moveLearningSystem, 0f);
            _systemManager.RegisterSystem(_abilityLearningSystem, 0f);

            _sceneManager.AddScene(GameSceneState.MainMenu, new MainMenuScene());
            _sceneManager.AddScene(GameSceneState.TerminalMap, new GameMapScene()); // Changed to GameMapScene
            _sceneManager.AddScene(GameSceneState.Settings, new SettingsScene());
            _sceneManager.AddScene(GameSceneState.Battle, new BattleScene());
            _sceneManager.AddScene(GameSceneState.ChoiceMenu, new ChoiceMenuScene());
            _sceneManager.AddScene(GameSceneState.Split, new SplitMapScene());

            _previousResolution = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
            OnResize(null, null);

            // Initialize the full-screen composite render target
            _finalCompositeTarget = new RenderTarget2D(
                GraphicsDevice,
                Window.ClientBounds.Width,
                Window.ClientBounds.Height,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            ServiceLocator.Register<SpriteBatch>(_spriteBatch);

            // Load the CRT shader
            try
            {
                _crtEffect = Content.Load<Effect>("Shaders/CRTShader");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FATAL ERROR] Could not load CRT Shader: {ex.Message}");
                _crtEffect = null;
            }

            try
            {
                _defaultFont = Content.Load<BitmapFont>("Fonts/Px437_IBM_BIOS");
                ServiceLocator.Register<BitmapFont>(_defaultFont);
            }
            catch
            {
                throw new Exception("Please add a BitmapFont to your 'Content/Fonts' folder");
            }

            try
            {
                _secondaryFont = Content.Load<BitmapFont>("Fonts/5x5_pixel");
                // Not registering with ServiceLocator to avoid type collision.
                // Access via ServiceLocator.Get<Core>().SecondaryFont
            }
            catch
            {
                // If the secondary font fails, we can fall back to the default font.
                // This prevents a crash if the asset is missing.
                Debug.WriteLine("[WARNING] Could not load secondary font 'Fonts/5x5_pixel'. Using default font as fallback.");
                _secondaryFont = _defaultFont;
            }

            // Load only essential assets needed for the main menu and global UI.
            _spriteManager.LoadEssentialContent();
            _backgroundManager.LoadContent();

            // Load data for battle system
            BattleDataCache.LoadData(Content);
            _progressionManager.LoadSplits();

            // Initialize core systems that require content but should always be available.
            _diceRollingSystem.Initialize(GraphicsDevice, Content);

            // The rest of the loading is deferred until the player clicks "Play".

            // Set the initial scene to the main menu.
            _sceneManager.ChangeScene(GameSceneState.MainMenu);
        }

        /// <summary>
        /// Sets the flag indicating whether the main game assets have been loaded.
        /// </summary>
        public void SetGameLoaded(bool isLoaded)
        {
            _isGameLoaded = isLoaded;
        }

        /// <summary>
        /// Triggers a full-screen color flash that fades out over the specified duration.
        /// </summary>
        public void TriggerFullscreenFlash(Color color, float duration)
        {
            _flashColor = color;
            _flashDuration = duration;
            _flashTimer = duration;
        }

        /// <summary>
        /// Triggers a sequence of full-screen color flashes.
        /// </summary>
        public void TriggerScreenFlashSequence(Color color)
        {
            _screenFlashState = new ScreenFlashState
            {
                Timer = 0f,
                FlashesRemaining = ScreenFlashState.TOTAL_FLASHES,
                IsCurrentlyWhite = true,
                FlashColor = color
            };
        }

        /// <summary>
        /// Triggers a full-screen glitch effect that fades out over the specified duration.
        /// </summary>
        public void TriggerFullscreenGlitch(float duration)
        {
            _glitchDuration = duration;
            _glitchTimer = duration;
        }

        /// <summary>
        /// Handles the result of a completed dice roll.
        /// </summary>
        /// <param name="result">The structured result of the roll.</param>
        private void HandleRollCompleted(DiceRollResult result)
        {
            Debug.WriteLine("---------- ROLL COMPLETED ----------");
            var sb = new StringBuilder();
            foreach (var groupResult in result.ResultsByGroup)
            {
                sb.Clear();
                sb.Append(string.Join(", ", groupResult.Value));
                Debug.WriteLine($"Group '{groupResult.Key}' Results: [ {sb} ]");
            }
            Debug.WriteLine("------------------------------------");
        }

        protected override void Update(GameTime gameTime)
        {
            // Pause the entire game update loop if the window is not focused.
            if (!IsActive)
            {
                return;
            }

            if (_settings.IsFrameLimiterEnabled)
            {
                IsFixedTimeStep = true;
                TargetElapsedTime = TimeSpan.FromSeconds(1.0 / _settings.TargetFramerate);
            }
            else
            {
                IsFixedTimeStep = false;
            }
            _graphics.SynchronizeWithVerticalRetrace = _settings.IsVsync;

            // Update the flash and glitch timers
            if (_flashTimer > 0)
            {
                _flashTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
            if (_glitchTimer > 0)
            {
                _glitchTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
            if (_screenFlashState != null)
            {
                _screenFlashState.Timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_screenFlashState.Timer >= ScreenFlashState.TOTAL_FLASH_CYCLE_DURATION)
                {
                    _screenFlashState.Timer -= ScreenFlashState.TOTAL_FLASH_CYCLE_DURATION;
                    _screenFlashState.FlashesRemaining--;
                    if (_screenFlashState.FlashesRemaining <= 0)
                    {
                        _screenFlashState = null;
                    }
                }

                if (_screenFlashState != null)
                {
                    _screenFlashState.IsCurrentlyWhite = _screenFlashState.Timer < ScreenFlashState.FLASH_ON_DURATION;
                }
            }

            // Handle debug input
            KeyboardState currentKeyboardState = Keyboard.GetState();

            // --- Console Toggle ---
            if (currentKeyboardState.IsKeyDown(Keys.OemTilde) && _previousKeyboardState.IsKeyUp(Keys.OemTilde))
            {
                if (_debugConsole.IsVisible) _debugConsole.Hide();
                else _debugConsole.Show();
            }

            // --- Debug Overlays (Hold to show) ---
            _global.ShowSplitMapGrid = currentKeyboardState.IsKeyDown(Keys.F1);
            _drawMouseDebugDot = currentKeyboardState.IsKeyDown(Keys.F2);
            _global.ShowDebugOverlays = currentKeyboardState.IsKeyDown(Keys.F3);
            _diceRollingSystem.DebugShowColliders = _global.ShowDebugOverlays;

            if (KeyPressed(Keys.F5, currentKeyboardState, _previousKeyboardState))
            {
                // Soft reset the game
                SoftResetGame();
            }
            if (KeyPressed(Keys.F6, currentKeyboardState, _previousKeyboardState))
            {
                var choiceMenu = _sceneManager.GetScene(GameSceneState.ChoiceMenu) as ChoiceMenuScene;
                if (choiceMenu != null)
                {
                    var choiceGenerator = new ChoiceGenerator();
                    var choices = choiceGenerator.GenerateSpellChoices(1, _random.Next(2, 4)).Cast<object>().ToList();
                    choiceMenu.Show(choices, () => _sceneManager.HideModal());
                    _sceneManager.ShowModal(GameSceneState.ChoiceMenu);
                }
            }
            if (KeyPressed(Keys.F7, currentKeyboardState, _previousKeyboardState))
            {
                var choiceMenu = _sceneManager.GetScene(GameSceneState.ChoiceMenu) as ChoiceMenuScene;
                if (choiceMenu != null)
                {
                    var choiceGenerator = new ChoiceGenerator();
                    var choices = choiceGenerator.GenerateAbilityChoices(1, _random.Next(2, 4)).Cast<object>().ToList();
                    choiceMenu.Show(choices, () => _sceneManager.HideModal());
                    _sceneManager.ShowModal(GameSceneState.ChoiceMenu);
                }
            }
            if (KeyPressed(Keys.F8, currentKeyboardState, _previousKeyboardState))
            {
                var choiceMenu = _sceneManager.GetScene(GameSceneState.ChoiceMenu) as ChoiceMenuScene;
                if (choiceMenu != null)
                {
                    var itemChoices = new List<object>();
                    var allItems = BattleDataCache.Consumables.Values.ToList();
                    if (allItems.Any())
                    {
                        int count = Math.Min(_random.Next(2, 4), allItems.Count);
                        var randomItems = allItems.OrderBy(x => _random.Next()).Take(count);
                        itemChoices.AddRange(randomItems);
                    }
                    choiceMenu.Show(itemChoices, () => _sceneManager.HideModal());
                    _sceneManager.ShowModal(GameSceneState.ChoiceMenu);
                }
            }
            if (KeyPressed(Keys.F9, currentKeyboardState, _previousKeyboardState))
            {
                BattleDebugHelper.RunDamageCalculationTestSuite();
            }
            if (KeyPressed(Keys.F12, currentKeyboardState, _previousKeyboardState))
            {
                _sceneManager.ChangeScene(GameSceneState.AnimationEditor);
            }

            _previousKeyboardState = currentKeyboardState;

            // Handle delayed saving of custom resolutions
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

            // This ensures physics calculations are stable and not dependent on the frame rate.
            // We multiply by the simulation speed to "fast forward" the physics time.
            float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _physicsTimeAccumulator += elapsedSeconds * _global.DiceSimulationSpeedMultiplier;

            while (_physicsTimeAccumulator >= Global.FIXED_PHYSICS_TIMESTEP)
            {
                // Run a single, fixed-step physics update for any relevant systems.
                _diceRollingSystem.PhysicsStep(Global.FIXED_PHYSICS_TIMESTEP);
                _particleSystemManager.Update(Global.FIXED_PHYSICS_TIMESTEP);

                _physicsTimeAccumulator -= Global.FIXED_PHYSICS_TIMESTEP;
            }

            // The loading screen now acts as a modal state that can be triggered at any time.
            if (_loadingScreen.IsActive)
            {
                _loadingScreen.Update(gameTime);
                _sceneManager.Update(gameTime); // Allow scene manager to update its transition state
                // The dice system might be warming up, so it needs updates.
                if (!_isGameLoaded)
                {
                    _diceRollingSystem.Update(gameTime);
                }
                return; // Block all other game updates
            }

            // The debug console is the next highest priority modal state.
            if (_debugConsole.IsVisible)
            {
                _debugConsole.Update(gameTime);
                _hapticsManager.Update(gameTime); // Allow haptics to continue for feedback
                base.Update(gameTime);
                return; // Block all other game updates
            }


            // --- Frame-Rate Dependent Updates ---
            _sceneManager.Update(gameTime);
            _diceRollingSystem.Update(gameTime); // Update dice visuals and game logic every frame.
            _animationManager.Update(gameTime);
            _cursorManager.Update(gameTime);

            // These systems handle game logic and should be paused.
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

            // --- Phase 1: Render the game world (scene, particles, etc.) to its render target ---
            if (_sceneManager.CurrentActiveScene?.GetType() != typeof(TransitionScene))
            {
                GraphicsDevice.SetRenderTarget(_sceneRenderTarget);
                GraphicsDevice.Clear(Color.Transparent);

                Matrix virtualSpaceTransform = Matrix.Identity;

                _sceneManager.Draw(_spriteBatch, _defaultFont, gameTime, virtualSpaceTransform);
            }

            // --- Phase 1.5: Render the dice system to its own render target. ---
            var diceRenderTarget = _diceRollingSystem.Draw(_defaultFont);

            // --- Phase 2: Composite everything onto the full-screen render target ---
            GraphicsDevice.SetRenderTarget(_finalCompositeTarget);
            GraphicsDevice.Clear(_global.GameBg);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            if (!_sceneManager.IsLoadingBetweenScenes && !_sceneManager.IsHoldingBlack)
            {
                // Draw the main game scene first
                _spriteBatch.Draw(_sceneRenderTarget, _finalRenderRectangle, Color.White);

                // Draw the dice system on top of the game scene if it's active
                if (diceRenderTarget != null)
                {
                    // The dice are rendered to a full virtual-size texture, so we draw it
                    // into the same letterboxed rectangle as the main scene.
                    _spriteBatch.Draw(diceRenderTarget, _finalRenderRectangle, Color.White);
                }
            }
            _spriteBatch.End();

            Matrix screenScaleMatrix = Matrix.Invert(_mouseTransformMatrix);
            _sceneManager.CurrentActiveScene?.DrawFullscreenUI(_spriteBatch, _defaultFont, gameTime, screenScaleMatrix);

            // --- Phase 3: Render the final composite to the screen with the CRT shader ---
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(_global.GameBg);

            Matrix shakeMatrix = _hapticsManager.GetHapticsMatrix();
            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, null, null, null, shakeMatrix);

            bool applyCrtEffect = _crtEffect != null;

            if (applyCrtEffect)
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

                float maxFlashOpacity = 0.25f;
                _crtEffect.Parameters["FlashColor"]?.SetValue(flashColor.ToVector3());
                _crtEffect.Parameters["FlashIntensity"]?.SetValue(flashIntensity * maxFlashOpacity);

                // Calculate glitch intensity (fades from 1 to 0)
                float glitchIntensity = 0f;
                if (_glitchTimer > 0 && _glitchDuration > 0)
                {
                    glitchIntensity = Easing.EaseOutCubic(_glitchTimer / _glitchDuration);
                }
                _crtEffect.Parameters["ImpactGlitchIntensity"]?.SetValue(glitchIntensity);

                _crtEffect.CurrentTechnique.Passes[0].Apply();
            }

            var fullScreenRect = new Rectangle(0, 0, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
            _spriteBatch.Draw(_finalCompositeTarget, fullScreenRect, Color.White);
            _spriteBatch.End();

            // --- Phase 4: Draw UI elements that should NOT have the shader applied ---
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _sceneManager.DrawOverlay(_spriteBatch, _defaultFont, gameTime);

            if (_debugConsole.IsVisible)
            {
                _debugConsole.Draw(_spriteBatch, _defaultFont, gameTime);
            }

            if (_defaultFont != null)
            {
                string versionText = $"v{Global.GAME_VERSION}";
                float padding = 5f;
                var screenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
                var versionPosition = new Vector2(padding, screenHeight - _defaultFont.LineHeight - padding);
                _spriteBatch.DrawStringSnapped(_defaultFont, versionText, versionPosition, _global.Palette_DarkGray);
            }
            _spriteBatch.End();

            // Draw the custom cursor on top of everything, in virtual space.
            _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: Matrix.Invert(_mouseTransformMatrix));
            _cursorManager.Draw(_spriteBatch);
            _spriteBatch.End();

            // Draw the debug mouse dot last, so it's on top of even the cursor.
            if (_drawMouseDebugDot)
            {
                _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp, transformMatrix: Matrix.Invert(_mouseTransformMatrix));
                var virtualMousePos = TransformMouse(Mouse.GetState().Position);
                _spriteBatch.Draw(_pixel, virtualMousePos, Color.Red);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }


        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        /// <summary>
        /// Recalculates the rendering scale and position when the window is resized.
        /// </summary>
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
            else
            {
                _isCustomResolutionSavePending = false;
            }

            // Update the settings object with the new actual resolution from the window
            _settings.Resolution = newResolution;

            // If the settings scene is active, tell it to refresh its state to show the new resolution
            if (_sceneManager.CurrentActiveScene is SettingsScene settingsScene)
            {
                settingsScene.RefreshUIFromSettings();
            }

            var screenWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var screenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;

            float scaleX = (float)screenWidth / Global.VIRTUAL_WIDTH;
            float scaleY = (float)screenHeight / Global.VIRTUAL_HEIGHT;

            if (screenWidth < Global.VIRTUAL_WIDTH || screenHeight < Global.VIRTUAL_HEIGHT)
            {
                _finalScale = Math.Min(scaleX, scaleY);
            }
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

            // Recreate the full-screen render target to match the new window size
            _finalCompositeTarget?.Dispose();
            _finalCompositeTarget = new RenderTarget2D(
                GraphicsDevice,
                Window.ClientBounds.Width,
                Window.ClientBounds.Height,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);
        }

        /// <summary>
        /// Transforms mouse coordinates from screen space to 'virtual' game space.
        /// </summary>
        public static Vector2 TransformMouse(Point screenPoint)
        {
            var coreInstance = ServiceLocator.Get<Core>();
            var transformed = Vector2.Transform(screenPoint.ToVector2(), coreInstance.MouseTransformMatrix);
            return new Vector2(MathF.Round(transformed.X), MathF.Round(transformed.Y));
        }

        /// <summary>
        /// Transforms coordinates from 'virtual' game space to screen space.
        /// </summary>
        public static Point TransformVirtualToScreen(Point virtualPoint)
        {
            var coreInstance = ServiceLocator.Get<Core>();
            var toScreenMatrix = Matrix.Invert(coreInstance.MouseTransformMatrix);
            var screenVector = Vector2.Transform(virtualPoint.ToVector2(), toScreenMatrix);
            return new Point((int)screenVector.X, (int)screenVector.Y);
        }

        /// <summary>
        /// Returns a Rectangle in virtual coordinates that represents the actual visible area of the window.
        /// </summary>
        public Rectangle GetActualScreenVirtualBounds()
        {
            // Get the top-left and bottom-right corners of the actual screen in virtual coordinates.
            Vector2 topLeftVirtual = TransformMouse(new Point(0, 0));
            Vector2 bottomRightVirtual = TransformMouse(new Point(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight));

            return new Rectangle(
                (int)topLeftVirtual.X,
                (int)topLeftVirtual.Y,
                (int)(bottomRightVirtual.X - topLeftVirtual.X),
                (int)(bottomRightVirtual.Y - topLeftVirtual.Y)
            );
        }

        public void ExitApplication() => Exit();

        private void SoftResetGame()
        {
            // 1. Stop/reset all active managers and systems
            _diceRollingSystem.ClearRoll();
            _actionExecutionSystem.HandleInterruption();
            _particleSystemManager.ClearAllEmitters();
            _hapticsManager.StopAll();
            _tooltipManager.Hide();
            _loadingScreen.Clear();
            _debugConsole.ClearHistory();
            _progressionManager.ClearCurrentSplitMap();

            // 2. Completely reset the entity-component-system state
            var entityManager = ServiceLocator.Get<EntityManager>();
            var componentStore = ServiceLocator.Get<ComponentStore>();
            entityManager.Clear();
            componentStore.Clear();
            _gameState.Reset();

            // 3. Transition back to the main menu
            _sceneManager.ChangeScene(GameSceneState.MainMenu);
        }

        private bool KeyPressed(Keys key, KeyboardState current, KeyboardState previous) => current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }
}