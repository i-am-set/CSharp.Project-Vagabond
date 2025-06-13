using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectVagabond.Scenes; // Add this using statement
using System;

// TODO: generate different noise maps to generate different map things
// TODO: add a way to generate different map elements based on the noise map
// TODO: make the map generation more complex, e.g. add rivers, lakes, etc.
// TODO: world time mechanic
// TODO: player customization; backgrounds, stats, bodyfat, muscle (both of which effect stat spread as well as gives buffs and needs at their extremes)
// TODO: Ctrl-Z undo previous path queued
// TODO: Make resting take random time (full rest between 6 and 11 hours)
// TODO: Add menu state machine (terminal/map, dialogue, combat, settings, mainmenu)
// TODO: Wait command: (wait 3 hours 2 minutes)

namespace ProjectVagabond
{
    public class Core : Game
    {
        // Singleton logic //
        public static Core Instance { get; private set; }

        // Class references //
        private static readonly GameState _gameState = new();
        private static readonly SpriteManager _spriteManager = new();
        private static readonly TextureFactory _textureFactory = new();
        private static readonly InputHandler _inputHandler = new();
        private static readonly MapRenderer _mapRenderer = new();
        private static readonly TerminalRenderer _terminalRenderer = new();
        private static readonly AutoCompleteManager _autoCompleteManager = new();
        private static readonly CommandProcessor _commandProcessor = new();
        private static readonly StatsRenderer _statsRenderer = new();
        private static readonly WorldClockManager _worldClockManager = new();
        private static readonly HapticsManager _hapticsManager = new();
        private static readonly SceneManager _sceneManager = new();
        public static readonly GameSettings _settings = new();

        // Public references //
        public static GameState CurrentGameState => _gameState;
        public static MapRenderer CurrentMapRenderer => _mapRenderer;
        public static SpriteManager CurrentSpriteManager => _spriteManager;
        public static TextureFactory CurrentTextureFactory => _textureFactory;
        public static AutoCompleteManager CurrentAutoCompleteManager => _autoCompleteManager;
        public static TerminalRenderer CurrentTerminalRenderer => _terminalRenderer;
        public static CommandProcessor CurrentCommandProcessor => _commandProcessor;
        public static InputHandler CurrentInputHandler => _inputHandler;
        public static StatsRenderer CurrentStatsRenderer => _statsRenderer;
        public static WorldClockManager CurrentWorldClockManager => _worldClockManager;
        public static HapticsManager CurrentHapticsManager => _hapticsManager;
        public static SceneManager CurrentSceneManager => _sceneManager;
        public static GameSettings Settings => _settings;

        // New: Render target for fixed aspect ratio
        private RenderTarget2D _renderTarget;
        private Rectangle _finalRenderRectangle;
        private static Matrix _mouseTransformMatrix;

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public Core()
        {
            Global.Instance.CurrentGraphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set window size to the virtual resolution and allow resizing
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
            
            // Scene initialization
            _sceneManager.AddScene(GameSceneState.MainMenu, new MainMenuScene());
            _sceneManager.AddScene(GameSceneState.TerminalMap, new TerminalMapScene());
            _sceneManager.AddScene(GameSceneState.Settings, new SettingsScene());
            _sceneManager.AddScene(GameSceneState.Dialogue, new DialogueScene());
            _sceneManager.AddScene(GameSceneState.Combat, new CombatScene());

            // Initial calculation for the render rectangle and mouse matrix
            OnResize(null, null);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            Global.Instance.CurrentSpriteBatch = new SpriteBatch(GraphicsDevice);

            // Create the render target with the virtual resolution
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

            // Set initial scene
            _sceneManager.ChangeScene(GameSceneState.MainMenu);
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

            // Delegate update to the current scene
            _sceneManager.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            // --- Pass 1: Render the entire scene to the RenderTarget ---
            GraphicsDevice.SetRenderTarget(_renderTarget);
            GraphicsDevice.Clear(Global.Instance.GameBg);

            // Delegate draw to the current scene, which will now draw on the _renderTarget
            _sceneManager.Draw(gameTime);

            // --- Pass 2: Render the RenderTarget to the screen (back buffer) ---
            GraphicsDevice.SetRenderTarget(null);
            // Clear the back buffer to create the letterbox/pillarbox effect
            GraphicsDevice.Clear(Color.Black);

            Global.Instance.CurrentSpriteBatch.Begin(samplerState: SamplerState.PointClamp);
            // Draw the finalized scene, scaled to fit the window
            Global.Instance.CurrentSpriteBatch.Draw(_renderTarget, _finalRenderRectangle, Color.White);
            Global.Instance.CurrentSpriteBatch.End();

            base.Draw(gameTime);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        /// <summary>
        /// Recalculates the rendering scale and position when the window is resized.
        /// </summary>
        public void OnResize(object sender, EventArgs e)
        {
            if (GraphicsDevice == null) return;

            var screenWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var screenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;

            // Calculate the scale to fit the virtual resolution within the new window size
            float scaleX = (float)screenWidth / Global.VIRTUAL_WIDTH;
            float scaleY = (float)screenHeight / Global.VIRTUAL_HEIGHT;
            float scale = Math.Min(scaleX, scaleY);

            // Calculate the dimensions and position of the scaled render target
            int destWidth = (int)(Global.VIRTUAL_WIDTH * scale);
            int destHeight = (int)(Global.VIRTUAL_HEIGHT * scale);
            int destX = (screenWidth - destWidth) / 2;
            int destY = (screenHeight - destHeight) / 2;

            _finalRenderRectangle = new Rectangle(destX, destY, destWidth, destHeight);

            // Create a matrix to transform mouse coordinates from screen space to virtual space
            _mouseTransformMatrix = Matrix.Invert(Matrix.CreateTranslation(destX, destY, 0) * Matrix.CreateScale(scale));
        }

        /// <summary>
        /// Transforms mouse coordinates from screen space to virtual game space.
        /// </summary>
        public static Vector2 TransformMouse(Point screenPoint)
        {
            return Vector2.Transform(screenPoint.ToVector2(), _mouseTransformMatrix);
        }

        public static void ResizeWindow(int width, int height)
        {
            var graphics = Global.Instance.CurrentGraphics;
            graphics.PreferredBackBufferWidth = width;
            graphics.PreferredBackBufferHeight = height;
            graphics.ApplyChanges();

            Instance.OnResize(null, null);
        }

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