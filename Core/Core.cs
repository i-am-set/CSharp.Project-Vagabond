using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        private static readonly SceneManager _sceneManager = new(); // Add SceneManager

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
        public static SceneManager CurrentSceneManager => _sceneManager; // Add public accessor

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public Core()
        {
            Global.Instance.CurrentGraphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            Global.Instance.CurrentGraphics.PreferredBackBufferWidth = 1200; // Set window size
            Global.Instance.CurrentGraphics.PreferredBackBufferHeight = 800;
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        protected override void Initialize()
        {
            Instance = this;

            // Scene initialization
            _sceneManager.AddScene(GameSceneState.MainMenu, new MainMenuScene());
            _sceneManager.AddScene(GameSceneState.TerminalMap, new TerminalMapScene());
            _sceneManager.AddScene(GameSceneState.Settings, new SettingsScene());
            _sceneManager.AddScene(GameSceneState.Dialogue, new DialogueScene());
            _sceneManager.AddScene(GameSceneState.Combat, new CombatScene());

            base.Initialize();
        }

        protected override void LoadContent()
        {
            Global.Instance.CurrentSpriteBatch = new SpriteBatch(GraphicsDevice);

            try
            {
                Global.Instance.DefaultFont = Content.Load<SpriteFont>("Fonts/Px437_IBM_BIOS");
            }
            catch
            {
                throw new Exception("Please add a SpriteFont to your Content/Fonts folder");
            }

            _spriteManager.LoadSpriteContent();

            // Set initial scene
            _sceneManager.ChangeScene(GameSceneState.MainMenu);
        }

        protected override void Update(GameTime gameTime)
        {
            // Delegate update to the current scene
            _sceneManager.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Global.Instance.GameBg);

            // Delegate draw to the current scene
            _sceneManager.Draw(gameTime);

            base.Draw(gameTime);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

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