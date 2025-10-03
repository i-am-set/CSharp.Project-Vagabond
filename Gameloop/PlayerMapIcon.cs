using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.UI
{
    public class PlayerMapIcon
    {
        public Vector2 Position { get; private set; }
        private Texture2D? _texture;
        private Vector2 _origin;
        private float _bobTimer;

        public PlayerMapIcon()
        {
            // Constructor is now empty to avoid accessing assets before they are loaded.
        }

        private void InitializeTexture()
        {
            // Lazy initialization: Get the texture only when it's first needed for drawing.
            if (_texture == null)
            {
                _texture = ServiceLocator.Get<SpriteManager>().PlayerSprite;
                if (_texture != null)
                {
                    _origin = new Vector2(_texture.Width / 2f, _texture.Height / 2f);
                }
            }
        }

        public void SetPosition(Vector2 position)
        {
            Position = position;
        }

        public void Update(GameTime gameTime)
        {
            _bobTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            InitializeTexture(); // Ensure texture is loaded before drawing.
            if (_texture == null) return; // Don't draw if the texture is still null.

            const float BOB_SPEED = 2f;
            const float BOB_AMOUNT = 2f;
            float yOffset = MathF.Sin(_bobTimer * BOB_SPEED) * BOB_AMOUNT;
            var drawPosition = new Vector2(Position.X, Position.Y + yOffset);

            spriteBatch.DrawSnapped(_texture, drawPosition, null, Color.White, 0f, _origin, 1f, SpriteEffects.None, 0.5f);
        }
    }
}