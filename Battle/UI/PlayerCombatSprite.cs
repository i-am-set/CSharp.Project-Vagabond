#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectVagabond.Utils;
using System;

namespace ProjectVagabond.Battle.UI
{
    /// <summary>
    /// Manages the state, animation, and rendering of the player's heart sprite in combat.
    /// </summary>
    public class PlayerCombatSprite
    {
        private Texture2D? _texture;
        private Vector2 _position;
        private Vector2 _origin;

        // Animation
        private float _frameTimer;
        private int _frameIndex;
        private int _frameCount;
        private const int FRAME_WIDTH = 32;
        private const int FRAME_HEIGHT = 32;
        private const float FRAME_DURATION = 0.2f; // ~5 FPS

        public PlayerCombatSprite()
        {
            // Texture is lazy-loaded to ensure SpriteManager is ready.
        }

        private void Initialize()
        {
            if (_texture == null)
            {
                _texture = ServiceLocator.Get<SpriteManager>().PlayerHeartSpriteSheet;
                if (_texture != null)
                {
                    _origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT / 2f);
                    _frameCount = _texture.Width / FRAME_WIDTH;
                }
            }
        }

        public void SetPosition(Vector2 newPosition)
        {
            _position = newPosition;
        }

        public void Update(GameTime gameTime)
        {
            Initialize();
            if (_texture == null || _frameCount <= 1) return;

            _frameTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_frameTimer >= FRAME_DURATION)
            {
                _frameTimer -= FRAME_DURATION;
                _frameIndex = (_frameIndex + 1) % _frameCount;
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            Initialize();
            if (_texture == null) return;

            var sourceRectangle = new Rectangle(_frameIndex * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT);
            spriteBatch.DrawSnapped(_texture, _position, sourceRectangle, Color.White, 0f, _origin, 1f, SpriteEffects.None, 0.5f);
        }
    }
}
#nullable restore