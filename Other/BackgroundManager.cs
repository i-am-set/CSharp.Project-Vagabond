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

            // The destination rectangle is the entire virtual screen.
            var destRect = new Rectangle(0, 0, Global.VIRTUAL_WIDTH, Global.VIRTUAL_HEIGHT);

            // The source rectangle's position is our scrolling offset.
            // Its size is the same as the destination, and SamplerState.LinearWrap handles the tiling.
            var sourceRect = new Rectangle(
                (int)_offset.X,
                (int)_offset.Y,
                destRect.Width,
                destRect.Height
            );

            spriteBatch.Draw(_texture, destRect, sourceRect, Color.White);
        }
    }
}