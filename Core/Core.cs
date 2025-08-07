using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond;
using ProjectVagabond.Combat;
using ProjectVagabond.Combat.UI;
using ProjectVagabond.Dice;
using ProjectVagabond.Encounters;
using ProjectVagabond.Particles;
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
// TODO: Make resting take random time (full rest between 6 and 11 hours)
// TODO: Wait command: (wait 3 hours 2 minutes)
// TODO: Brainstorm a way to add POIs (think rust, darkwood, the long dark, tarkov)
// TODO: Add a way to save and load the game state
// TODO: Finish entity implimentation
// TODO: Make "activeStatusEffectcomponent" a component that can be added to any entity, not just the player
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
        private Texture2D _pixel;
        private RenderTarget2D _renderTarget;
        private Rectangle _finalRenderRectangle;
        private Matrix _mouseTransformMatrix;
        private bool _useLinearSampling;
        private Point _previousResolution;

        public Matrix MouseTransformMatrix => _mouseTransformMatrix;

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
        private AISystem _aiSystem;
        private SpriteManager _spriteManager;
        private CombatInitiationSystem _combatInitiationSystem;
        private DiceRollingSystem _diceRollingSystem;
        private BackgroundManager _backgroundManager;
        private PreEncounterAnimationSystem _preEncounterAnimationSystem;
        private LoadingScreen _loadingScreen;
        private AnimationManager _animationManager;

        // Input State
        private KeyboardState _previousKeyboardState;

        // Physics Timestep
        private float _physicsTimeAccumulator = 0f;
        private TimeSpan _scaledTotalGameTime = TimeSpan.Zero;
        private bool _isTimeSlowed = false;

        // Loading State
        private bool _isGameLoaded = false;

        // Custom Resolution Save State
        private float _customResolutionSaveTimer = 0f;
        private bool _isCustomResolutionSavePending = false;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

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
            _isTimeSlowed = _global.EnableMasterTimeScaleOnStart;

            // Phase 2: GraphicsDevice Registration
            ServiceLocator.Register<GraphicsDevice>(GraphicsDevice);

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

            var itemManager = new ItemManager();
            ServiceLocator.Register<ItemManager>(itemManager);

            var actionManager = new ActionManager();
            ServiceLocator.Register<ActionManager>(actionManager);

            var worldClockManager = new WorldClockManager();
            ServiceLocator.Register<WorldClockManager>(worldClockManager);

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

            // Encounter System Initialization
            EncounterActionRegistry.RegisterActions();
            var encounterManager = new EncounterManager();
            ServiceLocator.Register<EncounterManager>(encounterManager);

            var poiManagerSystem = new POIManagerSystem();
            ServiceLocator.Register<POIManagerSystem>(poiManagerSystem);

            // GameState must be registered before systems that depend on it in their constructor.
            _gameState = new GameState(noiseManager, componentStore, worldClockManager, chunkManager, _global, _spriteManager);
            ServiceLocator.Register<GameState>(_gameState);

            // These systems depend on GameState being available in the ServiceLocator.
            var possibleEncounterListBuilder = new PossibleEncounterListBuilder();
            ServiceLocator.Register<PossibleEncounterListBuilder>(possibleEncounterListBuilder);

            var encounterTriggerSystem = new EncounterTriggerSystem();
            ServiceLocator.Register<EncounterTriggerSystem>(encounterTriggerSystem);

            var playerInputSystem = new PlayerInputSystem();
            ServiceLocator.Register<PlayerInputSystem>(playerInputSystem);

            _actionExecutionSystem = new ActionExecutionSystem();
            ServiceLocator.Register<ActionExecutionSystem>(_actionExecutionSystem);

            var combatTurnSystem = new CombatTurnSystem();
            ServiceLocator.Register<CombatTurnSystem>(combatTurnSystem);

            _aiSystem = new AISystem();
            ServiceLocator.Register<AISystem>(_aiSystem);

            _combatInitiationSystem = new CombatInitiationSystem();
            ServiceLocator.Register<CombatInitiationSystem>(_combatInitiationSystem);

            var statusEffectSystem = new StatusEffectSystem();
            ServiceLocator.Register<StatusEffectSystem>(statusEffectSystem);
            worldClockManager.OnTimePassed += statusEffectSystem.ProcessTimePassed;

            var energySystem = new EnergySystem();
            ServiceLocator.Register<EnergySystem>(energySystem);

            var terminalRenderer = new TerminalRenderer();
            ServiceLocator.Register<TerminalRenderer>(terminalRenderer);

            var mapRenderer = new MapRenderer();
            ServiceLocator.Register<MapRenderer>(mapRenderer);

            // PreEncounterAnimationSystem depends on MapRenderer, so it must be registered after.
            _preEncounterAnimationSystem = new PreEncounterAnimationSystem();
            ServiceLocator.Register<PreEncounterAnimationSystem>(_preEncounterAnimationSystem);

            var mapInputHandler = new MapInputHandler(mapRenderer.MapContextMenu, mapRenderer);
            ServiceLocator.Register<MapInputHandler>(mapInputHandler);

            var promptRenderer = new PromptRenderer();
            ServiceLocator.Register<PromptRenderer>(promptRenderer);

            var autoCompleteManager = new AutoCompleteManager();
            ServiceLocator.Register<AutoCompleteManager>(autoCompleteManager);

            var commandProcessor = new CommandProcessor(playerInputSystem);
            ServiceLocator.Register<CommandProcessor>(commandProcessor);

            var statsRenderer = new StatsRenderer();
            ServiceLocator.Register<StatsRenderer>(statsRenderer);

            var clockRenderer = new ClockRenderer();
            ServiceLocator.Register<ClockRenderer>(clockRenderer);



            var inputHandler = new InputHandler();
            ServiceLocator.Register<InputHandler>(inputHandler);

            _sceneManager = new SceneManager();
            ServiceLocator.Register<SceneManager>(_sceneManager);

            // Phase 4: Final Setup
            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            _settings.ApplyGraphicsSettings(_graphics, this);
            _settings.ApplyGameSettings();

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            ServiceLocator.Register<Texture2D>(_pixel);

            _systemManager.RegisterSystem(_actionExecutionSystem, 0f);
            _systemManager.RegisterSystem(_combatInitiationSystem, 0f);
            _systemManager.RegisterSystem(_aiSystem, 0f);
            _systemManager.RegisterSystem(energySystem, 0f);
            _systemManager.RegisterSystem(poiManagerSystem, 0f);
            _systemManager.RegisterSystem(encounterTriggerSystem, 0f);
            _systemManager.RegisterSystem(_preEncounterAnimationSystem, 0f);

            _sceneManager.AddScene(GameSceneState.MainMenu, new MainMenuScene());
            _sceneManager.AddScene(GameSceneState.TerminalMap, new GameMapScene()); // Changed to GameMapScene
            _sceneManager.AddScene(GameSceneState.Settings, new SettingsScene());
            _sceneManager.AddScene(GameSceneState.Dialogue, new DialogueScene());
            _sceneManager.AddScene(GameSceneState.Encounter, new EncounterScene());
            _sceneManager.AddScene(GameSceneState.Combat, new CombatScene());

            _previousResolution = new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
            OnResize(null, null);
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            ServiceLocator.Register<SpriteBatch>(_spriteBatch);

            _renderTarget = new RenderTarget2D(
                GraphicsDevice,
                Global.VIRTUAL_WIDTH,
                Global.VIRTUAL_HEIGHT,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);

            try
            {
                _defaultFont = Content.Load<BitmapFont>("Fonts/Px437_IBM_BIOS");
                ServiceLocator.Register<BitmapFont>(_defaultFont);
            }
            catch
            {
                throw new Exception("Please add a BitmapFont to your 'Content/Fonts' folder");
            }

            // Load only essential assets needed for the main menu and global UI.
            _spriteManager.LoadEssentialContent();
            _backgroundManager.LoadContent();

            // The rest of the loading is deferred until the player clicks "Play".

            // Set the initial scene to the main menu.
            _sceneManager.ChangeScene(GameSceneState.MainMenu);
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

            // Handle debug input
            KeyboardState currentKeyboardState = Keyboard.GetState();
            if (currentKeyboardState.IsKeyDown(Keys.F1) && _previousKeyboardState.IsKeyUp(Keys.F1))
            {
                _diceRollingSystem.DebugShowColliders = !_diceRollingSystem.DebugShowColliders;
            }
            if (currentKeyboardState.IsKeyDown(Keys.F4) && _previousKeyboardState.IsKeyUp(Keys.F4))
            {
                _isTimeSlowed = !_isTimeSlowed;
            }
            if (currentKeyboardState.IsKeyDown(Keys.F5) && _previousKeyboardState.IsKeyUp(Keys.F5))
            {
                var tasks = new List<LoadingTask> { new DelayTask(0.15f) };
                _sceneManager.ChangeScene(GameSceneState.Combat, tasks);
            }

            // Use F2 to trigger a sample grouped dice roll for demonstration.
            if (currentKeyboardState.IsKeyDown(Keys.F2) && _previousKeyboardState.IsKeyUp(Keys.F2))
            {
                // 1. Define the groups for the roll.
                var rollRequest = new List<DiceGroup>
                {
                    new DiceGroup
                    {
                        GroupId = "damage",
                        NumberOfDice = 2,
                        Tint = Color.Red,
                        ResultProcessing = DiceResultProcessing.Sum,
                        DieType = DieType.D6, // Explicitly a D6
                        Scale = 1.0f // Normal size
                    },
                    new DiceGroup
                    {
                        GroupId = "status_effect",
                        NumberOfDice = 1,
                        Tint = Color.Blue,
                        ResultProcessing = DiceResultProcessing.IndividualValues,
                        DieType = DieType.D6, // Explicitly a D6
                        Scale = 0.6f // Smaller die
                    },
                    new DiceGroup
                    {
                        GroupId = "poison_damage",
                        NumberOfDice = 1,
                        Tint = Color.Green,
                        ResultProcessing = DiceResultProcessing.Sum,
                        DieType = DieType.D4,
                        Scale = 1.0f
                    }
                };

                // 2. Call the roll method with the request.
                _diceRollingSystem.Roll(rollRequest);
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

            // Create a scaled GameTime object for slow-motion debugging.
            float currentTimeScale = _isTimeSlowed ? _global.MasterTimeScale : 1.0f;
            var scaledElapsedTime = TimeSpan.FromTicks((long)(gameTime.ElapsedGameTime.Ticks * currentTimeScale));
            _scaledTotalGameTime += scaledElapsedTime;
            var scaledGameTime = new GameTime(_scaledTotalGameTime, scaledElapsedTime);

            // The loading screen now acts as a modal state that can be triggered at any time.
            if (_loadingScreen.IsActive)
            {
                _loadingScreen.Update(scaledGameTime);
                _sceneManager.Update(scaledGameTime); // Allow scene manager to update its transition state
                // The dice system might be warming up, so it needs updates.
                if (!_isGameLoaded)
                {
                    _diceRollingSystem.Update(scaledGameTime);
                }
                return; // Block all other game updates
            }

            // This ensures physics calculations are stable and not dependent on the frame rate.
            // We multiply by the simulation speed to "fast forward" the physics time.
            float elapsedSeconds = (float)scaledGameTime.ElapsedGameTime.TotalSeconds;
            _physicsTimeAccumulator += elapsedSeconds * _global.DiceSimulationSpeedMultiplier;

            while (_physicsTimeAccumulator >= Global.FIXED_PHYSICS_TIMESTEP)
            {
                // Run a single, fixed-step physics update for any relevant systems.
                _diceRollingSystem.PhysicsStep(Global.FIXED_PHYSICS_TIMESTEP);

                _physicsTimeAccumulator -= Global.FIXED_PHYSICS_TIMESTEP;
            }

            // --- Frame-Rate Dependent Updates ---
            _sceneManager.Update(scaledGameTime);
            _tooltipManager.Update(scaledGameTime); // Tooltips should always update.
            _particleSystemManager.Update(scaledGameTime);
            _diceRollingSystem.Update(scaledGameTime); // Update dice visuals and game logic every frame.
            _backgroundManager.Update(scaledGameTime);
            _animationManager.Update(scaledGameTime);

            // This system handles core logic that must run even when paused (to handle interruptions).
            _combatInitiationSystem.Update(scaledGameTime);

            // These systems handle game logic and should be paused.
            if (!_gameState.IsPaused)
            {
                if (_gameState.IsInCombat)
                {
                }
                else if (_sceneManager.CurrentActiveScene is GameMapScene)
                {
                    _gameState.UpdateActiveEntities();
                    _systemManager.Update(scaledGameTime);
                }
            }

            base.Update(gameTime); // Base.Update uses the unscaled gameTime for window events.
        }

        protected override void Draw(GameTime gameTime)
        {
            bool useLetterboxing = _sceneManager.CurrentActiveScene?.UsesLetterboxing ?? true;

            Matrix screenScaleMatrix = Matrix.Invert(_mouseTransformMatrix);
            Matrix shakeMatrix = _hapticsManager.GetHapticsMatrix();
            var finalSamplerState = _useLinearSampling ? SamplerState.LinearClamp : SamplerState.PointClamp;

            // --- Phase 1: Render virtual-space content ---
            if (useLetterboxing)
            {
                // For letterboxed scenes, draw everything to a single render target first.
                GraphicsDevice.SetRenderTarget(_renderTarget);
                GraphicsDevice.Clear(Color.Transparent);

                _sceneManager.Draw(_spriteBatch, _defaultFont, gameTime, Matrix.Identity);

                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _tooltipManager.Draw(_spriteBatch, _defaultFont);
                _spriteBatch.End();
            }

            var diceTexture = _diceRollingSystem.Draw(_defaultFont);

            // --- Phase 2: Composite everything to the backbuffer ---
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin(samplerState: SamplerState.LinearWrap);
            if (!_sceneManager.IsLoadingBetweenScenes)
            {
                _backgroundManager.Draw(_spriteBatch, GraphicsDevice.PresentationParameters.Bounds);
            }
            _spriteBatch.End();

            _sceneManager.DrawUnderlay(_spriteBatch, _defaultFont, gameTime);

            // --- Draw the main content block ---
            if (useLetterboxing)
            {
                _spriteBatch.Begin(samplerState: finalSamplerState, transformMatrix: shakeMatrix);
                _spriteBatch.Draw(_renderTarget, _finalRenderRectangle, Color.White);
                _spriteBatch.End();

                if (diceTexture != null)
                {
                    _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: finalSamplerState, transformMatrix: shakeMatrix);
                    _spriteBatch.Draw(diceTexture, _finalRenderRectangle, Color.White);
                    _spriteBatch.End();
                }
            }
            else // Fullscreen rendering (e.g., Combat)
            {
                Matrix fullScreenTransform = shakeMatrix * screenScaleMatrix;
                _sceneManager.Draw(_spriteBatch, _defaultFont, gameTime, fullScreenTransform);

                if (diceTexture != null)
                {
                    _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: finalSamplerState, transformMatrix: fullScreenTransform);
                    _spriteBatch.Draw(diceTexture, new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT), Color.White);
                    _spriteBatch.End();
                }

                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: fullScreenTransform);
                _tooltipManager.Draw(_spriteBatch, _defaultFont);
                _spriteBatch.End();
            }

            Matrix particleTransform = useLetterboxing ? shakeMatrix : shakeMatrix * screenScaleMatrix;
            _particleSystemManager.Draw(_spriteBatch, particleTransform);

            _sceneManager.DrawOverlay(_spriteBatch, _defaultFont, gameTime);

            if (_loadingScreen.IsActive)
            {
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _loadingScreen.Draw(_spriteBatch, _defaultFont, GraphicsDevice.PresentationParameters.Bounds);
                _spriteBatch.End();
            }

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            if (_defaultFont != null)
            {
                string versionText = $"v{Global.GAME_VERSION}";
                float padding = 5f;
                var screenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
                var versionPosition = new Vector2(padding, screenHeight - _defaultFont.LineHeight - padding);
                _spriteBatch.DrawString(_defaultFont, versionText, versionPosition, _global.Palette_DarkGray);
            }
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        /// <summary>
        /// Recalculates the rendering scale and position when the window is resized.
        /// If the window is smaller than the virtual resolution, it scales down to fit.
        /// Otherwise, it uses integer scaling for a crisp, pixel-perfect look.
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

            float finalScale;

            if (screenWidth < Global.VIRTUAL_WIDTH || screenHeight < Global.VIRTUAL_HEIGHT)
            {
                finalScale = Math.Min(scaleX, scaleY);
                _useLinearSampling = true;
            }
            else
            {
                int integerScale = (int)Math.Min(scaleX, scaleY);
                if (_settings.SmallerUi) integerScale--;
                finalScale = Math.Max(1, integerScale);
                _useLinearSampling = false;
            }

            int destWidth = (int)(Global.VIRTUAL_WIDTH * finalScale);
            int destHeight = (int)(Global.VIRTUAL_HEIGHT * finalScale);

            int destX = (screenWidth - destWidth) / 2;
            int destY = (screenHeight - destHeight) / 2;

            _finalRenderRectangle = new Rectangle(destX, destY, destWidth, destHeight);

            _mouseTransformMatrix = Matrix.CreateTranslation(-destX, -destY, 0) * Matrix.CreateScale(1.0f / finalScale);
        }

        /// <summary>
        /// Transforms mouse coordinates from screen space to 'virtual' game space.
        /// </summary>
        public static Vector2 TransformMouse(Point screenPoint)
        {
            var coreInstance = ServiceLocator.Get<Core>();
            return Vector2.Transform(screenPoint.ToVector2(), coreInstance.MouseTransformMatrix);
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
        /// This accounts for letterboxing or pillarboxing applied during scaling.
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
    }
}
