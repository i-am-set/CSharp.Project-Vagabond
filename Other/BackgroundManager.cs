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

        public Vector2 ScrollDirection { get; set; } = new Vector2(0.1f, 0.1f);
        public float ScrollSpeed { get; set; } = 15f;

        public BackgroundManager()
        {
            _core = ServiceLocator.Get<Core>();
            _graphicsDevice = ServiceLocator.Get<GraphicsDevice>();
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
                _offset += Vector2.Normalize(ScrollDirection) * ScrollSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_texture == null) return;

            // Calculate the starting position for the top-left tile based on the offset.
            // The modulo operator ensures the position wraps around, creating the infinite tiling effect.
            float startX = -(_offset.X % _texture.Width);
            float startY = -(_offset.Y % _texture.Height);

            // We need to draw enough tiles to cover the entire virtual screen.
            int screenWidth = Global.VIRTUAL_WIDTH;
            int screenHeight = Global.VIRTUAL_HEIGHT;

            // Loop through and draw tiles to fill the screen.
            for (float y = startY; y < screenHeight; y += _texture.Height)
            {
                for (float x = startX; x < screenWidth; x += _texture.Width)
                {
                    spriteBatch.Draw(_texture, new Vector2(x, y), Color.White);
                }
            }
        }
    }
}