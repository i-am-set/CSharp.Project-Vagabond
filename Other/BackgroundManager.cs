using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;

namespace ProjectVagabond
{
    public class BackgroundManager
    {
        private Texture2D _texture;
        private Vector2 _offset;
        private readonly Core _core;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly WorldClockManager _worldClockManager;

        public Vector2 ScrollDirection { get; set; } = new Vector2(0.1f, 0.1f);
        public float ScrollSpeed { get; set; } = 5f;

        public BackgroundManager()
        {
            _core = ServiceLocator.Get<Core>();
            _graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
            _worldClockManager = ServiceLocator.Get<WorldClockManager>();
            _offset = Vector2.Zero;
        }

        public void LoadContent()
        {
            try
            {
                _texture = _core.Content.Load<Texture2D>("Sprites/UI/Backgrounds/tiled_background_1");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Could not load background texture 'Sprites/UI/Backgrounds/tiled_background_1'. Using fallback. {ex.Message}");
                Debug.Fail("Background texture failed to load. Please ensure 'Content/Sprites/UI/Backgrounds/tiled_background_1.xnb' exists and the asset name is correct.");
                var textureFactory = ServiceLocator.Get<TextureFactory>();
                _texture = textureFactory.CreateColoredTexture(32, 32, new Color(10, 10, 10));
            }
        }

        public void Update(GameTime gameTime)
        {
            if (ScrollDirection != Vector2.Zero)
            {
                // The effective speed is the base ScrollSpeed multiplied by the current time scale.
                float effectiveSpeed = ScrollSpeed * (_worldClockManager.TimeScale);
                _offset += Vector2.Normalize(ScrollDirection) * effectiveSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
        }

        /// <summary>
        /// Draws the tiled background to fill the virtual resolution area.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            DrawTiled(spriteBatch, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);
        }

        /// <summary>
        /// Draws the tiled background to fill the entire physical screen.
        /// </summary>
        public void DrawFullScreen(SpriteBatch spriteBatch)
        {
            var pp = _graphicsDevice.PresentationParameters;
            DrawTiled(spriteBatch, pp.BackBufferWidth, pp.BackBufferHeight);
        }

        /// <summary>
        /// Private helper to draw the tiled texture to a specified width and height.
        /// </summary>
        private void DrawTiled(SpriteBatch spriteBatch, int width, int height)
        {
            if (_texture == null) return;

            // Calculate the starting position for the top-left tile based on the offset.
            // The modulo operator ensures the position wraps around, creating the infinite tiling effect.
            float startX = -(_offset.X % _texture.Width);
            float startY = -(_offset.Y % _texture.Height);

            // Loop through and draw tiles to fill the specified area.
            for (float y = startY; y < height; y += _texture.Height)
            {
                for (float x = startX; x < width; x += _texture.Width)
                {
                    spriteBatch.Draw(_texture, new Vector2(x, y), Color.White);
                }
            }
        }
    }
}