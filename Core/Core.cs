using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input; // Added for Keyboard state
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Dice; // Added using directive
using ProjectVagabond.Particles;
using ProjectVagabond.Scenes;
using ProjectVagabond.UI;
using System;
using System.Collections.Generic; // Added for List
using System.Diagnostics;       // Added for Debug.WriteLine
using System.Text;              // Added for StringBuilder

// TODO: generate different noise maps to generate different map things
// TODO: add a way to generate different map elements based on the noise map
// TODO: make the map generation more complex, e.g. add rivers, lakes, etc.
// TODO: player customization; backgrounds, stats, bodyfat, muscle (both of which effect stat spread as well as gives buffs and needs at their extremes)
// TODO: Ctrl-Z undo previous path queued
// TODO: Make resting take random time (full rest between 6 and 11 hours)
// TODO: Wait command: (wait 3 hours 2 minutes)
// TODO: Brainstorm a way to add POIs (think rust, darkwood, the long dark, tarkov)
// TODO: Add a way to save and load the game state
// TODO: Impliment dialogue logic into dialogue scene
// TODO: Impliment combat logic into combat scene
// TODO: Convert display to have larger map, smaller terminal, with the terminal input being hidden unless the player presses ~ to "open console"
// TODO: Finish entity implimentation
// TODO: Make "activeStatusEffectcomponent" a component that can be added to any entity, not just the player
// TODO: Add a 1d8, 1d10, 1d12, and 1d20 to the dice rolling system; one at a time since its complex to impliment
// TODO: Make the loading screen use the same display as the health bar
// TODO: Make it so the game checks if a die is still moving before rerolling it, but if its moving outside of the bounds of the screen, then reroll, and on top of that have a 20 second timeout for rerolling dice that are still moving just in case the physics engine gets stuck
// TODO: Make hit makrer number that appears on map entity
// TODO: Speed up scrolling background and dice to match speed up

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

        public Matrix MouseTransformMatrix => _mouseTransformMatrix;

        // Managers & Systems
        private Global _global;
        private GameSettings _settings;
        private SceneManager _sceneManager;
        private HapticsManager _hapticsManager;
        private SystemManager _systemManager;
        private TooltipManager _tooltipManager;
        private GameState _gameState;
        private ActionExecutionSystem _actionExecutionSystem;
        private AISystem _aiSystem;
        private CombatProcessingSystem _combatProcessingSystem;
        private SpriteManager _spriteManager;
        private CombatUIAnimationManager _combatUIAnimationManager;
        private LocalMapTurnSystem _localMapTurnSystem;
        private InterpolationSystem _interpolationSystem;
        private CombatInitiationSystem _combatInitiationSystem;
        private ParticleSystemManager _particleSystemManager;
        private DiceRollingSystem _diceRollingSystem;
        private BackgroundManager _backgroundManager;

        // Input State
        private KeyboardState _previousKeyboardState;

        // Physics Timestep
        private float _physicsTimeAccumulator = 0f;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            _graphics.PreferredBackBufferWidth = Global.VIRTUAL_WIDTH;
            _graphics.PreferredBackBufferHeight = Global.VIRTUAL_HEIGHT;
            Window.AllowUserResizing = false;
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

            var worldClockManager = new WorldClockManager();
            ServiceLocator.Register<WorldClockManager>(worldClockManager);



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

            _combatUIAnimationManager = new CombatUIAnimationManager();
            _combatUIAnimationManager.RegisterAnimation("TargetSelector", new PulsingAnimation(duration: 0.4f));
            _combatUIAnimationManager.RegisterAnimation("TurnIndicator", new BobbingAnimation(speed: 5f, amount: 1f));
            ServiceLocator.Register<CombatUIAnimationManager>(_combatUIAnimationManager);

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

            _gameState = new GameState(noiseManager, componentStore, worldClockManager, chunkManager, _global, _spriteManager);
            ServiceLocator.Register<GameState>(_gameState);

            var playerInputSystem = new PlayerInputSystem();
            ServiceLocator.Register<PlayerInputSystem>(playerInputSystem);

            _localMapTurnSystem = new LocalMapTurnSystem();
            ServiceLocator.Register<LocalMapTurnSystem>(_localMapTurnSystem);

            _actionExecutionSystem = new ActionExecutionSystem();
            ServiceLocator.Register<ActionExecutionSystem>(_actionExecutionSystem);

            var combatTurnSystem = new CombatTurnSystem();
            ServiceLocator.Register<CombatTurnSystem>(combatTurnSystem);

            _aiSystem = new AISystem();
            ServiceLocator.Register<AISystem>(_aiSystem);

            var combatResolutionSystem = new CombatResolutionSystem();
            ServiceLocator.Register<CombatResolutionSystem>(combatResolutionSystem);

            _combatProcessingSystem = new CombatProcessingSystem();
            ServiceLocator.Register<CombatProcessingSystem>(_combatProcessingSystem);

            _combatInitiationSystem = new CombatInitiationSystem();
            ServiceLocator.Register<CombatInitiationSystem>(_combatInitiationSystem);

            var statusEffectSystem = new StatusEffectSystem();
            ServiceLocator.Register<StatusEffectSystem>(statusEffectSystem);
            worldClockManager.OnTimePassed += statusEffectSystem.ProcessTimePassed;

            var energySystem = new EnergySystem();
            ServiceLocator.Register<EnergySystem>(energySystem);

            _interpolationSystem = new InterpolationSystem();
            ServiceLocator.Register<InterpolationSystem>(_interpolationSystem);

            var terminalRenderer = new TerminalRenderer();
            ServiceLocator.Register<TerminalRenderer>(terminalRenderer);

            var mapRenderer = new MapRenderer();
            ServiceLocator.Register<MapRenderer>(mapRenderer);

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
            _systemManager.RegisterSystem(_localMapTurnSystem, 0f);
            _systemManager.RegisterSystem(_combatInitiationSystem, 0f);
            _systemManager.RegisterSystem(_aiSystem, 0f);
            _systemManager.RegisterSystem(energySystem, 0f);

            _sceneManager.AddScene(GameSceneState.MainMenu, new MainMenuScene());
            _sceneManager.AddScene(GameSceneState.TerminalMap, new TerminalMapScene());
            _sceneManager.AddScene(GameSceneState.Settings, new SettingsScene());
            _sceneManager.AddScene(GameSceneState.Dialogue, new DialogueScene());

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

            _spriteManager.LoadSpriteContent();
            _backgroundManager.LoadContent();
            ServiceLocator.Get<ItemManager>().LoadWeapons("Content/Items/Weapons");
            _diceRollingSystem.Initialize(GraphicsDevice, Content);
            _diceRollingSystem.OnRollCompleted += HandleRollCompleted; // Subscribe to the new event
            ServiceLocator.Get<ArchetypeManager>().LoadArchetypes("Content/Archetypes");
            _gameState.InitializeWorld();
            _gameState.InitializeRenderableEntities();

            _sceneManager.ChangeScene(GameSceneState.MainMenu, fade_duration: 0.5f);
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

            // This ensures physics calculations are stable and not dependent on the frame rate.
            // We multiply by the simulation speed to "fast forward" the physics time.
            float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _physicsTimeAccumulator += elapsedSeconds * _global.DiceSimulationSpeedMultiplier;

            while (_physicsTimeAccumulator >= Global.FIXED_PHYSICS_TIMESTEP)
            {
                // Run a single, fixed-step physics update for any relevant systems.
                _diceRollingSystem.PhysicsStep(Global.FIXED_PHYSICS_TIMESTEP);

                _physicsTimeAccumulator -= Global.FIXED_PHYSICS_TIMESTEP;
            }

            // --- Frame-Rate Dependent Updates ---
            _sceneManager.Update(gameTime);
            _tooltipManager.Update(gameTime); // Tooltips should always update.
            _particleSystemManager.Update(gameTime);
            _diceRollingSystem.Update(gameTime); // Update dice visuals and game logic every frame.
            _backgroundManager.Update(gameTime);

            // These systems handle visual updates and should be paused.
            if (!_gameState.IsPaused)
            {
                _combatUIAnimationManager.Update(gameTime);
                _interpolationSystem.Update(gameTime);
            }

            // This system handles core logic that must run even when paused (to handle interruptions).
            _combatInitiationSystem.Update(gameTime);

            // These systems handle game logic and should be paused.
            if (!_gameState.IsPaused)
            {
                if (_gameState.IsInCombat)
                {
                    _combatProcessingSystem.Update(gameTime);
                }
                else if (_sceneManager.CurrentActiveScene is TerminalMapScene)
                {
                    _gameState.UpdateActiveEntities();
                    _systemManager.Update(gameTime);
                }
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            // --- 1. Draw the main 2D scene to a render target ---
            GraphicsDevice.SetRenderTarget(_renderTarget);
            GraphicsDevice.Clear(Color.Transparent);

            // Draw the tiling background first, inside the render target.
            _spriteBatch.Begin(samplerState: SamplerState.LinearWrap);
            _backgroundManager.Draw(_spriteBatch);
            _spriteBatch.End();

            _sceneManager.Draw(_spriteBatch, _defaultFont, gameTime);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _tooltipManager.Draw(_spriteBatch, _defaultFont);
            _spriteBatch.End();

            // --- 2. Draw the 3D dice to their own render target ---
            var diceTexture = _diceRollingSystem.Draw(_defaultFont);

            // --- 3. Draw everything to the screen ---
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black); // Use a solid color for letterboxing

            _sceneManager.DrawUnderlay(_spriteBatch, _defaultFont, gameTime);

            var finalSamplerState = _useLinearSampling ? SamplerState.LinearClamp : SamplerState.PointClamp;
            Matrix shakeMatrix = _hapticsManager.GetHapticsMatrix();

            // Draw the main 2D scene
            _spriteBatch.Begin(samplerState: finalSamplerState, transformMatrix: shakeMatrix);
            _spriteBatch.Draw(_renderTarget, _finalRenderRectangle, Color.White);
            _spriteBatch.End();

            // Draw the dice texture on top of the 2D scene
            if (diceTexture != null)
            {
                _spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: finalSamplerState, transformMatrix: shakeMatrix);
                _spriteBatch.Draw(diceTexture, _finalRenderRectangle, Color.White);
                _spriteBatch.End();
            }

            _sceneManager.DrawOverlay(_spriteBatch, _defaultFont, gameTime);

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

        public void ExitApplication() => Exit();
    }
}