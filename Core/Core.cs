using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

// TODO: impliment programmer art for the map elements
// TODO: generate different noise maps to generate different map things
// TODO: add a way to generate different map elements based on the noise map
// TODO: add health and energy mechanics 50%/100% (allow the values to be changed with methods)
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

        // Public references //
        public static GameState CurrentGameState => _gameState;
        public static SpriteManager CurrentSpriteManager => _spriteManager;
        public static TextureFactory CurrentTextureFactory => _textureFactory;
        public static AutoCompleteManager CurrentAutoCompleteManager => _autoCompleteManager;
        public static TerminalRenderer CurrentTerminalRenderer => _terminalRenderer;
        public static CommandProcessor CurrentCommandProcessor => _commandProcessor;
        public static InputHandler CurrentInputHandler => _inputHandler;
        public static StatsRenderer CurrentStatsRenderer => _statsRenderer;

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
        }

        protected override void Update(GameTime gameTime)
        {
            _inputHandler.HandleInput(gameTime);
            _gameState.UpdateMovement(gameTime);
            _statsRenderer.Update(gameTime);

            base.Update(gameTime);
        }
        
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Global.Instance.GameBg);

            Global.Instance.CurrentSpriteBatch.Begin();


            _mapRenderer.DrawMap();
            _terminalRenderer.DrawTerminal();
            _statsRenderer.DrawStats();

            Global.Instance.CurrentSpriteBatch.End();

            base.Draw(gameTime);
        }

        // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- //

        public void ExitApplication()
        {
            Exit();
        }
    }
}