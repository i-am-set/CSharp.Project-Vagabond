using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Timers;
using ProjectVagabond.Scenes;
using System;
using System.Collections.Generic;

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
// TODO: Add settings ui in game menu corner

namespace ProjectVagabond
{
    public class Core : Game
    {
        // Singleton logic //
        public static Core Instance { get; private set; }

        // Class references //
        // --- Core ECS and Systems must be initialized first, as other managers may depend on them. ---
        private static readonly EntityManager _entityManager = new();
        private static readonly ComponentStore _componentStore = new();
        private static readonly ChunkManager _chunkManager = new();
        private static readonly SystemManager _systemManager = new();
        private static readonly PlayerInputSystem _playerInputSystem = new();
        private static readonly ActionExecutionSystem _actionExecutionSystem = new();
        private static readonly AISystem _aiSystem = new();

        // --- GameState can now be initialized safely as its dependencies are ready. ---
        private static readonly GameState _gameState = new();

        // --- Other managers and renderers ---
        private static readonly SpriteManager _spriteManager = new();
        private static readonly TextureFactory _textureFactory = new();
        private static readonly InputHandler _inputHandler = new();
        private static readonly MapRenderer _mapRenderer = new();
        private static readonly MapInputHandler _mapInputHandler = new(_mapRenderer.MapContextMenu, _mapRenderer);
        private static readonly TerminalRenderer _terminalRenderer = new();
        private static readonly AutoCompleteManager _autoCompleteManager = new();
        private static readonly CommandProcessor _commandProcessor = new(_playerInputSystem);
        private static readonly StatsRenderer _statsRenderer = new();
        private static readonly WorldClockManager _worldClockManager = new();
        private static readonly ClockRenderer _clockRenderer = new();
        private static readonly HapticsManager _hapticsManager = new();
        private static readonly SceneManager _sceneManager = new();
        private static readonly TooltipManager _tooltipManager = new();
        public static readonly GameSettings _settings = SettingsManager.LoadSettings();

        // Misc //
        private RenderTarget2D _renderTarget;
        private Rectangle _finalRenderRectangle;
        private static Matrix _mouseTransformMatrix;
        private static Texture2D _pixel;
        private bool _useLinearSampling;

        // Public references //
        public static GameState CurrentGameState => _gameState;
        public static MapRenderer CurrentMapRenderer => _mapRenderer;
        public static MapInputHandler CurrentMapInputHandler => _mapInputHandler;
        public static SpriteManager CurrentSpriteManager => _spriteManager;
        public static TextureFactory CurrentTextureFactory => _textureFactory;
        public static AutoCompleteManager CurrentAutoCompleteManager => _autoCompleteManager;
        public static TerminalRenderer CurrentTerminalRenderer => _terminalRenderer;
        public static CommandProcessor CurrentCommandProcessor => _commandProcessor;
        public static InputHandler CurrentInputHandler => _inputHandler;
        public static StatsRenderer CurrentStatsRenderer => _statsRenderer;
        public static ClockRenderer CurrentClockRenderer => _clockRenderer;
        public static WorldClockManager CurrentWorldClockManager => _worldClockManager;
        public static HapticsManager CurrentHapticsManager => _hapticsManager;
        public static SceneManager CurrentSceneManager => _sceneManager;
        public static TooltipManager CurrentTooltipManager => _tooltipManager;
        public static GameSettings Settings => _settings;
        public static Texture2D Pixel => _pixel;
        public static EntityManager EntityManager => _entityManager;
        public static ComponentStore ComponentStore => _componentStore;
        public static ChunkManager ChunkManager => _chunkManager;
        public static SystemManager SystemManager => _systemManager;
        public static PlayerInputSystem PlayerInputSystem => _playerInputSystem;
        public static ActionExecutionSystem ActionExecutionSystem => _actionExecutionSystem;
        public static AISystem AISystem => _aiSystem;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public Core()
        {
            Global.Instance.CurrentGraphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            Global.Instance.CurrentGraphics.PreferredBackBufferWidth = Global.VIRTUAL_WIDTH;
            Global.Instance.CurrentGraphics.PreferredBackBufferHeight = Global.VIRTUAL_HEIGHT;
            Window.AllowUserResizing = false;
            Window.ClientSizeChanged += OnResize;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        protected override void Initialize()
        {
            Instance = this;

            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

            Settings.ApplyGraphicsSettings(Global.Instance.CurrentGraphics, this);
            Settings.ApplyGameSettings();

            _pixel = new Texture2D(GraphicsDevice, 1, 1);

            // Register systems with their update frequencies
            _systemManager.RegisterSystem(_actionExecutionSystem, 0f); // Runs every frame

            // Subscribe the AISystem to the time-passing event
            _worldClockManager.OnTimePassed += _aiSystem.ProcessEntities;

            _sceneManager.AddScene(GameSceneState.MainMenu, new MainMenuScene());
            _sceneManager.AddScene(GameSceneState.TerminalMap, new TerminalMapScene());
            _sceneManager.AddScene(GameSceneState.Settings, new SettingsScene());
            _sceneManager.AddScene(GameSceneState.Dialogue, new DialogueScene());
            _sceneManager.AddScene(GameSceneState.Combat, new CombatScene());

            OnResize(null, null);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            Global.Instance.CurrentSpriteBatch = new SpriteBatch(GraphicsDevice);

            _renderTarget = new RenderTarget2D(
                GraphicsDevice,
                Global.VIRTUAL_WIDTH,
                Global.VIRTUAL_HEIGHT,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);

            try
            {
                Global.Instance.DefaultFont = Content.Load<BitmapFont>("Fonts/Px437_IBM_BIOS");
            }
            catch
            {
                throw new Exception("Please add a BitmapFont to your Content/Fonts folder");
            }

            _spriteManager.LoadSpriteContent();

            _sceneManager.ChangeScene(GameSceneState.MainMenu, fade_duration: 0.5f);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Settings.IsFrameLimiterEnabled)
            {
                IsFixedTimeStep = true;
                TargetElapsedTime = TimeSpan.FromSeconds(1.0 / Settings.TargetFramerate);
            }
            else
            {
                IsFixedTimeStep = false;
            }
            Global.Instance.CurrentGraphics.SynchronizeWithVerticalRetrace = Settings.IsVsync;

            _sceneManager.Update(gameTime);

            if (_sceneManager.CurrentActiveScene is TerminalMapScene)
            {
                CurrentGameState.UpdateActiveEntities();
                _systemManager.Update(gameTime);
            }

            _tooltipManager.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(_renderTarget);
            GraphicsDevice.Clear(Color.Transparent);

            _sceneManager.Draw(gameTime);

            Global.Instance.CurrentSpriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _tooltipManager.Draw(Global.Instance.CurrentSpriteBatch);
            Global.Instance.CurrentSpriteBatch.End();

            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Global.Instance.GameBg);

            _sceneManager.DrawUnderlay(gameTime);

            var finalSamplerState = _useLinearSampling ? SamplerState.LinearClamp : SamplerState.PointClamp;

            Global.Instance.CurrentSpriteBatch.Begin(samplerState: finalSamplerState);
            Global.Instance.CurrentSpriteBatch.Draw(_renderTarget, _finalRenderRectangle, Color.White);
            Global.Instance.CurrentSpriteBatch.End();

            _sceneManager.DrawOverlay(gameTime);

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
                if (Settings.SmallerUi) integerScale--;
                finalScale = Math.Max(1, integerScale);
                _useLinearSampling = false;
            }

            int destWidth = (int)(Global.VIRTUAL_WIDTH * finalScale);
            int destHeight = (int)(Global.VIRTUAL_HEIGHT * finalScale);

            int destX = (screenWidth - destWidth) / 2;
            int destY = (screenHeight - destHeight) / 2;

            _finalRenderRectangle = new Rectangle(destX, destY, destWidth, destHeight);

            _mouseTransformMatrix = Matrix.CreateTranslation(-destX, -destY, 0) *
                                      Matrix.CreateScale(1.0f / finalScale);
        }

        /// <summary>
        /// Transforms mouse coordinates from screen space to 'virtual' game space.
        /// </summary>
        public static Vector2 TransformMouse(Point screenPoint)
        {
            return Vector2.Transform(screenPoint.ToVector2(), _mouseTransformMatrix);
        }

        /// <summary>
        /// Transforms coordinates from 'virtual' game space to screen space.
        /// </summary>
        public static Point TransformVirtualToScreen(Point virtualPoint)
        {
            // We need the inverse of _mouseTransformMatrix to go from virtual to screen.
            var toScreenMatrix = Matrix.Invert(_mouseTransformMatrix);
            var screenVector = Vector2.Transform(virtualPoint.ToVector2(), toScreenMatrix);
            return new Point((int)screenVector.X, (int)screenVector.Y);
        }

        /// <summary>
        /// Helper method to resize the game window.
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public static void ResizeWindow(int width, int height)
        {
            var graphics = Global.Instance.CurrentGraphics;
            graphics.PreferredBackBufferWidth = width;
            graphics.PreferredBackBufferHeight = height;
            graphics.ApplyChanges();

            Instance.OnResize(null, null);
        }

        /// <summary>
        /// Helper method to exit the application.
        /// </summary>
        public void ExitApplication() => Exit();

        public static void ScreenShake(float intensity, float duration) =>
            _hapticsManager.TriggerShake(intensity, duration);

        public static void ScreenHop(float intensity, float duration) =>
            _hapticsManager.TriggerHop(intensity, duration);



        public static void ScreenPulse(float intensity, float duration) =>
            _hapticsManager.TriggerPulse(intensity, duration);

        public static void ScreenWobble(float intensity, float duration, float frequency = 5f) =>
            _hapticsManager.TriggerWobble(intensity, duration, frequency);

        public static void ScreenDrift(Vector2 direction, float intensity, float duration) =>
            _hapticsManager.TriggerDrift(direction, intensity, duration);

        public static void ScreenBounce(Vector2 direction, float intensity, float duration) =>
            _hapticsManager.TriggerBounce(direction, intensity, duration);

        public static void ScreenRandomHop(float intensity, float duration) =>
            _hapticsManager.TriggerRandomHop(intensity, duration);

        public static void StopAllHaptics() => _hapticsManager.StopAll();
    }
}