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

        /// <summary>
        /// Draws the tiled background to fill the specified destination area, respecting the integer scale.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Rectangle destinationBounds, float scale)
        {
            DrawTiled(spriteBatch, destinationBounds.Width, destinationBounds.Height, scale);
        }

        /// <summary>
        /// Private helper to draw the tiled and scaled texture.
        /// </summary>
        private void DrawTiled(SpriteBatch spriteBatch, int screenWidth, int screenHeight, float scale)
        {
            if (_texture == null || scale <= 0) return;

            float scaledTexWidth = _texture.Width * scale;
            float scaledTexHeight = _texture.Height * scale;

            if (scaledTexWidth == 0 || scaledTexHeight == 0) return;

            // The amount of "scrolled" pixels on screen.
            float scrolledX = _offset.X * scale;
            float scrolledY = _offset.Y * scale;

            // The starting position must wrap around the scaled texture size.
            float startX = -(scrolledX % scaledTexWidth);
            float startY = -(scrolledY % scaledTexHeight);

            // If startX/Y is positive due to a negative offset, wrap it back so we start drawing off-screen.
            if (startX > 0) startX -= scaledTexWidth;
            if (startY > 0) startY -= scaledTexHeight;

            // Loop through and draw scaled tiles to fill the specified area.
            for (float y = startY; y < screenHeight; y += scaledTexHeight)
            {
                for (float x = startX; x < screenWidth; x += scaledTexWidth)
                {
                    spriteBatch.Draw(_texture, new Vector2(x, y), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}