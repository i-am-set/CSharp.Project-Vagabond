using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

// TODO: Change the grid to 5x5 images instead of 8x8
// TODO: Make "freeMove" disable when path is dont executing.
// TODO: Allow for the player to cancel movement while executing using ESCAPE
namespace ProjectVagabond
{
    public class Core : Game
    {
        public static Core Instance { get; private set; }

        private static readonly GameState _gameState = new();
        private static readonly SpriteManager _spriteManager = new();
        private static readonly TextureFactory _textureFactory = new();
        private static readonly InputHandler _inputHandler = new();
        private static readonly MapRenderer _mapRenderer = new();
        private static readonly TerminalRenderer _terminalRenderer = new();
        private static readonly AutoCompleteManager _autoCompleteManager = new();
        private static readonly CommandProcessor _commandProcessor = new();

        // Public references //
        public static GameState CurrentGameState => _gameState;
        public static SpriteManager CurrentSpriteManager => _spriteManager;
        public static TextureFactory CurrentTextureFactory => _textureFactory;
        public static AutoCompleteManager CurrentAutoCompleteManager => _autoCompleteManager;
        public static TerminalRenderer CurrentTerminalRenderer => _terminalRenderer;
        public static CommandProcessor CurrentCommandProcessor => _commandProcessor;
        public static InputHandler CurrentInputHandler => _inputHandler;

        public Core()
        {
            Global.Instance.CurrentGraphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            Global.Instance.CurrentGraphics.PreferredBackBufferWidth = 1200; // Set window size
            Global.Instance.CurrentGraphics.PreferredBackBufferHeight = 800;
        }

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
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed) // Hack: Not really kosher
                Exit();

            _inputHandler.HandleInput(gameTime);
            _gameState.UpdateMovement(gameTime);

            base.Update(gameTime);
        }
        
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            Global.Instance.CurrentSpriteBatch.Begin();

            _mapRenderer.DrawMap();
            _terminalRenderer.DrawTerminal();

            Global.Instance.CurrentSpriteBatch.End();

            base.Draw(gameTime);
        }

        public void ExitApplication()
        {
            Exit();
        }
    }
}