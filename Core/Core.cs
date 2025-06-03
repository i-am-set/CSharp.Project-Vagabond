using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace ProjectVagabond
{
    public class Core : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private GameState _gameState;
        private CommandProcessor _commandProcessor;
        private InputHandler _inputHandler;
        private DisplayRenderer _displayRenderer;
        private MovementSystem _movementSystem;

        public Core()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
    
            
            _graphics.PreferredBackBufferWidth = 1200;// Set window size wdith to accommodate the layout
            _graphics.PreferredBackBufferHeight = 800;// Set window size height to accommodate the layout
        }

        protected override void Initialize()
        {
            _gameState = new GameState();
            _commandProcessor = new CommandProcessor();
            _inputHandler = new InputHandler();
            _displayRenderer = new DisplayRenderer();
            _movementSystem = new MovementSystem(_gameState);
            
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            
            try
            {
                _font = Content.Load<SpriteFont>("Fonts/Px437_IBM_Model3x_Alt1");
            }
            catch
            {
                throw new Exception("Please add a DefaultFont.spritefont file to your Content folder");
            }
            
            _displayRenderer.LoadContent(_font);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || 
                Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            _inputHandler.Update();
            
            if (_inputHandler.HasNewCommand)// Process any completed commands
            {
                string command = _inputHandler.GetCommand();
                _commandProcessor.ProcessCommand(command, _gameState, _movementSystem);
                _inputHandler.ClearCommand();
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();
            _displayRenderer.Draw(_spriteBatch, _gameState, _inputHandler.CurrentInput);
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}